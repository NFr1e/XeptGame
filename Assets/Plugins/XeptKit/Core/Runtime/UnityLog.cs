using System;
using UnityEngine;

namespace XeptKit.Core
{
    /// <summary>默认日志实现：输出到 Unity Debug。internal，不对外暴露。</summary>
    internal sealed class UnityLog : ILog
    {
        public void Info(string message) => Debug.Log(message);

        public void Warning(string message) => Debug.LogWarning(message);

        public void Error(string message) => Debug.LogError(message);

        public void Exception(Exception exception) => Debug.LogException(exception);
    }
}
