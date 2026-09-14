namespace XeptGame.Interaction.Behaviours
{
    /// <summary>点燃（Primary）：仅"未燃"时出现在清单里。</summary>
    public sealed class IgniteBehaviour : IInteractionBehaviour
    {
        /// <inheritdoc />
        public InputSlot Slot => InputSlot.Primary;

        /// <inheritdoc />
        public string PromptKey => InteractionPromptKeys.Ignite;

        /// <inheritdoc />
        public bool CanBuildOn(IInteractionTarget target, ICarryFacts carry)
            => target is IFireTarget fire && !fire.IsLit;

        /// <inheritdoc />
        public bool CanAct(IInteractionTarget target, InteractionContext context)
            => target is IFireTarget fire && !fire.IsLit;

        /// <inheritdoc />
        public void Act(IInteractionTarget target, InteractionContext context)
        {
            if (target is IFireTarget fire)
            {
                fire.TryIgnite();
            }
        }
    }
}
