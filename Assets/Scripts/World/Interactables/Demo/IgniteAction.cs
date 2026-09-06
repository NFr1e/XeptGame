using XeptGame.Interaction;

namespace XeptGame.World.Interactables.Demo
{
    /// <summary>
    /// 演示动作：点燃（Primary/E，纯 C#，构造注入宿主）。仅熄灭状态可交互。
    /// </summary>
    public sealed class IgniteAction : IInteractionAction
    {
        private readonly DemoCampfireProp _prop;

        public IgniteAction(DemoCampfireProp prop)
        {
            _prop = prop;
        }

        public InputSlot Slot => InputSlot.Primary;

        public string PromptText => "点燃";

        public bool CanInteract(InteractionContext context)
            => _prop != null && !_prop.IsLit;

        public void Interact(InteractionContext context)
            => _prop.TryIgnite();
    }
}
