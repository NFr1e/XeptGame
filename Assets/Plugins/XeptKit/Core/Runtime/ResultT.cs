using System;

namespace XeptKit.Core
{
    /// <summary>
    /// 携带值或错误的结果类型。readonly struct，零分配，适合热路径。
    /// 不做 Match/Map 等 LINQ 风格方法（YAGNI，需要时再补）。
    /// </summary>
    public readonly struct Result<T>
    {
        private readonly T _value;
        private readonly Error _error;
        private readonly bool _success;

        private Result(T value, Error error, bool success)
        {
            _value = value;
            _error = error;
            _success = success;
        }

        public bool IsSuccess => _success;

        public bool IsFailure => !_success;

        /// <summary>失败态访问将抛出 InvalidOperationException；安全取值请用 <see cref="TryGetValue"/>。</summary>
        public T Value
        {
            get
            {
                if (!_success)
                {
                    throw new InvalidOperationException("Result 为失败态，无法访问 Value。");
                }

                return _value;
            }
        }

        /// <summary>成功态访问将抛出 InvalidOperationException。</summary>
        public Error Error
        {
            get
            {
                if (_success)
                {
                    throw new InvalidOperationException("Result 为成功态，无法访问 Error。");
                }

                return _error;
            }
        }

        public static Result<T> Success(T value) => new Result<T>(value, default, true);

        public static Result<T> Failure(Error error) => new Result<T>(default, error, false);

        /// <summary>安全取值，不抛异常。</summary>
        public bool TryGetValue(out T value)
        {
            if (_success)
            {
                value = _value;
                return true;
            }

            value = default;
            return false;
        }

        public override string ToString() => _success ? $"Success: {_value}" : $"Failure: {_error}";
    }
}
