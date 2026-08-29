using System;
using System.Collections.Generic;
using XeptKit.Core;

namespace XeptKit.Event
{
    /// <summary>
    /// <see cref="IEventBus"/> 的默认实现。主线程 only、无锁。
    /// 内部以类型擦除字典存储 handler 数组；Subscribe / Unsubscribe 重建数组（低频），
    /// Publish 读取当前数组快照并迭代（零分配），回调中增删订阅不影响进行中的发布（作用在新数组上）。
    /// </summary>
    public sealed class EventBus : IEventBus, ISubscriptionClearable
    {
        private readonly Dictionary<Type, HandlerCollection> _handlers = new Dictionary<Type, HandlerCollection>();

        /// <inheritdoc />
        public event Action<Type, Exception> HandlerException;

        public IDisposable Subscribe<T>(Action<T> handler, int priority = 0)
        {
            Guard.NotNull(handler, nameof(handler));

            _handlers.GetOrAdd(typeof(T), () => new HandlerCollection()).Add(handler, priority);
            return new SubscriptionHandle(() => Remove(typeof(T), handler));
        }

        public bool Unsubscribe<T>(Action<T> handler)
        {
            Guard.NotNull(handler, nameof(handler));
            return Remove(typeof(T), handler);
        }

        public void Publish<T>(T eventData)
        {
            if (!_handlers.TryGetValue(typeof(T), out var collection))
            {
                return;
            }

            var entries = collection.Entries; // 快照读取，零分配
            for (int i = 0; i < entries.Length; i++)
            {
                try
                {
                    ((Action<T>)entries[i].Handler)(eventData);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                    RaiseHandlerException(typeof(T), ex);
                }
            }
        }

        public bool Contains<T>(Action<T> handler)
        {
            Guard.NotNull(handler, nameof(handler));

            if (!_handlers.TryGetValue(typeof(T), out var collection))
            {
                return false;
            }

            return collection.Contains(handler);
        }

        public void Clear() => _handlers.Clear();

        private bool Remove(Type eventType, Delegate handler)
        {
            if (!_handlers.TryGetValue(eventType, out var collection))
            {
                return false;
            }

            bool removed = collection.Remove(handler);
            if (collection.Count == 0)
            {
                _handlers.Remove(eventType);
            }

            return removed;
        }

        private void RaiseHandlerException(Type type, Exception ex)
        {
            try
            {
                HandlerException?.Invoke(type, ex);
            }
            catch (Exception hookEx)
            {
                Log.Exception(hookEx);
            }
        }

        /// <summary>按类型分组的 handler 集合，内部维护按 priority 降序的数组。</summary>
        private sealed class HandlerCollection
        {
            private HandlerEntry[] _entries = Array.Empty<HandlerEntry>();

            public HandlerEntry[] Entries => _entries;

            public int Count => _entries.Length;

            /// <summary>添加 handler。若已存在相同委托实例，幂等（忽略重复订阅）。</summary>
            public void Add(Delegate handler, int priority)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i].Handler.Equals(handler))
                    {
                        return;
                    }
                }

                var newArray = new HandlerEntry[_entries.Length + 1];
                Array.Copy(_entries, newArray, _entries.Length);
                newArray[^1] = new HandlerEntry(handler, priority);

                // 按 priority 降序（越大越先）。Array.Sort 非稳定，同优先级顺序无保证。
                Array.Sort(newArray, (a, b) => b.Priority.CompareTo(a.Priority));
                _entries = newArray;
            }

            public bool Remove(Delegate handler)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i].Handler.Equals(handler))
                    {
                        var newArray = new HandlerEntry[_entries.Length - 1];
                        Array.Copy(_entries, 0, newArray, 0, i);
                        Array.Copy(_entries, i + 1, newArray, i, _entries.Length - i - 1);
                        _entries = newArray;
                        return true;
                    }
                }

                return false;
            }

            public bool Contains(Delegate handler)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i].Handler.Equals(handler))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private readonly struct HandlerEntry
        {
            public readonly Delegate Handler;
            public readonly int Priority;

            public HandlerEntry(Delegate handler, int priority)
            {
                Handler = handler;
                Priority = priority;
            }
        }
    }
}
