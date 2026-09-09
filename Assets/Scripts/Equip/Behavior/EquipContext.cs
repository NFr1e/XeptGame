using XeptGame.Items;

namespace XeptGame.Equip
{
    /// <summary>
    /// 装备 FSM 的会话数据载体。状态类不保存跨进入的可变字段；
    /// Item/Version 是编排器同步的占用快照，不在这里执行容器写入。
    /// </summary>
    internal sealed class EquipContext
    {
        /// <summary>当前槽位的只读物品引用。</summary>
        public ItemDefinition Item;

        /// <summary>每次实际转入/转出递增；回滚恢复原占用时不递增。</summary>
        public long Version;

        /// <summary>每次新行为递增，同物品的两次拿出也具有不同身份。</summary>
        public long ActionId;

        /// <summary>当前动作累计时间 / 目标时长（由 Drawing/Stowing 状态 Update 推进；相位唯一权威在 Fsm，见 EquipController）。</summary>
        public float Elapsed;

        public float Duration;

        /// <summary>暂停标志：为 true 时冻结时钟、拒绝新请求与转出；不清空占用与进度（由组合根门控设置）。</summary>
        public bool Paused;

        /// <summary>当前占用物的拿放时长（随 <c>ReconcileOccupancy</c> 由编排携带；默认兜底，未占用时无意义）。</summary>
        public EquipTiming Timing = EquipTiming.Default;
    }
}
