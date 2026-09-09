using XeptKit.FSM;

namespace XeptGame.Equip
{
    /// <summary>已收回态：仍占槽、允许转出；目标拒绝时可稳定停驻（相位由 Fsm 状态推导）。</summary>
    public sealed class StowedState : StateBase
    {
    }
}
