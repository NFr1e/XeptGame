namespace XeptGame.Interaction.Behaviours
{
    /// <summary>
    /// 火源目标端口（桥接层）：点燃 / 熄灭 / 添柴三件事的操作面与状态读面。
    /// 由场景对象自愿实现（演示火堆 <c>DemoCampfireProp</c>）；未来的门/开关等"有状态场景物"照此加端口。
    /// </summary>
    public interface IFireTarget : IInteractionTarget
    {
        /// <summary>是否燃烧中。</summary>
        bool IsLit { get; }

        /// <summary>是否还能添柴（燃着且未满）。</summary>
        bool CanAddFuel { get; }

        /// <summary>点燃（已燃返回 false）。</summary>
        bool TryIgnite();

        /// <summary>熄灭（未燃返回 false）。</summary>
        bool TryExtinguish();

        /// <summary>添柴（满或未燃返回 false）。</summary>
        bool TryAddFuel();
    }
}
