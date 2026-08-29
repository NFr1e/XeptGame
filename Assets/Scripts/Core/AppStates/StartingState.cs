namespace XeptGame
{
    /// <summary>
    /// 内容入口：加载首个场景、拉起业务流程（GameplayFSM 等）。
    /// 业务细化 TODO：经 AppEntry.ScenesManager 加载首个场景 / 启动 GameplayFSM。
    /// 当前骨架：直接进入运行态。完成 → Running；失败 → Error（显式自救）。
    /// </summary>
    public sealed class StartingState : AppStateBase
    {
        public override void OnEnter()
        {
            // 业务细化：此处做内容入口（首场景/业务流程启动），失败时
            // App.LastError = ex; Fsm.RequestChange<ErrorState>();
            Fsm.RequestChange<RunningState>();
        }
    }
}
