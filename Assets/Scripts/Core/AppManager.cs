using UnityEngine;
using Cysharp.Threading.Tasks;
using XeptKit.Core;
using XeptKit.FSM;

namespace XeptGame
{
    /// <summary>
    /// 应用门面：持有 AppFSM，对外提供状态查询、启动、全局 Tick、应用暂停接线与 QuitApp。
    /// 职责边界（见 AppFSM 设计决议）：状态语义在 AppFSM；框架收尾在 AppEntry.Shutdown；本类只做编排接线。
    /// </summary>
    public class AppManager
    {
        /// <summary>应用级状态机（单层，6 状态：Initializing/Starting/Running/Paused/Error/Quitting）。</summary>
        public static Fsm AppFSM { get; private set; }

        /// <summary>应用上下文（EventBus + 会话数据）。</summary>
        public static AppContext Context { get; private set; }

        /// <summary>组合根装配完成后调用：创建 AppFSM 并订阅状态可观测日志。</summary>
        public void Initialize(AppContext context)
        {
            Guard.NotNull(context, nameof(context));

            Context = context;
            AppFSM = new Fsm(context);
            AppFSM.StateChanged += s => Log.Info($"[AppFSM] State change from {s.From?.ToString() ?? "启动"} to {s.To}");
        }

        /// <summary>启动应用：进入 <see cref="InitializingState"/>（fire-and-forget；失败显式自救进 ErrorState）。</summary>
        public void StartApp()
        {
            AppFSM.RequestChange<InitializingState>(KitLifecycle.GlobalToken);
        }

        /// <summary>重试启动（ErrorState 重试路径）：重新进入 <see cref="InitializingState"/>。</summary>
        public void RetryStart()
        {
            AppFSM.RequestChange<InitializingState>(KitLifecycle.GlobalToken);
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
        public void Dispose() => AppFSM?.Dispose();
    }
}
