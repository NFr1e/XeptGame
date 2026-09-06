using XeptGame.Interaction;

namespace XeptGame.World.Interactables.Demo
{
    /// <summary>
    /// 演示动作：添柴（Secondary/F，纯 C#，构造注入宿主）。仅燃烧且燃料未满时可交互（满后灰态演示）。
    /// </summary>
    public sealed class AddFuelAction : IInteractionAction
    {
        private readonly DemoCampfireProp _prop;

        public AddFuelAction(DemoCampfireProp prop)
        {
            _prop = prop;
        }

        public InputSlot Slot => InputSlot.Secondary;

        public string PromptText => "添柴";

        public bool CanInteract(InteractionContext context)
            => _prop != null && _prop.CanAddFuel;

        public void Interact(InteractionContext context)
            => _prop.TryAddFuel();
    }
}
