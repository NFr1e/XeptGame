using System.Collections.Generic;
using UnityEngine;
using XeptGame.Game.Flow;
using XeptGame.Interaction;
using XeptGame.Interaction.Behaviours;
using XeptGame.Items;
using XeptGame.Items.Operations;
using XeptGame.World;
using XeptKit.Core;

namespace XeptGame.World.Interactables
{
    /// <summary>
    /// 世界源的场景壳（视图）：只认 <see cref="IWorldSource"/>（数据面），不自行判断归属。
    /// <list type="bullet">
    /// <item><b>场景直摆</b>：<c>Awake</c> 按序列化定义构建<b>无状态堆叠源</b>（容器类物品的实例源需要会话工厂铸实例，
    /// 由记录层/运行期经 <see cref="Initialize"/> 注入，见 Item_Instance_Design.md §6）；</item>
    /// <item><b>运行期生成</b>：掉落物视图由记录层生成后用 <see cref="Initialize"/> 注入源；</item>
    /// <item><b>视图不删记录</b>（不变量 I3）：本类只做场景表现与交互动作，记录增删一律归记录层。</item>
    /// </list>
    /// <b>v5（Interaction_Behaviour_Design.md）追加</b>：
    /// <list type="bullet">
    /// <item><b>它是"可交互目标"</b>（<see cref="IPickupTarget"/>）：向行为暴露"能不能拾取 + 是不是可持物/容器"，
    /// 物品域的具体做法仍在本类里（<see cref="RequestPickup"/>），交互核心契约不认识物品；</item>
    /// <item><b>清单从总名单筛出来</b>：不再手写 <c>_actions.Add</c>，而是遍历
    /// <see cref="InteractionBehaviourCatalog"/>，按"能力面 + 携带事实"决定成员（无包时"拾取"不进清单）；</item>
    /// <item><b>成员会变</b>：因此实现 <see cref="IInteractionActionsNotifier"/>（背上/放下背包时通知执行器重建）。</item>
    /// </list>
    /// </summary>
    public sealed class WorldItem : MonoBehaviour,
        ISelectable, IInteractionActionsHost, IInteractionActionsNotifier, IInteractionInfoContext, IPickupTarget
    {
        [SerializeField] private ItemDefinition definition;
        [SerializeField] private int pickupCount = 1;

        [Tooltip("视图根：承载可见网格与可检测碰撞体、随「内容取空」整体隐藏的那个物体。" +
                 "留空 = 本物体（兼容既有摆件与 prefab）。组件可以挂在视图之内（隐藏时一并失活）。")]
        [SerializeField] private GameObject view;

        private readonly List<IInteractionAction> _actions = new();

        private IWorldSource _source;
        private ICarryFacts _carry;

        /// <inheritdoc />
        public event System.Action ActionsChanged;

        public ItemDefinition Definition => _source?.DisplayDefinition ?? definition;

        public bool IsSelected { get; private set; }

        public IReadOnlyList<IInteractionAction> Actions => _actions;

        public string DisplayNameKey => Definition != null ? Definition.DisplayNameKey : string.Empty;

        public string DisplayName => string.Empty;

        public Sprite IconSprite => Definition != null ? Definition.IconSprite : null;

        public Texture IconTexture => Definition != null ? Definition.IconTexture : null;

        // ---- IPickupTarget（行为据此决定"清单里有没有这件事"）----

        /// <inheritdoc />
        public bool CanPickup => _source != null && _source.Available && !_source.IsBusy && _source.HasContent;

        /// <inheritdoc />
        public bool IsHoldable => Definition != null && Definition.HasFacet<HoldableFacet>();

        /// <inheritdoc />
        public bool IsCarrier => Definition != null && Definition.HasFacet<ContainerFacet>();

        public int Remaining => _source != null && _source is WorldStackSource stack ? stack.Remaining : pickupCount;

        // ---- 视图（W8：视图根显式化；见 WorldItem_Design.md §7）----

        /// <summary>
        /// 视图根：随"内容取空"整体隐藏的那个物体（承载可见网格与**可检测碰撞体**）。
        /// 未配置时回退本物体——既有摆件/prefab 行为不变。
        /// </summary>
        public GameObject View => view != null ? view : gameObject;

        /// <summary>
        /// 视图是否"还在世上"（数据面可用性判据）：组件自身启用 **且** 视图激活。
        /// 视图内含组件时（组件挂在视图之内的子物体上），隐藏视图 → 组件失活 → 本判据自然为假。
        /// </summary>
        public bool IsViewAvailable => this != null && isActiveAndEnabled && View.activeInHierarchy;

        /// <summary>
        /// 配置自检（一次性，仅当**显式配置了**视图根时查）：视图根下没有任何碰撞体 →
        /// 隐藏后仍可能被探测器命中（"看不见但能瞄"）→ 记一次告警。留空回退自身时跳过（无从改进）。
        /// </summary>
        private void ValidateView()
        {
            if (view == null || view == gameObject)
            {
                return;
            }

            if (view.GetComponentInChildren<Collider>(true) == null)
            {
                Log.Warning($"[WorldItem] {name} 的视图根「{view.name}」下没有碰撞体：" +
                            "内容取空后隐藏它，物体仍可能被探测器命中（建议把可检测碰撞体放进视图根下）。");
            }
        }

        private void Awake()
        {
            ValidateView();

            if (_source == null)
            {
                _source = new WorldStackSource(
                    definition, Mathf.Max(0, pickupCount), () => IsViewAvailable, RefreshView);
            }
        }

        private void OnEnable()
        {
            // 会话可能晚于基座场景建立：订阅"会话已建"铃后再挂钩携带事实（不轮询）
            GameplaySessionEntry.Created += OnSessionCreated;
            HookCarry();
            RebuildActions();
        }

        private void OnDisable()
        {
            GameplaySessionEntry.Created -= OnSessionCreated;
            UnhookCarry();
        }

        /// <summary>运行期注入数据面（记录层生成视图时调用；场景直摆时不调用）。</summary>
        public void Initialize(IWorldSource source)
        {
            if (source != null)
            {
                _source = source;
            }

            RebuildActions();
        }

        /// <summary>
        /// 注入携带事实（装配/测试接缝）：注入假实现即可覆盖"有包/无包 × 各类物品"的组合，不需要真会话。
        /// 传 null = 视为无包。
        /// </summary>
        public void SetCarryFacts(ICarryFacts facts)
        {
            UnhookCarry();
            _carry = facts;
            if (_carry != null)
            {
                _carry.Changed += OnCarryChanged;
            }

            RebuildActions();
        }

        /// <summary>
        /// 按当前能力面与携带事实重建清单（成员 = 行为；可用与否仍由执行器/HUD 每帧查 <c>CanAct</c>）。
        /// 与上次成员相同则**不发通知**（幂等；避免无意义重建）。
        /// </summary>
        private void RebuildActions()
        {
            var carry = _carry;
            var next = new List<BoundAction>(InteractionBehaviourCatalog.All.Count);
            for (int i = 0; i < InteractionBehaviourCatalog.All.Count; i++)
            {
                var behaviour = InteractionBehaviourCatalog.All[i];
                if (behaviour.CanBuildOn(this, carry))
                {
                    next.Add(new BoundAction(behaviour, this));
                }
            }

            if (!MembersChanged(next))
            {
                return;
            }

            _actions.Clear();
            for (int i = 0; i < next.Count; i++)
            {
                _actions.Add(next[i]);
            }

            ActionsChanged?.Invoke();
        }

        /// <summary>成员是否与当前清单不同（按行为身份比较，不比较动作对象本身）。</summary>
        private bool MembersChanged(List<BoundAction> next)
        {
            if (next.Count != _actions.Count)
            {
                return true;
            }

            for (int i = 0; i < next.Count; i++)
            {
                var current = _actions[i] as BoundAction;
                if (current == null || !ReferenceEquals(next[i].Behaviour, current.Behaviour))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnSessionCreated()
        {
            HookCarry();
            RebuildActions();
        }

        private void OnCarryChanged() => RebuildActions();

        /// <summary>挂钩会话携带事实（同一实例不重复挂；会话未就绪则保持 null = 无包）。</summary>
        private void HookCarry()
        {
            var next = GameplaySessionEntry.TryGetInstance(out var entry) ? entry.Context : null;
            if (ReferenceEquals(next, _carry))
            {
                return;
            }

            UnhookCarry();
            _carry = next;
            if (_carry != null)
            {
                _carry.Changed += OnCarryChanged;
            }
        }

        private void UnhookCarry()
        {
            if (_carry == null)
            {
                return;
            }

            _carry.Changed -= OnCarryChanged;
            _carry = null;
        }

        /// <summary>
        /// 内容取空 → 隐藏**视图根**（数据面回调入口；不 Destroy，便于复位/池化）。
        /// 视图内含本组件时，隐藏后组件随之失活——"看不见 = 瞄不到 = 不再被选中"三合一，无需额外逻辑；
        /// 但**不能再指望本组件把视图显示回来**（复位/池化归世界生成系统）。
        /// </summary>
        public void RefreshView()
        {
            if (this != null && _source != null && !_source.HasContent)
            {
                View.SetActive(false);
            }
        }

        /// <inheritdoc />
        public OperationReceipt RequestPickup(PickupIntent intent)
        {
            if (!CanPickup || !GameplaySessionEntry.TryGetInstance(out var entry))
            {
                return null;
            }

            var item = Definition;
            if (item == null)
            {
                return null;
            }

            // 容器类物品 = 有状态实例：它的"一个"是这个背包本身 → 走实例路由（整体背上背槽）。
            // 实例需要会话工厂铸（签发 id + 按 facet 装配容器），故此处惰性铸造并缓存到 _source。
            var carrierSource = _source as IWorldCarrierSource ?? TryMintCarrierSource(entry);
            if (carrierSource != null)
            {
                return entry.Context.Operations.RequestPickupCarrier(carrierSource, intent);
            }

            return entry.Context.Operations.RequestPickup(_source, item, PickupCount(), intent, entry.Context.Inventory);
        }

        /// <summary>容器类物品惰性铸实例源（会话未就绪时返回 null，调用方按"不可拾取"处理）。</summary>
        private IWorldCarrierSource TryMintCarrierSource(GameplaySessionEntry entry)
        {
            if (definition == null || !definition.HasFacet<ContainerFacet>())
            {
                return null;
            }

            var carrier = entry.Context.Instances.CreateContainer(definition);
            var source = new WorldInstanceSource(
                carrier, 0, () => IsViewAvailable, RefreshView);
            _source = source;
            return source;
        }

        /// <summary>本次请求要搬多少：堆叠源 = 剩余全部；实例源（背包）= 1（数量恒 1）。</summary>
        private int PickupCount()
            => _source is WorldStackSource stack ? stack.Remaining : 1;

        public bool CanSelect(InteractionContext context) => Definition != null && CanPickup;

        public void OnSelected(InteractionContext context)
        {
            IsSelected = true;
        }

        public void OnDeselected()
        {
            IsSelected = false;
        }
    }
}
