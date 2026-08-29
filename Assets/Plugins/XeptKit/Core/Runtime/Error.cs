using System;

namespace XeptKit.Core
{
    /// <summary>
    /// 轻量错误结构，作为 Result / Result&lt;T&gt; 的错误承载。
    /// code 用 int 承载；具体错误码枚举属业务概念，由业务层定义并转换。
    /// </summary>
    public readonly struct Error
    {
        public int Code { get; }

        public string Message { get; }

        public Exception InnerException { get; }

        private Error(int code, string message, Exception innerException = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            InnerException = innerException;
        }

        public static Error Create(int code, string message, Exception inner = null)
            => new Error(code, message, inner);

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Message))
            {
                return $"Code: {Code}";
            }

            return $"Code: {Code} {Message}";
        }
    }
}
