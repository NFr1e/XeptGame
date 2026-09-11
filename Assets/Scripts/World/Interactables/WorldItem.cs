using System.Collections.Generic;
using UnityEngine;
using XeptGame.Game.Flow;
using XeptGame.Interaction;
using XeptGame.Items;
using XeptGame.Items.Operations;
using XeptGame.World;

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
    /// </summary>
    public sealed class WorldItem : MonoBehaviour, ISelectable, IInteractionActionsHost, IInteractionInfoContext
    {
        [SerializeField] private ItemDefinition definition;
        [SerializeField] private int pickupCount = 1;
        private readonly List<IInteractionAction> _actions = new();
        private IWorldSource _source;
        public ItemDefinition Definition => _source?.DisplayDefinition ?? definition;
        public bool IsSelected { get; private set; }
        public IReadOnlyList<IInteractionAction> Actions => _actions;
        public string DisplayNameKey => Definition != null ? Definition.DisplayNameKey : string.Empty;
        public string DisplayName => string.Empty;
        public Sprite IconSprite => Definition != null ? Definition.IconSprite : null;
        public Texture IconTexture => Definition != null ? Definition.IconTexture : null;
        public bool CanPickup => _source != null && _source.Available && !_source.IsBusy && _source.HasContent;
        public int Remaining => _source != null && _source is WorldStackSource stack ? stack.Remaining : pickupCount;

        private void Awake()
        {
            if (_source == null)
            {
                _source = new WorldStackSource(
                    definition, Mathf.Max(0, pickupCount), () => this != null && isActiveAndEnabled, RefreshView);
            }

            _actions.Add(new PickupAction(this, PickupIntent.Tap, InputSlot.Primary, "拾取"));
            _actions.Add(new PickupAction(this, PickupIntent.ForceHold, InputSlot.Hold, "拿取"));
        }

        /// <summary>运行期注入数据面（记录层生成视图时调用；场景直摆时不调用）。</summary>
        public void Initialize(IWorldSource source)
        {
            if (source != null)
            {
                _source = source;
            }
        }

        private void RefreshView()
        {
            if (this != null && _source != null && !_source.HasContent)
            {
                gameObject.SetActive(false);
            }
        }

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
                carrier, 0, () => this != null && isActiveAndEnabled, RefreshView);
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

        private sealed class PickupAction : IInteractionAction
        {
            private readonly WorldItem _host;
            private readonly PickupIntent _intent;
            public InputSlot Slot { get; }
            public string PromptText { get; }

            public PickupAction(WorldItem host, PickupIntent intent, InputSlot slot, string text)
            {
                _host = host;
                _intent = intent;
                Slot = slot;
                PromptText = text;
            }

            public bool CanInteract(InteractionContext context) => _host.CanPickup;
            public void Interact(InteractionContext context)
            {
                _host.RequestPickup(_intent);
            }
        }
    }
}
