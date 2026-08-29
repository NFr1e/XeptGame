using System;

namespace XeptKit.Core
{
    /// <summary>
    /// 日志静态门面。默认实现为 UnityLog（internal），开箱即用；
    /// 业务可在组合根启动期通过 <see cref="SetImplementation"/> 替换实现。
    /// 注意：_impl 为静态字段，关闭 Domain Reload 时会跨会话残留，
    /// 约定组合根在每次会话启动时重新 SetImplementation。
    /// </summary>
    public static class Log
    {
        private static ILog _impl = new UnityLog();

        public static void SetImplementation(ILog impl)
        {
            Guard.NotNull(impl, nameof(impl));
            _impl = impl;
        }

        /// <summary>当前日志实现（供保存/恢复场景，如测试内临时替换后还原、组合根切换）。</summary>
        public static ILog GetImplementation() => _impl;

        public static void Info(string message) => _impl.Info(message);

        public static void Warning(string message) => _impl.Warning(message);

        public static void Error(string message) => _impl.Error(message);

        public static void Exception(Exception exception) => _impl.Exception(exception);
    }
}
