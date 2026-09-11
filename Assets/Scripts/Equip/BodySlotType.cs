namespace XeptGame.Equip
{
    /// <summary>
    /// 身体槽位标识（一等值，Equip_FPV_Design.md §2.2/§3.2）——v1 唯一成员 = 手部槽；
    /// 甲/背 ⏳ 追加成员 = 扩展（纯新增，消费方合同零改动）。
    /// <b>本枚举是身体槽的身份词表</b>：其值即通用槽身份 <c>XeptGame.Container.SlotId</c>；
    /// 槽的"能放什么/能放多少"由对应槽实现（<c>SlotBase</c> 子类）的接纳谓词与每格上限回答。
    /// </summary>
    public enum BodySlotType
    {
        /// <summary>手部槽：单位制（无视可堆叠、容量恒 1 单位），接纳可持物（武器/工具/可堆叠资源）。</summary>
        Hand = 0,
    }
}
