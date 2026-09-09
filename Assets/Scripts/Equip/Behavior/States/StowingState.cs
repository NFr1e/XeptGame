using System;
using XeptKit.FSM;

namespace XeptGame.Equip
{
    /// <summary>收回态：以行为时钟推进，完成后进入 Stowed（不直接移动物品）；收回中禁止反向打断。</summary>
    public sealed class StowingState : StateBase
    {
        private EquipContext Ctx => Context as EquipContext;

        public override void Update(float deltaTime)
        {
            Ctx.Elapsed = Math.Min(Ctx.Duration, Ctx.Elapsed + deltaTime);
            if (Ctx.Elapsed >= Ctx.Duration)
            {
                Fsm.RequestChange<StowedState>();
            }
        }
    }
}
