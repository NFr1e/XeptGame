using XeptGame.Items.Operations;

namespace XeptGame.Interaction.Behaviours
{
    /// <summary>
    /// 拾取（Primary/点按）：把世界目标收进<b>当前背包</b>。由 <see cref="WorldItem"/> 的物品定义能力面
    /// 与携带事实共同决定是否出现（无包时不出现——点按在物品域被 <c>NoBag</c> 拒绝）。
    /// </summary>
    public sealed class PickupBehaviour : IInteractionBehaviour
    {
        /// <inheritdoc />
        public InputSlot Slot => InputSlot.Primary;

        /// <inheritdoc />
        public string PromptKey => InteractionPromptKeys.Pickup;

        /// <inheritdoc />
        public bool CanBuildOn(IInteractionTarget target, ICarryFacts carry)
            => target is IPickupTarget && carry != null && carry.HasBag;

        /// <inheritdoc />
        public bool CanAct(IInteractionTarget target, InteractionContext context)
            => target is IPickupTarget pickup && pickup.CanPickup;

        /// <inheritdoc />
        public void Act(IInteractionTarget target, InteractionContext context)
        {
            if (target is IPickupTarget pickup)
            {
                pickup.RequestPickup(PickupIntent.Tap);
            }
        }
    }
}
