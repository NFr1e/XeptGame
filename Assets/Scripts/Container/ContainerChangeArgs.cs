using XeptGame.Items;

namespace XeptGame.Container
{
    /// <summary>
    /// 容器级变更负载（聚合轨，SlotStore_Design.md §3）：单条已提交变更的完整语义，消费方免反向推导
    /// ——镜像 Interaction <c>SelectionChangeArgs</c> 先例（ItemLoop_Design.md §4.3）。
    /// <list type="bullet">
    /// <item>原名 <c>InventoryChangeArgs</c>：它本就是容器端口的通用负载，搬到机制层时一并正名；</item>
    /// <item>发布点唯一：容器在<b>提交点</b>按物品算完前后总数发一条（槽级细粒度变化走 <see cref="SlotChangeArgs"/>）；</item>
    /// <item>NewCount = 0 = 该物品在容器内归零（对列表容器即"行移除"）。</item>
    /// </list>
    /// </summary>
    public readonly struct ContainerChangeArgs
    {
        /// <summary>发生变更的物品。</summary>
        public readonly ItemDefinition Item;

        /// <summary>变化前数量（新增时为 0）。</summary>
        public readonly int OldCount;

        /// <summary>变化后数量（0 = 该物品已从容器清空）。</summary>
        public readonly int NewCount;

        public ContainerChangeArgs(ItemDefinition item, int oldCount, int newCount)
        {
            Item = item;
            OldCount = oldCount;
            NewCount = newCount;
        }
    }
}
