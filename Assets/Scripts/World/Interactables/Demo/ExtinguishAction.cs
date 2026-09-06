using XeptGame.Interaction;

namespace XeptGame.World.Interactables.Demo
{
    /// <summary>
    /// 演示动作：熄灭（Primary/E，纯 C#，构造注入宿主）。仅燃烧状态可交互。
    /// </summary>
    public sealed class ExtinguishAction : IInteractionAction
    {
        private readonly DemoCampfireProp _prop;

        public ExtinguishAction(DemoCampfireProp prop)
        {
            _prop = prop;
        }

        public InputSlot Slot => InputSlot.Primary;

        public string PromptText => "熄灭";

        public bool CanInteract(InteractionContext context)
            => _prop != null && _prop.IsLit;

        public void Interact(InteractionContext context)
            => _prop.TryExtinguish();
    }
}
