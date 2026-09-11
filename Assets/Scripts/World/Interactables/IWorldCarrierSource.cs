using XeptGame.Inv;

namespace XeptGame.World
{
    /// <summary>
    /// 携带<b>有状态实例</b>的世界源（背包等；Item_Instance_Design.md §6）：
    /// 它的内容不是"一堆同定义物品"，而是<b>那一个</b>实例——所以按定义的写入对它无效
    /// （实例行纪律，T1），取放只能<b>整体</b>进行。
    /// </summary>
    public interface IWorldCarrierSource : IWorldSource
    {
        /// <summary>当前携带的实例（空 = null）。</summary>
        ContainerInstance Carrier { get; }

        /// <summary>整体取出（原子）：成功后本源不再持有该实例（"一个实例一个位置"，不变量 I1）。</summary>
        bool TryTakeCarrier(out ContainerInstance carrier);

        /// <summary>整体放回（仅用于取出后的回滚）：本源当前为空即成功——回滚按<b>对象</b>语义，不按定义重新匹配。</summary>
        bool TryReturnCarrier(ContainerInstance carrier);
    }
}
