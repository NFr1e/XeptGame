using XeptKit.FSM;

namespace XeptGame
{
    /// <summary>
    /// 应用流程状态变化事件（App 域对外发布，下行"铃"）：AppFSM 状态切换 → App 域总线。
    /// 订阅者（内层会话等）**不解析负载**、仅当变化通知（铃），当前值经 <see cref="AppManager.IsRunning"/> 等门面查询——
    /// 事件不重放初值，现值由门面保证（装配时同步一次）。
    /// </summary>
    public readonly struct AppStateChangedEvent
    {
        /// <summary>新状态。</summary>
        public readonly StateBase To;

        /// <summary>旧状态（null = 初始进入）。</summary>
        public readonly StateBase From;

        public AppStateChangedEvent(StateBase to, StateBase from)
        {
            To = to;
            From = from;
        }
    }
}
