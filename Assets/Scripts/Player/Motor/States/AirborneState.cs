using System;
using XeptKit.FSM;

namespace XeptGame.Player
{
    /// <summary>
    /// 非稳定接地复合状态（父）：共享离地计时（土狼时间），子状态管空气/滑动语义。
    /// 子机器：{ Fall, UnstableGround }（设计决议 §2）。
    /// </summary>
    public sealed class AirborneState : CompositeStateBase
    {
        private PlayerMotorContext Ctx => Context as PlayerMotorContext;
        protected override Type ResolveInitialSubState() => typeof(FallState);

        public override void Update(float deltaTime)
        {
            // 离地计时（土狼时间窗口；Grounded 置 0）
            Ctx.TimeSinceLastAbleToJump += deltaTime;
        }
    }
}
