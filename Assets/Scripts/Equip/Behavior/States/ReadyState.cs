using XeptKit.FSM;

namespace XeptGame.Equip
{
    /// <summary>就绪态：拿出完成，具体使用仍由后续能力守卫判定（相位由 Fsm 状态推导）。</summary>
    public sealed class ReadyState : StateBase
    {
    }
}
