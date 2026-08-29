using System;

namespace XeptKit.Core
{
    /// <summary>
    /// 可空值类型。default(Optional&lt;T&gt;) 即 None；不提供 T → Optional&lt;T&gt; 隐式转换
    /// （避免掩盖 null 误用），强制走 Some / None。
    /// </summary>
    public readonly struct Optional<T>
    {
        private readonly T _value;
        private readonly bool _hasValue;

        private Optional(T value, bool hasValue)
        {
            _value = value;
            _hasValue = hasValue;
        }

        public bool HasValue => _hasValue;

        /// <summary>无值访问将抛出 InvalidOperationException；安全取值请用 <see cref="GetValueOrDefault"/>。</summary>
        public T Value
        {
            get
            {
                if (!_hasValue)
                {
                    throw new InvalidOperationException("Optional 无值，无法访问 Value。");
                }

                return _value;
            }
        }

        public T GetValueOrDefault(T fallback = default) => _hasValue ? _value : fallback;

        public static Optional<T> Some(T value) => new Optional<T>(value, true);

        public static Optional<T> None => default;

        public override string ToString() => _hasValue ? $"Some({_value})" : "None";
    }
}
