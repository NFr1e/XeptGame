using System;
using XeptKit.Event;

namespace XeptGame
{
    /// <summary>
    /// AppFSM 类型化上下文：EventBus + 应用级会话数据（如启动失败原因）。
    /// 依据 XeptKit.FSM 约定：状态实例按类型缓存、不能构造注入，会话数据必须经 Context 读写。
    /// </summary>
    public sealed class AppContext
    {
        public AppContext(IEventBus eventBus)
        {
            EventBus = eventBus;
        }

        /// <summary>应用内事件总线（供状态广播/订阅）。</summary>
        public IEventBus EventBus { get; }

        /// <summary>最近一次启动失败原因（由 Initializing/Starting 状态失败时写入，ErrorState 读取呈现）。</summary>
        public Exception LastError { get; set; }
    }
}
