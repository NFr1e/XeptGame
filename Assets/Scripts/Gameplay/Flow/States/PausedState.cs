namespace XeptGame.Game
{
    /// <summary>
    /// 游戏流程暂停态（GameplayFlow_Design.md §4.4）：**游戏菜单暂停**（与 AppFSM.Paused 的**平台级暂停**
    /// 语义不同）。恢复路径由未来菜单/系统触发 <c>RequestChange&lt;PlayingState&gt;</c>。
    /// 平台暂停（AppFSM.Paused）经 Unity 生命周期自然冻结（Update 停），不要求本状态转移；
    /// 联动策略（如"平台已暂停时不重复进菜单"）实现期细化。
    /// 骨架：占位（当前无菜单场景）。
    /// </summary>
    public sealed class PausedState : GameStateBase
    {
    }
}
