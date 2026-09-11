using Sirenix.OdinInspector;

namespace XeptGame.Items
{
    /// <summary>
    /// 物品要素（Facet）接口（[SerializeReference] 多态容器；ItemLoop_Design.md §3.2）——
    /// 作者在 Inspector 往 <c>ItemDefinition.Facets</c> 添加条目，"这物品是什么"的开放声明集。
    /// 一个物品的多个"面"（Facet）同列表共存，分两类：
    /// <list type="bullet">
    /// <item><b>分类面（纯语义，空条目）</b>：ResourceFacet/ConsumableFacet…，回答"这东西是什么性质"
    /// （可加工/可食用…），消费者是未来的战斗/合成/建造域——**不承担背包归类**（角色收窄，
    /// 见 Inventory_Facet_Design.md §4）；</item>
    /// <item><b>能力面（携带配置）</b>：条目引用该能力的配置资产（SO，可跨物品共享；HoldableFacet→HoldableFacetProfile、
    /// WorldFacet→WorldFacetProfile、InventoryItemFacet→InventoryItemFacetProfile…），回答"能干什么"——<b>条目存在 = 具备该能力</b>，
    /// 配置读取经 <c>GetFacet{T}()</c>（有能力语义 → 能力配置）；</item>
    /// </list>
    /// <list type="bullet">
    /// <item><b>存在性可靠</b>：[SerializeReference] 条目仅在作者添加后存在（无内嵌类自动实例化陷阱），
    /// 能力/语义一律以"条目存在"表达，不做空配置默认语义；</item>
    /// <item><b>命名稳定纪律</b>：序列化存类型名——Facet 类改名/移除会致既有资产数据丢失，视为稳定公共 API；</item>
    /// <item>背包归类/分页/排序不归本层：归类唯一标准 = <see cref="InventoryItemFacetProfile.category"/>，
    /// 页签与排序策略归背包侧配置（Inventory_Facet_Design.md §4/§6）。</item>
    /// </list>
    /// 序列化可行性先例：`HudBillboard.cs` 的 [SerializeReference] IHudBillboardClamp 策略缝（Odin 可用）。
    /// </summary>
    public interface IItemFacet
    {
    }

    /// <summary>
    /// 资源分类面（纯语义/分类）：被加工/合成/建造的**输入**（石头/木头/金属）。
    /// 判别：拿它去干什么？——喂给合成/建筑 = 资源；吃/喝/用掉立刻得效果 = 消耗品。
    /// 与 ConsumableFacet 可并存（生肉既可加工也可直接吃），作者按需多挂，不做单桶仲裁。
    /// </summary>
    [System.Serializable]
    public sealed class ResourceFacet : IItemFacet
    {
    }

    /// <summary>
    /// 消耗品分类面（纯语义/分类，可携带使用效果配置）：**直接使用即生效**的一次性效果载体（食物/体力药水）。
    /// 判别：使用动作本身赋予回复/效果 = 消耗品；作为原料喂给合成/建筑 = 资源（ResourceFacet）。
    /// </summary>
    [System.Serializable]
    public sealed class ConsumableFacet : IItemFacet
    {
    }

    /// <summary>
    /// 可持有能力面：条目存在 = 物品可被玩家持有（Equip + FPV 阶段消费）。
    /// 配置 = 对共享配置资产 <see cref="HoldableFacetProfile"/>（SO）的**引用**（Inspector 拖入、可跨物品复用）；
    /// profile 为 null = 能力已声明但配置未给（Object 引用判空可靠）。消费方经
    /// <c>GetFacet{HoldableFacet}()?.profile</c> 读取（有能力语义 → 能力配置）。
    /// </summary>
    [System.Serializable]
    public sealed class HoldableFacet : IItemFacet
    {
        /// <summary>握持配置资产引用（null = 未配置；见 <see cref="HoldableFacetProfile"/>）。</summary>
        [InlineEditor] public HoldableFacetProfile profile;
    }

    /// <summary>
    /// 世界可拾取能力面：条目存在 = 物品可在世界中以可拾取载体出现（WorldItem 接线阶段消费）。
    /// 配置 = 对共享配置资产 <see cref="WorldFacetProfile"/>（SO）的引用（null = 未配置）；
    /// worldPrefab 空 = 无模板可实例化（Object 引用判空可靠）。
    /// 生成密度/权重属关卡生成器配置，不进本条目（ItemLoop_Design.md §3.3）。
    /// </summary>
    [System.Serializable]
    public sealed class WorldFacet : IItemFacet
    {
        /// <summary>世界载体配置资产引用（null = 未配置）。</summary>
        [InlineEditor] public WorldFacetProfile profile;
    }

    /// <summary>
    /// 背包能力面：条目存在 = 该物品**参与背包系统**（有背包归类与每格上限声明，Inventory_Facet_Design.md §2）。
    /// 配置 = 对共享配置资产 <see cref="InventoryItemFacetProfile"/>（SO）的引用（可跨物品复用，如"资源·上限 20"）；
    /// profile 为 null = 能力已声明但配置未给（Object 引用判空可靠）→ 消费方按"未分类 + 不约束上限"处理并记诊断。
    /// 物品完全未挂本条目同义（兼容既有资产，零迁移）；参数一律经 <c>GetFacet{InventoryItemFacet}()?.profile</c> 读取。
    /// </summary>
    [System.Serializable]
    public sealed class InventoryItemFacet : IItemFacet
    {
        /// <summary>背包面配置资产引用（null = 未配置；见 <see cref="InventoryItemFacetProfile"/>）。</summary>
        [InlineEditor] public InventoryItemFacetProfile profile;
    }

    /// <summary>
    /// 容器能力面：条目存在 = <b>该物品自带一个容器</b>（背包类物品；Item_Instance_Design.md §3）。
    /// 配置 = 对共享配置资产 <see cref="ContainerFacetProfile"/>（SO）的引用；
    /// profile 为 null = 能力已声明但配置未给 → 容器实例按常量默认格数装配并记诊断。
    /// <b>与 <see cref="InventoryItemFacet"/> 的区别</b>：那条答"作为背包条目怎么归类、每格上限多少"，
    /// 本条目答"这个物品自己能不能装东西"——两者可并存（背包既是背包条目，也是容器）。
    /// 消费方：实例工厂按本条目决定"造堆叠物还是造容器实例"，背槽按本条目做种类门控。
    /// </summary>
    [System.Serializable]
    public sealed class ContainerFacet : IItemFacet
    {
        /// <summary>容器配置资产引用（null = 未配置；见 <see cref="ContainerFacetProfile"/>）。</summary>
        [InlineEditor] public ContainerFacetProfile profile;
    }

    /// <summary>
    /// 容器扩容能力面：条目存在 = 该物品**能扩大容器容量**（装备/使用时生效，SlotStore_Design.md §6）。
    /// 配置 = 对共享配置资产 <see cref="ContainerCapacityExpanderProfile"/>（SO）的引用（可跨物品复用）；
    /// profile 为 null = 能力已声明但配置未给 → 消费方按"不加格"处理并记诊断。
    /// 容量来源键取**所在槽位**（不用物品身份：聚合计数无法区分两件同名扩容物）。
    /// </summary>
    [System.Serializable]
    public sealed class ContainerCapacityExpanderFacet : IItemFacet
    {
        /// <summary>扩容配置资产引用（null = 未配置；见 <see cref="ContainerCapacityExpanderProfile"/>）。</summary>
        [InlineEditor] public ContainerCapacityExpanderProfile profile;
    }
}
