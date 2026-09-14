namespace XeptGame.Items
{
    /// <summary>
    /// 拾取一次性播报负载（纯数据；WorldItem_Design.md W5 事件轨）：
    /// "获得了 X × N"是一次性事件而非状态——状态由 Inventory.Changed 细粒度推送（状态轨），
    /// 本事件走 Gameplay 域 EventBus（GameManager.Context.EventBus），供拾取提示浮字等消费方订阅
    /// （背包 UI 决议阶段落地订阅者；当前无订阅者无害，为播报两轨立好骨架）。
    /// </summary>
    public readonly struct ItemAcquiredEvent
    {
        /// <summary>被拾取的物品定义。</summary>
        public readonly ItemDefinition Item;

        /// <summary>拾取数量。</summary>
        public readonly int Count;

        public ItemAcquiredEvent(ItemDefinition item, int count)
        {
            Item = item;
            Count = count;
        }
    }

    /// <summary>
    /// 使用（消耗）一次性播报负载（Backpack_UI_Design.md B2）：与 <see cref="ItemAcquiredEvent"/> 对称的
    /// "一次性事件而非状态"——扣减带来的状态变化由容器聚合轨（<c>SlotContainer.Changed</c>）推送。
    /// <b>效果域后置</b>：v1 只播报"用掉了一个"，"吃了回血"由将来的战斗/属性域消费本事件实现。
    /// </summary>
    public readonly struct ItemConsumedEvent
    {
        /// <summary>被使用的物品定义。</summary>
        public readonly ItemDefinition Item;

        /// <summary>使用数量（v1 恒 1）。</summary>
        public readonly int Count;

        public ItemConsumedEvent(ItemDefinition item, int count)
        {
            Item = item;
            Count = count;
        }
    }
}
