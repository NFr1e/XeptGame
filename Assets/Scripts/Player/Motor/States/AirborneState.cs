using System;
using XeptKit.FSM;

namespace XeptGame.Player
{
    /// <summary>
    /// 非稳定接地复合状态（父）：共享离地计时（土狼时间）与垂直下落速度会话重置，
    /// 子状态管空气/滑动语义。子机器：{ Fall, UnstableGround }（设计决议 §2）。
    /// 垂直下落速度捕获在子状态 ApplyVelocity 顶部进行（见 <see cref="MotorStateBase.CaptureFallSpeed"/>——
    /// 读取 sweep 投影前的带入速度并取会话最大值，规避 KCC 贴墙下落时结算后速度被投影归零）。
    /// </summary>
    public sealed class AirborneState : CompositeStateBase
    {
        private PlayerMotorContext Ctx => Context as PlayerMotorContext;
        protected override Type ResolveInitialSubState() => typeof(FallState);

        public override void OnEnter()
        {
            // 新离地周期：重置下落速度会话最大值（落地事件消费后由下次离地重新累积）
            Ctx.LastAirborneVerticalSpeed = 0f;
        }

        public override void Update(float deltaTime)
        {
            // 离地计时（土狼时间窗口；Grounded 置 0）
            Ctx.TimeSinceLastAbleToJump += deltaTime;
        }
    }
}
