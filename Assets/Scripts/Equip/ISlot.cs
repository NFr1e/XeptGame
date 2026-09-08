using XeptGame.Items;

namespace XeptGame.Equip
{
    /// <summary>
    /// 身体槽行为缝（Equip_FPV_Design.md §2.2/§3.2，T2）——"什么能进这个槽"是**策略**：代码化、以
    /// 物品能力面（Facet）为判别（序列化即退回按类型名注册的老路，违背行为不配置化纪律）。
    /// <list type="bullet">
    /// <item>ISlot 只覆盖<b>身体槽</b>（不压平背包格/箱格——那是容器端口层 IItemContainer 的事）；</item>
    /// <item>接口面最小：身份 + 接纳；<b>不持占用状态</b>——占用归 <see cref="Equipment"/> 管理
    /// （命令层唯一写者）；</item>
    /// <item>加甲/背槽 = 新增 <see cref="BodySlotType"/> 成员 + 新 ISlot 实现类 + 装配条目（纯新增）。</item>
    /// </list>
    /// </summary>
    public interface ISlot
    {
        /// <summary>槽位身份（BodySlot 一等值）。</summary>
        BodySlotType Id { get; }

        /// <summary>该槽是否接纳此物品（null 恒 false；判定面向物品能力面，内容开放）。</summary>
        bool Accepts(ItemDefinition definition);
    }
}
