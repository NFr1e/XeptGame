namespace XeptGame.Items
{
    /// <summary>
    /// 物品要素（Facet）接口（[SerializeReference] 多态容器；ItemLoop_Design.md §3.2）——
    /// 作者在 Inspector 往 <c>ItemDefinition.Facets</c> 添加条目，"这物品是什么"的开放声明集。
    /// 一个物品的多个"面"（Facet）同列表共存，分两类：
    /// <list type="bullet">
    /// <item><b>分类面（纯语义，空条目）</b>：ResourceFacet/ConsumableFacet…，回答"玩家怎么归类它"，
    /// 供背包页签/排序/过滤等分类消费者经 <c>HasFacet{T}()</c> 查询；</item>
    /// <item><b>能力面（携带配置）</b>：条目引用该能力的配置资产（SO，可跨物品共享；HoldableFacet→HoldProfile、
    /// WorldFacet→WorldProfile…），回答"能干什么"——<b>条目存在 = 具备该能力</b>，
    /// 配置读取经 <c>GetFacet{T}()</c>（有能力语义 → 能力配置）；</item>
    /// </list>
    /// <list type="bullet">
    /// <item><b>存在性可靠</b>：[SerializeReference] 条目仅在作者添加后存在（无内嵌类自动实例化陷阱），
    /// 能力/语义一律以"条目存在"表达，不做空配置默认语义；</item>
    /// <item><b>命名稳定纪律</b>：序列化存类型名——Facet 类改名/移除会致既有资产数据丢失，视为稳定公共 API；</item>
    /// <item>分页/排序映射不归本层，属背包域 BackpackProfile（ItemLoop_Design.md §6，UI 阶段）。</item>
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
    /// 配置 = 对共享配置资产 <see cref="HoldProfile"/>（SO）的**引用**（Inspector 拖入、可跨物品复用）；
    /// profile 为 null = 能力已声明但配置未给（Object 引用判空可靠）。消费方经
    /// <c>GetFacet{HoldableFacet}()?.profile</c> 读取（有能力语义 → 能力配置）。
    /// </summary>
    [System.Serializable]
    public sealed class HoldableFacet : IItemFacet
    {
        /// <summary>握持配置资产引用（null = 未配置；见 <see cref="HoldProfile"/>）。</summary>
        public HoldProfile profile;
    }

    /// <summary>
    /// 世界可拾取能力面：条目存在 = 物品可在世界中以可拾取载体出现（WorldItem 接线阶段消费）。
    /// 配置 = 对共享配置资产 <see cref="WorldProfile"/>（SO）的引用（null = 未配置）；
    /// worldPrefab 空 = 无模板可实例化（Object 引用判空可靠）。
    /// 生成密度/权重属关卡生成器配置，不进本条目（ItemLoop_Design.md §3.3）。
    /// </summary>
    [System.Serializable]
    public sealed class WorldFacet : IItemFacet
    {
        /// <summary>世界载体配置资产引用（null = 未配置）。</summary>
        public WorldProfile profile;
    }
}
