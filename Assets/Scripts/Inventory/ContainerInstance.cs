using XeptGame.Items;
using XeptKit.Core;

namespace XeptGame.Inv
{
    /// <summary>
    /// 背包实例（"背包即物品"，Item_Instance_Design.md §3）：<see cref="ItemInstance"/> 的容器类派生，
    /// <b>自己带一个容器</b>（<see cref="Inventory"/>）。
    /// <list type="bullet">
    /// <item><b>换包 = 换实例</b>：背槽里换成另一个实例即可，本实例的内容<b>永不迁移</b>；</item>
    /// <item><b>有状态 ⇒ 不可堆叠</b>（不变量 I2）：占一整格、数量恒 1；</item>
    /// <item><b>落位在背包域而非内容层</b>：本类引用的 <see cref="Inventory"/> 依赖 <c>XeptGame.Container</c>，
    /// 而容器层已依赖 <c>XeptGame.Items</c>——放进内容层会形成双向依赖，故与 <see cref="Inventory"/> 同域。</item>
    /// </list>
    /// 由 <see cref="ItemInstanceFactory"/> 创建（唯一创建点），不在别处 new。
    /// </summary>
    public sealed class ContainerInstance : ItemInstance
    {
        public ContainerInstance(long id, ItemDefinition definition, Inventory store) : base(id, definition)
        {
            Guard.NotNull(store, nameof(store));
            Store = store;
        }

        /// <summary>本背包自己的容器（格子、容量、聚合面与变更事件都在它身上）。</summary>
        public Inventory Store { get; }
    }
}
