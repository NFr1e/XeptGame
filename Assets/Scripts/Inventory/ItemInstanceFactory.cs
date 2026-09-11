using System;
using XeptGame.Core;
using XeptGame.Items;
using XeptKit.Core;

namespace XeptGame.Inv
{
    /// <summary>
    /// 实例工厂（Item_Instance_Design.md §2.1/§3）：<b>唯一创建实例的地方</b>——签发 id、按 facet 装配容器、
    /// 注入溢出出口（对照 Rust 的 <c>ItemManager</c>："实例只能由管理器创建"）。
    /// <list type="bullet">
    /// <item>由会话持有（<c>GameplaySessionContext</c>），与实例 id 签发器同生命周期；</item>
    /// <item>溢出出口目前是 <c>Action&lt;ItemDefinition,int&gt;</c> 占位（⏳ T5 升级为携带实例句柄 → 整包落地）；</item>
    /// <item><b>只提供当前有消费者的创建口</b>（容器实例）——无状态物品不走实例（沿用按定义寻址），
    /// 其它状态面（耐久/弹药）等真实消费者出现时再生长。</item>
    /// </list>
    /// </summary>
    public sealed class ItemInstanceFactory
    {
        private readonly InstanceIdAllocator _ids;
        private readonly Action<ItemDefinition, int> _discardSink;

        public ItemInstanceFactory(InstanceIdAllocator ids, Action<ItemDefinition, int> discardSink = null)
        {
            Guard.NotNull(ids, nameof(ids));
            _ids = ids;
            _discardSink = discardSink;
        }

        /// <summary>下一个将签发的实例号（存档层读取用；不消耗）。</summary>
        public long NextInstanceId => _ids.Next;

        /// <summary>
        /// 创建一个背包（容器）实例：容量基准取 <c>ContainerFacet.profile.ResolvedBaseSlots</c>；
        /// 未挂面 = 编程错误（调用方必须先判面）；挂了面但未配置资产 = 用常量默认格数并记诊断。
        /// </summary>
        public ContainerInstance CreateContainer(ItemDefinition definition)
        {
            Guard.NotNullObject(definition, nameof(definition));

            var facet = definition.GetFacet<ContainerFacet>();
            if (facet == null)
            {
                throw new InvalidOperationException(
                    "该物品未声明容器能力面（ContainerFacet），不能创建容器实例：" + definition.Id);
            }

            var profile = facet.profile;
            if (profile == null)
            {
                Log.Info($"[ItemInstanceFactory] {definition.Id} 的容器面未配置 ContainerFacetProfile → 用常量默认格数（{XeptGameConsts.Inventory.DefaultCapacity}）。");
            }

            var baseSlots = profile != null ? profile.ResolvedBaseSlots : XeptGameConsts.Inventory.DefaultCapacity;
            return new ContainerInstance(_ids.Allocate(), definition, new Inventory(baseSlots, _discardSink));
        }
    }
}
