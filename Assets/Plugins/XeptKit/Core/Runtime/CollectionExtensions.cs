using System;
using System.Collections.Generic;

namespace XeptKit.Core
{
    public static class CollectionExtensions
    {
        /// <summary>取或建：键存在则返回已有值，否则用 factory 创建并写入字典。</summary>
        public static TValue GetOrAdd<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> factory)
        {
            Guard.NotNull(dictionary, nameof(dictionary));
            Guard.NotNull(factory, nameof(factory));

            if (!dictionary.TryGetValue(key, out var value))
            {
                value = factory();
                dictionary[key] = value;
            }

            return value;
        }

        /// <summary>Fisher-Yates 洗牌，原地打乱顺序。</summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            Guard.NotNull(list, nameof(list));

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
