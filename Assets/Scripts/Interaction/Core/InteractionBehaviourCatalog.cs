using System.Collections.Generic;
using XeptGame.Interaction.Behaviours;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互行为**总名单**（v5，Interaction_Behaviour_Design.md §2 D2）：所有行为在这里登记一次，
    /// 宿主不再各自 <c>new</c> 具体动作类，而是遍历本名单按 <see cref="IInteractionBehaviour.CanBuildOn"/>
    /// 筛出自己这一份清单。
    /// <list type="bullet">
    /// <item><b>为什么要有</b>：让"一共有哪些行为"成为可遍历的事实，从而能写测试保证
    /// ①每个行为的文案键在 CSV 里存在、②每个行为类都被登记（见 InteractionBehaviourCatalogTests）；</item>
    /// <item><b>成员是无状态实例</b>：行为不持目标与状态，可安全共享；目标由运行时动作
    /// <see cref="BoundAction"/> 携带；</item>
    /// <item><b>新增行为 = 新增一个类 + 这里登记一行</b>；漏登记会被测试抓住。</item>
    /// </list>
    /// </summary>
    public static class InteractionBehaviourCatalog
    {
        /// <summary>全部行为（顺序 = 清单候选顺序；同槽互斥由执行器保证，仅取首个可用）。</summary>
        public static readonly IReadOnlyList<IInteractionBehaviour> All = new IInteractionBehaviour[]
        {
            #region Equip & Inventory
            new PickupBehaviour(),        // 拾取    Primary    interaction.pickup
            new EquipBehaviour(),         // 装备    Hold       interaction.equip
            new WearBehaviour(),          // 背上    Hold       interaction.wear
            #endregion

            #region Props

            #region Campfire
            new IgniteBehaviour(),        // 点燃    Primary    interaction.ignite
            new ExtinguishBehaviour(),    // 熄灭    Primary    interaction.extinguish
            new AddFuelBehaviour(),       // 添柴    Secondary  interaction.addfuel
            #endregion

            #endregion

            new DebugBehaviour(),         // 交互    Primary    interaction.debug
        };
    }
}
