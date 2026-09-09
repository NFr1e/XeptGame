using System;

namespace XeptGame.Game.Flow
{
    /// <summary>
    /// 一轮 GameplaySession 域入口（更名自 GameplayEntry；GameplaySession_Domain_Design.md §3，R2/R5）：
    /// 职责收窄为<b>装配 + 心跳转发</b>——创建并驱动 <see cref="Context"/>（域根：总线/数据/服务），
    /// 不再直接摊 Session/服务对象。
    /// <list type="bullet">
    /// <item><b>入口</b>：静态 Instance / Instantiate / Init / Dispose / TryGetInstance（非抛出访问口，
    /// 场景模块经此取 Context）；</item>
    /// <item><b>驱动</b>：Update 转发 gameplayDelta 到 Context.Tick；App/Game 状态变化经<b>下行"铃"（域总线事件）</b>
    /// 即时刷新暂停——状态类判断收口在各域门面（AppManager.IsRunning / GameManager.IsPlaying），本域不触碰外层具体状态类；</item>
    /// <item>生命周期桥 = <see cref="GameplaySessionLifecycleBridge"/>（GameplayLoadState 创建 / UnloadState 弃）；</item>
    /// <item>业务访问一律走 <c>Context.*</c>（门面收口后为 Context.Equip.*，见域重构决议）。</item>
    /// </list>
    /// </summary>
    public class GameplaySessionEntry
    {
        public static GameplaySessionEntry Instance { get; private set; }

        /// <summary>一轮域根对象（GameplaySessionContext：EventBus/Inventory/Equipment/Equip 服务；与入口同生共死）。</summary>
        public GameplaySessionContext Context { get; private set; }

        private IDisposable _gameGateSubscription;
        private IDisposable _appGateSubscription;

        /// <summary>
        /// 非抛出式取当前一轮入口（未初始化/已卸载返回 false）——场景侧组件（WorldItem 等）的无异常防御访问口，
        /// 避免把"启动时序错误"做成异常控制流。
        /// </summary>
        public static bool TryGetInstance(out GameplaySessionEntry entry)
        {
            entry = Instance;
            return Instance != null;
        }

        /// <summary>
        /// 是否处于游玩中（门控）：只在应用运行（App 门面现值）且玩法 Playing（Game 门面现值）时才解冻装备行为。
        /// 状态类判断收口在各自域门面（AppManager.IsRunning / GameManager.IsPlaying），本域不触碰具体状态类。
        /// </summary>
        public bool IsPlaying => AppManager.IsRunning && GameManager.IsPlaying;

        /// <summary>创建一轮入口（幂等）：首次创建实例，装配在 <see cref="Init"/> 完成。</summary>
        public static GameplaySessionEntry Instantiate()
        {
            Instance ??= new GameplaySessionEntry();
            return Instance;
        }

        /// <summary>装配一轮（幂等）：创建域根并订阅外层状态"铃"（域总线事件，只当变化通知）。</summary>
        public GameplaySessionEntry Init()
        {
            if (Context == null)
            {
                Context = new GameplaySessionContext();
                // 下行"铃"：订阅外层状态变化（不解析负载/状态类型）；现值经门面查询（IsPlaying），
                // 事件不重放初值 → 装配后立即按门面同步一次。
                _gameGateSubscription = GameManager.Context?.EventBus?.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
                _appGateSubscription = AppManager.Context?.EventBus?.Subscribe<AppStateChangedEvent>(OnAppStateChanged);
                ApplyGate();
            }

            return Instance;
        }

        /// <summary>卸载一轮（幂等）：退订铃，弃域根并置空入口引用。</summary>
        public void Dispose()
        {
            _gameGateSubscription?.Dispose();
            _gameGateSubscription = null;
            _appGateSubscription?.Dispose();
            _appGateSubscription = null;

            Context?.Dispose();
            Context = null;
            Instance = null;
        }

        #region LifecycleDriver
        public void Start()
        {
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            Context?.SetPaused(!IsPlaying);
            Context?.Tick(deltaTime);
        }

        /// <summary>玩法状态铃（Game 域广播）；不解析负载，铃响按门面现值刷新暂停。</summary>
        private void OnGameStateChanged(GameStateChangedEvent _) => ApplyGate();

        /// <summary>应用状态铃（App 域广播）；同上。</summary>
        private void OnAppStateChanged(AppStateChangedEvent _) => ApplyGate();

        private void ApplyGate() => Context?.SetPaused(!IsPlaying);

        public void LateUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }
        #endregion
    }
}
