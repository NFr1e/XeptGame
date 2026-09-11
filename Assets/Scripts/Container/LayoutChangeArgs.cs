namespace XeptGame.Container
{
    /// <summary>
    /// 结构性变更负载（槽位轨的批量档，SlotStore_Design.md §6）：
    /// 整理（压缩）/扩容/缩容这类"整体布局变了"的操作发一条，界面据此<b>重建视图</b>；
    /// 单格移动/交换/拆分仍走细粒度 <see cref="SlotChangeArgs"/>。
    /// </summary>
    public readonly struct LayoutChangeArgs
    {
        /// <summary>变更后的格数（扩容/缩容后的容量）。</summary>
        public readonly int Capacity;

        public LayoutChangeArgs(int capacity) => Capacity = capacity;
    }
}
