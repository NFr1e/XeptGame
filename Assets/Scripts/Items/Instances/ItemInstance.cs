using XeptKit.Core;

namespace XeptGame.Items
{
    /// <summary>
    /// 物品运行时实例（Item_Instance_Design.md §2.1）——<b>身份 + 定义</b>，仅此两项。
    /// <list type="bullet">
    /// <item><b>静态内容在 <see cref="ItemDefinition"/>、运行时状态在实例</b>（与 Unity Game Foundation 的
    /// Definition / GameItem 同构）：定义是编辑器只读 SO，实例承载"会变、且要跨会话持久化"的数据；</item>
    /// <item><b>状态字段不预置</b>（字段迟到优于早到）：耐久/弹药等按 facet 生长为派生实例（T2 起）；</item>
    /// <item><b>有状态 ⇒ 不可堆叠</b>（不变量 I2）：实例占一整格、数量恒 1、不参与合并——由槽载荷钉住；</item>
    /// <item><b>一个实例同一时刻只有一个位置</b>（不变量 I1）：实例只经容器口进出，没有"凭空添加一个已存在实例"的入口；</item>
    /// <item><b>id 用会话内单调 long</b>（DP7）：由 <see cref="InstanceIdAllocator"/> 签发，存档层持久化
    /// <c>NextInstanceId</c>；<b>0 预留为"无实例"哨兵</b>，故 id 必须为正；</item>
    /// <item><b>id 不进业务 API</b>（不变量 I5）：业务端口按定义或按格子寻址，id 只出现在存档与调试出口。</item>
    /// </list>
    /// </summary>
    public class ItemInstance
    {
        public ItemInstance(long id, ItemDefinition definition)
        {
            Guard.True(id > 0, "实例 id 必须为正（0 预留为无实例哨兵）。");
            Guard.NotNullObject(definition, nameof(definition));

            Id = id;
            Definition = definition;
        }

        /// <summary>会话内唯一且单调的身份——存档键的一半（另一半是定义的 id）。</summary>
        public long Id { get; }

        /// <summary>静态内容定义（SO，运行时只读；存档只写它的 id，定义缺失时可逆，见不变量 I6）。</summary>
        public ItemDefinition Definition { get; }

        public override string ToString() => "ItemInstance(" + Id + ":" + Definition.name + ")";
    }
}
