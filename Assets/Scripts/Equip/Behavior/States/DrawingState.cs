using System;
using XeptKit.FSM;

namespace XeptGame.Equip
{
    /// <summary>拿出态：以行为时钟推进（Elapsed → Duration），到点自转 Ready；允许被收回打断。</summary>
    public sealed class DrawingState : StateBase
    {
        private EquipContext Ctx => Context as EquipContext;

        public override void Update(float deltaTime)
        {
            Ctx.Elapsed = Math.Min(Ctx.Duration, Ctx.Elapsed + deltaTime);
            if (Ctx.Elapsed >= Ctx.Duration)
            {
                Fsm.RequestChange<ReadyState>();
            }
        }
    }
}
