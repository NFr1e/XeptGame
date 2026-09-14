namespace XeptGame.Interaction.Behaviours
{
    /// <summary>熄灭（Primary）：仅"燃着"时出现在清单里（与点燃构成状态互斥的成员切换）。</summary>
    public sealed class ExtinguishBehaviour : IInteractionBehaviour
    {
        /// <inheritdoc />
        public InputSlot Slot => InputSlot.Primary;

        /// <inheritdoc />
        public string PromptKey => InteractionPromptKeys.Extinguish;

        /// <inheritdoc />
        public bool CanBuildOn(IInteractionTarget target, ICarryFacts carry)
            => target is IFireTarget fire && fire.IsLit;

        /// <inheritdoc />
        public bool CanAct(IInteractionTarget target, InteractionContext context)
            => target is IFireTarget fire && fire.IsLit;

        /// <inheritdoc />
        public void Act(IInteractionTarget target, InteractionContext context)
        {
            if (target is IFireTarget fire)
            {
                fire.TryExtinguish();
            }
        }
    }
}
