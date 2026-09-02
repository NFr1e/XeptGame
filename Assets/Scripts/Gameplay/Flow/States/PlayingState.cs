namespace XeptGame.Gameplay
{
    /// <summary>
    /// 游戏流程玩法态（GameplayFlow_Design.md §4.3）：玩法进行中。
    /// 进入时经 GameplayManager 发布 <see cref="GameplayStateChangedEvent"/>（装配器等场景侧响应
    /// "玩法已开始"）；**Update 空转**——实际玩法由场景组件驱动（PlayerController.Update /
    /// InteractionDetector.Update 等），状态机只做流程编排不碰玩法。
    /// ⇄ PausedState（游戏菜单暂停）。
    /// </summary>
    public sealed class PlayingState : GameplayStateBase
    {
        // 玩法由场景组件驱动（帧驱动归组件），本状态仅承载"玩法进行中"的流程语义。
    }
}
