using System;
using System.Collections.Generic;
using XeptGame.Container;
using XeptGame.Items;

namespace XeptGame.Equip
{
    /// <summary>
    /// 身体容器（运行时 = 配置驱动的槽占用集，Equip_FPV_Design.md §2.2/§3.2；SlotStore_Design.md §3）：
    /// 构造时注入 <see cref="SlotBase"/> 列表（槽配置），容器只做<b>管理</b>——寻址、分配、聚合、事件转发；
    /// <b>占用事实归各槽</b>（不再自持占用字典，消除"第二份占用表"）。
    /// <list type="bullet">
    /// <item><b>槽 = 一等对象</b>：<see cref="BodySlotType"/> 是身体槽身份词表，其值即 <see cref="SlotId"/>；
    /// 事件双轨——槽级（按槽键控、含数量）由槽发布后本容器原样转发，容器级聚合轨在提交点发布；</item>
    /// <item><b>单位制不是容器特例</b>：由手槽自己的门控表达（<c>CapacityFor = 1</c>）——加槽 = 加一个槽对象；</item>
    /// <item><b>实现 <see cref="IItemContainer"/></b>（单位语义容器面）——让 <see cref="ContainerTransfer.Move"/> 哑原语真实可用
    /// （收起 = Move(body → bag) 等）；</item>
    /// <item>占用变更仅经容器这一唯一公开写面（槽自身写口为 internal），命令层纪律不变。</item>
    /// </list>
    /// </summary>
    public sealed class Equipment : SlotContainer, IItemContainer
    {
        private readonly Dictionary<BodySlotType, SlotBase> _byType = new();

        public Equipment(IReadOnlyList<SlotBase> slots) : base(slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!Enum.IsDefined(typeof(BodySlotType), slot.Id.Value))
                {
                    throw new ArgumentException($"身体容器收到未登记的身体槽身份：{slot.Id}（须为 BodySlotType 成员）。");
                }

                _byType[(BodySlotType)slot.Id.Value] = slot;
            }
        }

        /// <summary>读取某槽当前占用（null = 槽空；槽未配置返回 null）。</summary>
        public ItemDefinition Get(BodySlotType slot)
            => _byType.TryGetValue(slot, out var target) ? target.Item : null;

        /// <summary>
        /// 读取某槽持有的<b>实例</b>（null = 槽空 / 无状态占用 / 槽未配置）。
        /// 当前背包推导用：背槽 → <c>ContainerInstance</c>（Item_Instance_Design.md §3）。
        /// </summary>
        public ItemInstance GetInstance(BodySlotType slot)
            => _byType.TryGetValue(slot, out var target) ? target.Instance : null;

        /// <summary>槽是否为空。</summary>
        public bool IsEmpty(BodySlotType slot) => Get(slot) == null;

        /// <summary>槽的接纳查询（委托给该槽的门控；供命令层做"可持/可入槽"守卫）。</summary>
        public bool SlotAccepts(BodySlotType slot, ItemDefinition definition)
            => _byType.TryGetValue(slot, out var target) && target.Accepts(definition);
    }
}
