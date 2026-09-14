namespace XeptGame.Interaction.Behaviours
{
    /// <summary>
    /// 添柴（Secondary）：燃着即出现在清单里；<b>燃料满只影响"此刻能否按"（灰态），不改成员</b>——
    /// 这正是名单（粗）与可用（细）两层分工的示例。
    /// </summary>
    public sealed class AddFuelBehaviour : IInteractionBehaviour
    {
        /// <inheritdoc />
        public InputSlot Slot => InputSlot.Secondary;

        /// <inheritdoc />
        public string PromptKey => InteractionPromptKeys.AddFuel;

        /// <inheritdoc />
        public bool CanBuildOn(IInteractionTarget target, ICarryFacts carry)
            => target is IFireTarget fire && fire.IsLit;

        /// <inheritdoc />
        public bool CanAct(IInteractionTarget target, InteractionContext context)
            => target is IFireTarget fire && fire.CanAddFuel;

        /// <inheritdoc />
        public void Act(IInteractionTarget target, InteractionContext context)
        {
            if (target is IFireTarget fire)
            {
                fire.TryAddFuel();
            }
        }
    }
}
