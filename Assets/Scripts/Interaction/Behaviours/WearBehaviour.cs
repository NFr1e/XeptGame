using XeptGame.Items.Operations;

namespace XeptGame.Interaction.Behaviours
{
    /// <summary>
    /// 背上（Hold/长按）：把<b>容器/背包</b>放到背上（背槽空 = 整体背上；背槽已占 = 换包，旧包需有去向）。
    /// <b>无包时仍然出现</b>——它就是"从无包变有包"的那条路。
    /// </summary>
    public sealed class WearBehaviour : IInteractionBehaviour
    {
        /// <inheritdoc />
        public InputSlot Slot => InputSlot.Hold;

        /// <inheritdoc />
        public string PromptKey => InteractionPromptKeys.Wear;

        /// <inheritdoc />
        public bool CanBuildOn(IInteractionTarget target, ICarryFacts carry)
            => target is IPickupTarget pickup && pickup.IsCarrier;

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
