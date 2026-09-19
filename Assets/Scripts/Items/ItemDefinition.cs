using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using XeptGame.Core;

namespace XeptGame.Items
{
    /// <summary>
    /// 物品定义（内容数据资产，ItemLoop_Design.md §3）——"这个东西是什么"的静态描述，不答运行时状态。
    /// <list type="bullet">
    /// <item><b>本体</b>：id / 2D 身份图标（Sprite 或 Texture 二选一，IconKind 切换）；displayNameKey
    /// 派生（id + ".name"），不存第二份字符串——Inspector 以只读栏展示，杜绝"名字 Key 在哪配置"的疑惑；</item>
    /// <item><b>要素（Facets）</b>：[SerializeReference] List&lt;IItemFacet&gt;——同一列表承载分类面
    /// （ResourceFacet/ConsumableFacet…，空条目）与<b>能力面</b>（HoldableFacet/WorldFacet…，携带配置子对象）。
    /// **条目存在 = 语义/能力存在**（SerializeReference 无自动实例化陷阱），配置读取经 <see cref="GetFacet{T}"/>；</item>
    /// <item><b>运行时只读纪律</b>：共享资产只读；写操作永不落在 Definition 上；</item>
    /// <item>资产引用即键（第一版无注册表）；id 为稳定键（将来注册表/存档索引）。</item>
    /// </list>
    /// **编辑体验纪律**：Odin 属性仅用于轻量展示/条件字段（[ShowIf]/[ReadOnly]），判断逻辑不进数据类；
    /// 编辑器复杂度上升时按字段/资产增量迁往 XeptGame.Editor（CustomEditor/PropertyDrawer），Odin 属性可逐条移除、
    /// 不碰序列化字段（ItemLoop_Design.md §3.4）。
    /// </summary>
    [CreateAssetMenu(
        menuName = XeptGameConsts.Editor.ItemDefinitionMenuName,
        fileName = XeptGameConsts.Editor.ItemDefinitionFileName,
        order = XeptGameConsts.Editor.ItemDefinitionOrder)]
    [Icon(XeptGameConsts.Editor.ItemDefinitionIconPath)]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField, PropertyOrder(0)] private string id;

        /// <summary>派生显示键：紧随 id 展示的只读栏（无逻辑，只读）。</summary>
        [ShowInInspector, ReadOnly, PropertyOrder(1)]
        public string DisplayNameKey => string.IsNullOrEmpty(id) ? string.Empty : id + ".name";

        [Space(20)]
        [SerializeField, PropertyOrder(2)] private IconKind iconKind;

        [ShowIf(nameof(iconKind), IconKind.Sprite), PreviewField, PropertyOrder(3)]
        [SerializeField] private Sprite iconSprite;

        [ShowIf(nameof(iconKind), IconKind.Texture), PreviewField, PropertyOrder(3)]
        [SerializeField] private Texture iconTexture;

        [Space(20)]
        [SerializeField, SerializeReference, PropertyOrder(4)] private List<IItemFacet> facets = new();

        public string Id => id;
        public IconKind IconKind => iconKind;
        public Sprite IconSprite => iconKind == IconKind.Sprite ? iconSprite : null;
        public Texture IconTexture => iconKind == IconKind.Texture ? iconTexture : null;
        public IReadOnlyList<IItemFacet> Facets => facets;

        /// <summary>
        /// 语义查询收口：是否存在指定类型要素（内部 is 模式匹配；空/失效条目安全跳过）。
        /// 分类判断（分类面）用本查询，消费方不得自行遍历 Facets 做 is。
        /// </summary>
        public bool HasFacet<T>() where T : IItemFacet
        {
            for (int i = 0; i < facets.Count; i++)
            {
                if (facets[i] is T)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 能力读取收口：取指定类型的要素实例（含其配置子对象），无则返回 null。
        /// 能力消费方一律经本查询（如 <c>GetFacet{HoldableFacet}()?.profile</c>），禁止自行遍历。
        /// </summary>
        public T GetFacet<T>() where T : class, IItemFacet
        {
            for (int i = 0; i < facets.Count; i++)
            {
                if (facets[i] is T instance)
                {
                    return instance;
                }
            }

            return null;
        }

        public bool TryGetFacet<T>(out T facet) where T : class, IItemFacet
        {
            facet = GetFacet<T>();
            return facet != null;
        }
    }

    /// <summary>
    /// 2D 身份（icon）的表示类型：显示消费方可能用 Sprite（uGUI Image）或 Texture 基类
    /// （Texture2D/RenderTexture，如 RawImage / 世界贴片）。Editor 层（Odin [ShowIf]）按类型
    /// 条件显示对应字段（ItemDefinition.iconSprite / iconTexture）。
    /// </summary>
    public enum IconKind
    {
        /// <summary>无图标（运行时用占位）。</summary>
        None = 0,

        /// <summary>Sprite 图标（uGUI Image 等）。</summary>
        Sprite = 1,

        /// <summary>纹理图标（Texture2D / RenderTexture 均可，经 Texture 基类承接；RawImage / 贴片等）。</summary>
        Texture = 2,
    }
}
