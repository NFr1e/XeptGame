namespace XeptGame
{
    /// <summary>
    /// 应用级暂停态（平台后台 / 全局遮罩）。语义：Paused 是声明 + 事件（StateChanged），
    /// 不自动暂停任何业务——音频/计时/GameplayFSM 等自行订阅响应（无隐式规则）。
    /// </summary>
    public sealed class PausedState : AppStateBase
    {
    }
}
