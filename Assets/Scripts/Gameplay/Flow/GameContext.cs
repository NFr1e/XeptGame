using System;
using Cysharp.Threading.Tasks;
using XeptGame.Game.Flow;
using XeptKit.Asset;
using XeptKit.Event;
using XeptKit.Scenes;

namespace XeptGame.Game
{
    /// <summary>
    /// GameplayFSM 类型化上下文（纯 C# 会话数据，GameplayFlow_Design.md §3.1）：
    /// 依据 XeptKit.FSM 约定——状态实例按类型缓存复用、不能构造注入，会话数据必须经 Context 读写。
    /// **域隔离**：EventBus 为本域自建（与 App 域隔离，生命周期归 GameplayManager——Shutdown 时 Clear）；
    /// 跨域事件（错误上报）由 GameplayManager 显式桥接。
    /// **完成门**（GameplayReady/LevelReady）由状态类（执行器）加载完成后置位，GameLoadingManager 消费推进——
    /// 门仅覆盖确定性事项（场景组加载），业务模块自生命周期初始化、不进加载门控（GameplayFlow_Design.md §4.2）。
    /// </summary>
    public sealed class GameContext
    {
        public GameContext(IEventBus appEvtBus, IScenesManager scenesManager, IAssetLoader assetLoader)
        {
            AppEventBus = appEvtBus;
            ScenesManager = scenesManager;
            AssetLoader = assetLoader;
            EventBus = new EventBus(); // Gameplay 域总线（自建，独立生命周期）
        }

        /// <summary>App 域事件总线，跨域桥接：Gameplay 域错误事件 → App 域（AppManager 订阅不变；Gameplay 域总线独立生命周期）</summary>
        public IEventBus AppEventBus { get; set; }

        /// <summary>Gameplay 域事件总线（自建；状态/错误事件；Shutdown 时 Clear）。</summary>
        public IEventBus EventBus { get; }

        /// <summary>场景加载服务（纯 C#，无场景依赖）。</summary>
        public IScenesManager ScenesManager { get; }

        /// <summary>资产加载服务（基座场景组按 Addressables key 加载——index 0 为 AppEntry 场景，基座非启动即加载）。</summary>
        public IAssetLoader AssetLoader { get; }

        /// <summary>
        /// 生命周期桥接（GameplayEntry 生命周期驱动器，GameplayLoadState 初始化；GameplayUnloadState 卸载）。
        /// </summary>
        public GameplayLifecycleBridge LifecycleBridge { get; set; }

        /// <summary>
        /// 当前关卡组（GameLoadingManager 从启动请求写入；null = 已在关卡中/无关卡组，LevelLoad 跳过）。
        /// </summary>
        public SceneGroup LevelGroup { get; set; }

        /// <summary>基座场景组就绪门：GameplayLoadState 加载完成后置位；GameLoadingManager 消费推进。</summary>
        public UniTaskCompletionSource GameplayReady { get; } = new();

        /// <summary>关卡组就绪门：LevelLoadState 加载完成后置位；GameLoadingManager 消费推进。</summary>
        public UniTaskCompletionSource LevelReady { get; } = new();

        /// <summary>游戏流程失败原因（错误上报用，ErrorState/日志读取）。</summary>
        public Exception LastError { get; set; }
    }
}
