using System.Threading;
using XeptKit.Core;
using XeptKit.Event;
using XeptKit.FSM;

namespace XeptGame.Game
{
    /// <summary>
    /// 游戏流程门面（纯 C#，镜像 AppManager）：持有 GameplayFSM，提供启动、状态事件发布与**跨域桥接**。
    /// 职责边界（GameplayFlow_Design.md §2.1）：状态语义在 GameplayFSM；加载编排在 GameLoadingManager；
    /// AppFSM 不管理本机生命周期（仅 StartingState 拉起，fire-and-forget；错误时由 ErrorState 收尾 Shutdown）。
    /// 装配器等场景侧经 EventBus 订阅 <see cref="GameStateChangedEvent"/>，**不持本类实例**。
    /// </summary>
    public static class GameManager
    {
        /// <summary>游戏流程状态机（单层：Boot → GameplayLoad → LevelLoad → Playing ⇄ Paused）。</summary>
        public static Fsm GameFSM { get; private set; }

        /// <summary>游戏流程上下文（会话数据唯一载体；Gameplay 域总线生命周期归本类）。</summary>
        public static GameContext Context { get; private set; }

        /// <summary>
        /// 本机生命周期令牌源（linked 到 <see cref="KitLifecycle.GlobalToken"/>）：
        /// 编排器 <see cref="GameLoadingManager.RunAsync"/> 绑定本令牌而非裸 GlobalToken——
        /// **Shutdown 时 Cancel 即让挂在完成门上的编排器以 OCE 退出**，立即释放其对 Context 的持有
        /// （错误后收尾，不必活到会话结束；见 GameplayFlow_Design.md §4.5）。
        /// </summary>
        private static CancellationTokenSource _lifecycleCts;

        /// <summary>
        /// 启动游戏流程：创建 GameplayFSM、订阅状态可观测日志与事件发布、建立跨域桥接，
        /// 进入 BootState（停驻）并启动加载编排（<see cref="GameLoadingManager.RunAsync"/>，fire-and-forget）。
        /// 由 AppFSM.StartingState 调用（fire-and-forget）；幂等（已启动则忽略）。
        /// </summary>
        public static void Start(GameContext context)
        {
            if (GameFSM != null)
            {
                return;
            }

            Context = context;
            _lifecycleCts = CancellationTokenSource.CreateLinkedTokenSource(KitLifecycle.GlobalToken);
            GameFSM = new Fsm(context);
            GameFSM.StateChanged += s => Log.Info($"[GameFSM] State change from {s.From?.ToString() ?? "启动"} to {s.To}");
            GameFSM.StateChanged += OnStateChanged;

            // 跨域桥接：Gameplay 域错误事件 → App 域（AppManager 订阅不变；Gameplay 域总线独立生命周期）
            context.EventBus.Subscribe<GameFlowErrorEvent>(e => context.AppEventBus.Publish(e));

            GameFSM.RequestChange<BootState>(_lifecycleCts.Token);

            // 加载编排：常驻等待启动请求门（Boot 停驻——**停在门上不加载任何东西**）→ 有请求才推进加载阶段。
            // 必须在 FSM 创建即启动（RequestStart 可能早于本机就绪，门同步保证请求不丢）。
            // 令牌用本机 linked CTS（非裸 GlobalToken）：Shutdown 取消即退出，不活到会话结束。
            _ = GameLoadingManager.RunAsync(_lifecycleCts.Token);
        }

        /// <summary>
        /// 终止游戏流程（幂等；错误收尾 = AppFSM.ErrorState.OnEnter，应用收尾亦复用）：
        /// ① Cancel+Dispose 本机令牌 → 编排器 OCE 退出（释放 Context 持有）→ ② 同步 Dispose Fsm（终止标记，
        /// 非优雅：错误态已停驻/转移已收敛，无需 Exit 钩子）→ ③ 清 Gameplay 域总线 + 置空引用。
        /// Shutdown 后 Start 可再次重建（重试路径：ErrorState → RetryGame → StartingState）。
        /// </summary>
        public static void Shutdown()
        {
            if (_lifecycleCts != null)
            {
                _lifecycleCts.Cancel(); // 编排器挂在完成门上 → OCE 退出（OCE 在 RunAsync 内静默收敛）
                _lifecycleCts.Dispose();
                _lifecycleCts = null;
            }

            GameFSM?.Dispose();
            GameFSM = null;

            if (Context != null)
            {
                (Context.EventBus as ISubscriptionClearable)?.Clear(); // Gameplay 域独立清场
                Context = null;
            }
        }

        /// <summary>状态变化转发到 Gameplay 域 EventBus（场景侧经此感知流程节点）。</summary>
        private static void OnStateChanged(Fsm.StateChangeArgs args)
        {
            Context?.EventBus.Publish(new GameStateChangedEvent(args.To, args.From));
        }
    }
}
