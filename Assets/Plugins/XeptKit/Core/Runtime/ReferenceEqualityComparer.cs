using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace XeptKit.Core
{
    /// <summary>
    /// 引用相等比较器。仅按引用相等判定，规避「重写 Equals/GetHashCode 的类型被值相等误判」。
    /// 供需按引用跟踪实例的集合（如对象池活跃集）使用。
    /// </summary>
    /// <typeparam name="T">被比较的引用类型</typeparam>
    public sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        public static ReferenceEqualityComparer<T> Instance { get; } = new();

        private ReferenceEqualityComparer() { }

        public bool Equals(T x, T y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
