using System;
using System.Collections.Generic;
using XeptKit.Core;

namespace XeptKit.Pool
{
    /// <summary>
    /// C# 引用类型对象池。基于 <see cref="Stack{T}"/> 管理闲置实例，通过工厂委托创建，
    /// 支持预热与整体释放。活跃追踪按引用相等判定（<see cref="ReferenceEqualityComparer{T}"/>），
    /// 规避「T 重写 Equals/GetHashCode 后不同实例被值相等误判」。主线程 only、无锁。
    /// </summary>
    /// <typeparam name="T">池化的引用类型</typeparam>
    public sealed class ReferencePool<T> : IDisposable where T : class
    {
        private readonly Stack<T> _inactive = new Stack<T>();
        private readonly HashSet<T> _active = new HashSet<T>(ReferenceEqualityComparer<T>.Instance);
        private readonly Func<T> _factory;

        /// <summary>当前活跃（已取出未归还）的对象数量。</summary>
        public int ActiveCount => _active.Count;

        /// <summary>当前闲置（已归还可复用）的对象数量。</summary>
        public int InactiveCount => _inactive.Count;

        /// <summary>
        /// 创建 C# 引用类型对象池。
        /// </summary>
        /// <param name="factory">创建新实例的工厂委托。不可为 null。</param>
        /// <param name="defaultCapacity">初始预热数量。大于 0 时构造期间自动预热。</param>
        public ReferencePool(Func<T> factory, int defaultCapacity = 0)
        {
            Guard.NotNull(factory, nameof(factory));
            Guard.InRange(defaultCapacity, 0, int.MaxValue, nameof(defaultCapacity));

            _factory = factory;

            if (defaultCapacity > 0)
            {
                Prewarm(defaultCapacity);
            }
        }

        /// <summary>
        /// 从池中获取一个实例。有闲置则复用，否则经工厂创建；返回已 OnSpawn 的实例。
        /// </summary>
        public T Get()
        {
            T instance;

            if (_inactive.Count > 0)
            {
                instance = _inactive.Pop();
            }
            else
            {
                instance = CreateInstance();
            }

            _active.Add(instance);

            if (instance is IPoolable poolable)
            {
                try
                {
                    poolable.OnSpawn();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
            }

            return instance;
        }

        /// <summary>
        /// 将实例归还池中。重复归还、误还他池对象、null 静默忽略。
        /// </summary>
        public void Release(T instance)
        {
            if (instance == null || !_active.Remove(instance))
            {
                return;
            }

            if (instance is IPoolable poolable)
            {
                try
                {
                    poolable.OnDespawn();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
            }

            _inactive.Push(instance);
        }

        /// <summary>
        /// 预热池，预先创建指定数量的实例。
        /// </summary>
        public void Prewarm(int count)
        {
            Guard.InRange(count, 0, int.MaxValue, nameof(count));

            for (int i = 0; i < count; i++)
            {
                var instance = CreateInstance();

                if (instance is IPoolable poolable)
                {
                    try
                    {
                        poolable.OnDespawn();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                }

                _inactive.Push(instance);
            }
        }

        /// <summary>
        /// 整体释放：清空活跃与闲置集合，引用交 GC 回收。池仍可继续使用。幂等。
        /// </summary>
        public void Clear()
        {
            _active.Clear();
            _inactive.Clear();
        }

        /// <summary>等价 <see cref="Clear"/>，幂等，可进 using / CompositeDisposable。</summary>
        public void Dispose() => Clear();

        private T CreateInstance()
        {
            var instance = _factory();
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"ReferencePool<{typeof(T).Name}>: 工厂委托返回了 null，工厂必须始终返回有效的 {typeof(T).Name} 实例。");
            }

            return instance;
        }
    }
}
