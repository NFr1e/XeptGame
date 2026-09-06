using System;
using System.Collections.Generic;
using UnityEngine;
using XeptGame.Game;
using XeptGame.Game.Flow;
using XeptGame.Interaction;
using XeptGame.Items;
using XeptKit.Core;

namespace XeptGame.World.Interactables
{
    /// <summary>
    /// 世界可拾取物（v3.1 宿主模型；WorldItem_Design.md）：场景直摆可拾取载体——
    /// 序列化持有 ItemDefinition，扮演**宿主（ISelectable + IInteractionActionsHost）**；
    /// 唯一动作 = 内部纯 C# <see cref="PickupAction"/>（Primary 拾取，构造注入宿主，无 Unity 生命周期纠缠）。
    /// 拾取逻辑收口在 <see cref="TryPickup"/>：加背包 → 发布 ItemAcquiredEvent（事件轨）→ 隐藏自身（防重复拾取、不销毁）。
    /// </summary>
    public sealed class WorldItem : MonoBehaviour, ISelectable, IInteractionActionsHost, IInteractionDisplayInfo
    {
        [Tooltip("对应的物品定义（WorldFacet 等条目数据源）；必填——缺失时不可交互")]
        [SerializeField] private ItemDefinition definition;

        [Tooltip("每次拾取数量（世界堆叠 per-instance，不进 Definition）")]
        [SerializeField] private int pickupCount = 1;

        private readonly List<IInteractionAction> _actions = new();
        private PickupAction _pickup;
        private bool _isSelected;

        /// <summary>绑定的物品定义。</summary>
        public ItemDefinition Definition => definition;

        /// <inheritdoc />
        public bool IsSelected => _isSelected;

        /// <inheritdoc />
        public IReadOnlyList<IInteractionAction> Actions => _actions;

        /// <summary>成员恒定（单动作），事件保留接口语义（不触发）。</summary>
        public event Action ActionsChanged;

        // ---- IInteractionDisplayInfo（提示头部：名字 = Definition 本地化键；图标 = Definition，IconKind 感知）----

        /// <inheritdoc />
        public string DisplayNameKey => definition != null ? definition.DisplayNameKey : string.Empty;

        /// <inheritdoc />
        public string DisplayName => string.Empty; // 无直显文案（走键解析，未注册即隐名）

        /// <inheritdoc />
        public Sprite IconSprite => definition != null ? definition.IconSprite : null;

        /// <inheritdoc />
        public Texture IconTexture => definition != null ? definition.IconTexture : null;

        private void Awake()
        {
            if (definition == null)
            {
                Log.Error($"[WorldItem] {name}: definition 未接线（应在 Inspector 指定 ItemDefinition），不可交互。");
            }

            _pickup = new PickupAction(this);
            _actions.Add(_pickup);
        }

        /// <summary>是否可拾取（动作门控数据源）。</summary>
        public bool CanPickup => definition != null && pickupCount > 0;

        /// <summary>执行拾取（动作 Interact 收口）：入包 → 事件轨播报 → 隐藏。</summary>
        public bool TryPickup()
        {
            if (definition == null)
            {
                Log.Error($"[WorldItem] {name}: definition 未接线，无法拾取。");
                return false;
            }

            if (pickupCount <= 0)
            {
                Log.Error($"[WorldItem] {name}: pickupCount 必须为正（当前 {pickupCount}）。");
                return false;
            }

            if (!GameplayEntry.TryGetInstance(out var entry))
            {
                Log.Error("[WorldItem] 一轮会话未初始化（GameplayEntry），无法拾取。");
                return false;
            }

            entry.Session.Inventory.Add(definition, pickupCount);
            Log.Info($"[WorldItem] 拾取 {definition.Id} × {pickupCount}（{name}）");

            GameManager.Context?.EventBus.Publish(new ItemAcquiredEvent(definition, pickupCount));

            gameObject.SetActive(false); // 拾取后隐藏：防重复拾取；不 Destroy（可复位/重生系统接管）
            return true;
        }

        /// <inheritdoc />
        public bool CanSelect(InteractionContext context) => definition != null;

        /// <inheritdoc />
        public void OnSelected(InteractionContext context) => _isSelected = true;

        /// <inheritdoc />
        public void OnDeselected() => _isSelected = false;

        /// <summary>拾取动作（纯 C#，构造注入宿主）。</summary>
        private sealed class PickupAction : IInteractionAction
        {
            private readonly WorldItem _host;

            public PickupAction(WorldItem host)
            {
                _host = host;
            }

            public InputSlot Slot => InputSlot.Primary;

            public string PromptText => "拾取";

            public bool CanInteract(InteractionContext context) => _host.CanPickup;

            public void Interact(InteractionContext context) => _host.TryPickup();
        }
    }
}
