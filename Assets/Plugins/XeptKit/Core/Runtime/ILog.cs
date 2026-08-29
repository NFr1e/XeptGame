using System;

namespace XeptKit.Core
{
    /// <summary>框架日志抽象。同步方法，第一版不做 params 格式化重载。</summary>
    public interface ILog
    {
        void Info(string message);

        void Warning(string message);

        void Error(string message);

        void Exception(Exception exception);
    }
}
