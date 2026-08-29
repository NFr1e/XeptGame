using System;

namespace XeptKit.Event
{
    /// <summary>
    /// 订阅反注册句柄。持有退订动作，Dispose 即执行；幂等（重复 Dispose 无副作用）。
    /// </summary>
    internal sealed class SubscriptionHandle : IDisposable
    {
        private Action _onDispose;

        public SubscriptionHandle(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            var onDispose = _onDispose;
            _onDispose = null;
            onDispose?.Invoke();
        }
    }
}
