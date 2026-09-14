namespace XeptGame.Interaction.Behaviours
{
    /// <summary>调试交互（Primary）：对照物专用，覆盖"冷却期灰态/按下重判"的验证面。</summary>
    public sealed class DebugBehaviour : IInteractionBehaviour
    {
        /// <inheritdoc />
        public InputSlot Slot => InputSlot.Primary;

        /// <inheritdoc />
        public string PromptKey => InteractionPromptKeys.Debug;

        /// <inheritdoc />
        public bool CanBuildOn(IInteractionTarget target, ICarryFacts carry)
            => target is IDebugTarget;

        /// <inheritdoc />
        public bool CanAct(IInteractionTarget target, InteractionContext context)
            => target is IDebugTarget debug && !debug.IsCoolingDown;

        /// <inheritdoc />
        public void Act(IInteractionTarget target, InteractionContext context)
        {
            if (target is IDebugTarget debug)
            {
                debug.TryInteract();
            }
        }
    }
}
