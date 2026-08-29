using System;

namespace XeptKit.Core
{
    /// <summary>
    /// 非泛型结果类型，用于"仅校验成败"的操作（保存、校验等），避免被迫携带值类型。
    /// </summary>
    public readonly struct Result
    {
        private readonly Error _error;
        private readonly bool _success;

        private Result(Error error, bool success)
        {
            _error = error;
            _success = success;
        }

        public bool IsSuccess => _success;

        public bool IsFailure => !_success;

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

        public static Result Success() => new Result(default, true);

        public static Result Failure(Error error) => new Result(error, false);

        /// <summary>安全获取错误，不抛异常。</summary>
        public bool TryGetError(out Error error)
        {
            if (!_success)
            {
                error = _error;
                return true;
            }

            error = default;
            return false;
        }

        public override string ToString() => _success ? "Success" : $"Failure: {_error}";
    }
}
