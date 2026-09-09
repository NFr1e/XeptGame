using System.Collections.Generic;
using UnityEngine;
using XeptGame.Game.Flow;
using XeptGame.Interaction;
using XeptGame.Items;
using XeptGame.Items.Operations;

namespace XeptGame.World.Interactables
{
    /// <summary>世界源容器的场景壳；接纳请求不隐藏，数量提交归零后才隐藏。</summary>
    public sealed class WorldItem : MonoBehaviour, ISelectable, IInteractionActionsHost, IInteractionInfoContext
    {
        [SerializeField] private ItemDefinition definition;
        [SerializeField] private int pickupCount = 1;
        private readonly List<IInteractionAction> _actions = new();
        private WorldItemContainer _source;
        public ItemDefinition Definition => definition;
        public bool IsSelected { get; private set; }
        public IReadOnlyList<IInteractionAction> Actions => _actions;
        public string DisplayNameKey => definition != null ? definition.DisplayNameKey : string.Empty;
        public string DisplayName => string.Empty;
        public Sprite IconSprite => definition != null ? definition.IconSprite : null;
        public Texture IconTexture => definition != null ? definition.IconTexture : null;
        public bool CanPickup => _source != null && _source.Available && !_source.IsBusy && _source.Remaining > 0;
        public int Remaining => _source?.Remaining ?? pickupCount;

        private void Awake()
        {
            _source = new WorldItemContainer(definition, Mathf.Max(0, pickupCount), () => this != null && isActiveAndEnabled, RefreshView);
            _actions.Add(new PickupAction(this, PickupIntent.Tap, InputSlot.Primary, "拾取"));
            _actions.Add(new PickupAction(this, PickupIntent.ForceHold, InputSlot.Hold, "拿取"));
        }

        private void RefreshView()
        {
            if (this != null && _source.Remaining == 0)
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

            return entry.Context.Operations.RequestPickup(_source, definition, _source.Remaining, intent, entry.Context.Inventory);
        }

        public bool CanSelect(InteractionContext context) => definition != null && Remaining > 0;
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
