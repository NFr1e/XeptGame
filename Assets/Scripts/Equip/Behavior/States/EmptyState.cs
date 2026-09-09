using XeptKit.FSM;

namespace XeptGame.Equip
{
    /// <summary>空手态：无槽位占用，不允许拿出或使用（相位由 Fsm 状态推导，本类为占位标记）。</summary>
    public sealed class EmptyState : StateBase
    {
    }
}
