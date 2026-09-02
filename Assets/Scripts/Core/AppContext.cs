using System;
using Cysharp.Threading.Tasks;
using XeptKit.Event;

namespace XeptGame
{
    /// <summary>AppFSM 失败来源（ErrorState 重试路径据此分派：启动失败 → RetryLaunch；游戏流程失败 → RetryGame）。</summary>
    public enum AppFailureSource
    {
        /// <summary>无失败（未进入 ErrorState）。</summary>
        None,

        /// <summary>启动失败（Initializing / Starting 阶段；重试走 RetryLaunch 全量重跑）。</summary>
        Startup,

        /// <summary>游戏流程失败（GameplayFSM 加载阶段 Fail；重试走 RetryGame 重建流程）。</summary>
        GameplayFlow,
    }

    /// <summary>
    /// AppFSM 类型化上下文：App 域事件总线 + 应用级会话数据（启动失败原因）+ 启动请求门。
    /// 依据 XeptKit.FSM 约定：状态实例按类型缓存、不能构造注入，会话数据必须经 Context 读写。
    /// **启动请求门**：场景组件（主菜单"开始游戏"按钮 / LevelBoot / LoadLevelButton）写入启动请求时置位；GameLoadingManager 消费——
    /// 门同步消除"请求写入"与"流程就绪"的时序竞态（写入早则门已置位立即过，写入晚则流程等待）。
    /// </summary>
    public sealed class AppContext
    {
        public AppContext(IEventBus eventBus)
        {
            EventBus = eventBus;
        }

        /// <summary>App 域事件总线（应用生命周期事件；Gameplay 域事件见 GameplayContext.EventBus）。</summary>
        public IEventBus EventBus { get; }

        /// <summary>最近一次失败原因（由 Initializing/Starting 状态或 GameplayFlowErrorEvent 桥接写入，ErrorState 读取呈现）。</summary>
        public Exception LastError { get; set; }

        /// <summary>最近一次失败来源（ErrorState 区分重试路径；无失败 = None）。</summary>
        public AppFailureSource FailureSource { get; set; } = AppFailureSource.None;

        /// <summary>
        /// 启动请求门：GameLoadingManager.RequestStart 写入请求时置位（幂等）；GameLoadingManager.RunAsync
        /// 消费（await 后读取请求）。请求源：主菜单"开始游戏"按钮 / LevelBoot（Editor 直接 Play 关卡）/
        /// LoadLevelButton。无请求 = 停在 Boot 等待内容入口（正常语义，无超时，仅取消时退出）。
        /// </summary>
        public UniTaskCompletionSource StartRequested { get; } = new();
    }
}
