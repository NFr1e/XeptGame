using XeptGame.Items.Operations;

namespace XeptGame.Interaction.Behaviours
{
    /// <summary>
    /// 装备（Hold/长按）：把可持物<b>拿到身上</b>（先上一手，余量按物品域规则处理）。
    /// <b>无包时仍然出现</b>——它不经过背包，是"没有背包也有去处"的那条路。
    /// </summary>
    public sealed class EquipBehaviour : IInteractionBehaviour
    {
        /// <inheritdoc />
        public InputSlot Slot => InputSlot.Hold;

        /// <inheritdoc />
        public string PromptKey => InteractionPromptKeys.Equip;

        /// <inheritdoc />
        public bool CanBuildOn(IInteractionTarget target, ICarryFacts carry)
            => target is IPickupTarget pickup && pickup.IsHoldable;

        /// <inheritdoc />
        public bool CanAct(IInteractionTarget target, InteractionContext context)
            => target is IPickupTarget pickup && pickup.CanPickup;

        /// <inheritdoc />
        public void Act(IInteractionTarget target, InteractionContext context)
        {
            if (target is IPickupTarget pickup)
            {
                pickup.RequestPickup(PickupIntent.ForceHold);
            }
        }
    }
}
