using XeptKit.Input;

namespace XeptGame
{
    public readonly struct GameplayInputLayer : IInputLayer
    {
        public readonly string Name => "Gameplay";
        public readonly int Priority => 10;
        public readonly bool BlockLowerLayers => false;
    }

    public readonly struct MenuInputLayer : IInputLayer
    {
        public readonly string Name => "Menu";
        public readonly int Priority => 20;
        public readonly bool BlockLowerLayers => true;
    }

    public readonly struct SystemInputLayer : IInputLayer
    {
        public readonly string Name => "System";
        public readonly int Priority => 30;
        public readonly bool BlockLowerLayers => false;
    }
}
