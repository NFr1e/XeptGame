using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using XeptKit.Core;
using XeptKit.Event;
using XeptKit.FSM;
using XeptGame.Gameplay;

namespace XeptGame
{
    /// <summary>
    /// 应用门面：持有 AppFSM，对外提供状态查询、启动、全局 Tick、应用暂停接线与 QuitApp。
    /// 职责边界（见 AppFSM 设计决议）：状态语义在 AppFSM；框架收尾在 AppEntry.Shutdown；本类只做编排接线。
    /// 另：订阅 GameplayFlowErrorEvent（接收 GameplayFSM 死亡信号 → 记失败来源 + 转 ErrorState；
    /// GameplayFSM 的收尾 Shutdown 统一在 ErrorState.OnEnter 执行——不管理其生命周期，只做接管）。
    /// 重试入口按 <see cref="AppFailureSource"/> 分派：启动失败 → <see cref="RetryLaunch"/>（全量重跑）；
    /// 游戏流程失败 → <see cref="RetryGame"/>（ErrorState 已收尾，回 StartingState 重建流程）。
    /// </summary>
    public class AppManager
    {
        /// <summary>应用级状态机（单层，6 状态：Initializing/Starting/Running/Paused/Error/Quitting）。</summary>
        public static Fsm AppFSM { get; private set; }

        /// <summary>应用上下文（EventBus + 会话数据）。</summary>
        public static AppContext Context { get; private set; }

        private IDisposable _errorSubscription;

        /// <summary>组合根装配完成后调用：创建 AppFSM 并订阅状态可观测日志 + 游戏流程错误事件。</summary>
        public void Initialize(AppContext context)
        {
            Guard.NotNull(context, nameof(context));

            Context = context;
            AppFSM = new Fsm(context);
            AppFSM.StateChanged += s => Log.Info($"[AppFSM] State change from {s.From?.ToString() ?? "启动"} to {s.To}");

            // 接收 GameplayFSM 死亡信号（GameplayFlow_Design.md §4.5）——AppFSM 不管理其生命周期，但转 ErrorState 接管
            _errorSubscription = context.EventBus.Subscribe<GameplayFlowErrorEvent>(OnGameplayFlowError);
        }

        /// <summary>启动应用：进入 <see cref="InitializingState"/>（fire-and-forget；失败显式自救进 ErrorState）。</summary>
        public void LaunchApp()
        {
            AppFSM.RequestChange<InitializingState>(KitLifecycle.GlobalToken);
        }

        /// <summary>
        /// 重试启动（**启动失败**路径，<see cref="AppFailureSource.Startup"/>，ErrorState UI 调用）：
        /// 重跑 <see cref="InitializingState"/>（Addressables 等基础设施）→ 成功后显式续接
        /// <see cref="StartingState"/>（首次启动的 Initializing→Starting 由 AfterSceneLoad 桥一次性驱动，
        /// 重试无第二次桥，故在此续接——场景已加载，无场景 Awake 竞态）。
        /// 若初始化再次失败（InitializingState 内部自救转 ErrorState）则跳过续接。
        /// 游戏流程失败请用 <see cref="RetryGame"/>（本方法不触碰 GameplayFSM）。
        /// </summary>
        public async UniTask RetryLaunch()
        {
            if (AppFSM == null || AppFSM.IsTransitioning || AppFSM.CurrentState is not ErrorState)
            {
                return;
            }

            await AppFSM.ChangeStateAsync<InitializingState>(KitLifecycle.GlobalToken);

            // 初始化成功且未再次失败（失败已自转 ErrorState/转移中）才续接内容入口
            if (AppFSM != null && AppFSM.CurrentState is InitializingState && !AppFSM.IsTransitioning)
            {
                AppFSM.RequestChange<StartingState>(KitLifecycle.GlobalToken);
            }
        }

        /// <summary>
        /// 重试游戏流程（**游戏流程失败**路径，<see cref="AppFailureSource.GameplayFlow"/>，ErrorState UI 调用）：
        /// 回 <see cref="StartingState"/> 重建——AppCore 组幂等加载（已加载则跳过）+ 新建 GameplayContext +
        /// <see cref="GameplayManager.Start"/>（ErrorState.OnEnter 已 Shutdown 收尾，可安全重建）。
        /// 启动请求门若已置位（上次已 RequestStart）→ 新 RunAsync 立即消费并**重放同一请求**（重试语义）。
        /// </summary>
        public void RetryGame()
        {
            AppFSM.RequestChange<StartingState>(KitLifecycle.GlobalToken);
        }

        /// <summary>启动应用（当首个场景加载后） </summary>
        public void StartApp()
        {
            AppFSM.RequestChange<StartingState>(KitLifecycle.GlobalToken);
        }

        /// <summary>全局 Tick 入口（由 AppLifecycleBridge.Update 驱动；应用后台时 Unity 停止 Update，物理自然暂停）。</summary>
        public void Tick(float deltaTime) => AppFSM.Tick(deltaTime);

        /// <summary>
        /// 应用暂停/恢复（平台后台 / 全局遮罩，由桥 OnApplicationPause 转发）。
        /// 转移表：Starting ⇄ Paused、Running ⇄ Paused；其余状态忽略（Paused 仅从运行/启动可达）。
        /// </summary>
        public void OnApplicationPause(bool paused)
        {
            if (AppFSM is null) return;

            if (paused)
            {
                if (AppFSM.CurrentState is RunningState or StartingState)
                {
                    AppFSM.RequestChange<PausedState>();
                }
            }
            else if (AppFSM.CurrentState is PausedState)
            {
                AppFSM.RequestChange<RunningState>();
            }
        }

        /// <summary>
        /// 两段式退出：先业务关闭（QuittingState，可异步），再触发 Application.Quit；
        /// 框架收尾由 Application.quitting → AppEntry.Shutdown() 完成（业务关闭期间 GlobalToken 仍有效）。
        /// 幂等：已在退出态或转移中则忽略。
        /// </summary>
        public async UniTask QuitAppAsync()
        {
            if (AppFSM == null || AppFSM.CurrentState is QuittingState || AppFSM.IsTransitioning)
            {
                return;
            }

            await AppFSM.ChangeStateAsync<QuittingState>(KitLifecycle.GlobalToken);
            Application.Quit();
        }

        /// <summary>同步终止 AppFSM（由 AppEntry.Shutdown 框架收尾调用）。</summary>
        public void Dispose()
        {
            _errorSubscription?.Dispose();
            _errorSubscription = null;
            AppFSM?.Dispose();
        }

        /// <summary>
        /// 游戏流程错误处理（GameplayFlow_Design.md §4.5）：记失败来源（GameplayFlow）+ LastError + 转 ErrorState。
        /// **不在此收尾 GameplayFSM**——收尾（GameplayManager.Shutdown：取消编排令牌 + Dispose Fsm）统一在
        /// ErrorState.OnEnter 执行（AppFSM 收敛进 ErrorState 时 GameplayFSM 的失败转移早已结束，时序干净）。
        /// 仅从运行/启动/暂停态可转（Error 已是终态/转移中时忽略——RequestChange 幂等由 FSM 保证；
        /// 含 Paused：平台暂停期间加载失败，恢复后仍须收敛到 ErrorState，不留泄漏）。
        /// </summary>
        private void OnGameplayFlowError(GameplayFlowErrorEvent e)
        {
            Context.LastError = e.Exception ?? new Exception(e.Message);
            Context.FailureSource = AppFailureSource.GameplayFlow; // ErrorState 重试路径据此分派（RetryGame）
            if (AppFSM != null && AppFSM.CurrentState is RunningState or StartingState or PausedState)
            {
                AppFSM.RequestChange<ErrorState>();
            }
        }
    }
}
