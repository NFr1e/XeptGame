using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 冲刺（高速档）。准入 = <see cref="PlayerMotorContext.CanSprint"/>（冲刺意图 + 移动意图 + **非后向**）。
    /// 规则：**后半球禁止冲刺**——后向输入立即降级 Walk（《孤岛惊魂 6》实测做法），
    /// 副作用是后向速度低于起滑门槛 ⇒ 后向滑铲在速度上不可能；
    /// 蹲伏与冲刺互斥——冲刺中按蹲**起滑**（<see cref="ISlidable"/> → SlideState，速度达门槛时）。
    /// 能力：可蹲可跳（<see cref="ICrouchable"/>/<see cref="IJumpable"/>）、可起滑（<see cref="ISlidable"/>）。
    /// </summary>
    public sealed class SprintState : MotorStateBase, ICrouchable, IJumpable, ISlidable
    {
        public override void ApplyVelocity(ref Vector3 velocity, float deltaTime)
        {
            ApplyGroundMove(ref velocity, Ctx.Profile.sprintSpeed, Ctx.Profile.stableMovementSharpness, deltaTime);
            ConsumeAddVelocity(ref velocity);
        }

        public override void ApplyRotation(ref Quaternion rotation, float deltaTime)
            => ApplyOrientation(ref rotation, deltaTime);

        public override void Update(float deltaTime)
        {
            // 奔跑准入失效 → 退出到 Walk。三种失效来源：冲刺意图终止、无移动意图、**后向输入**
            // （后半球禁止冲刺 ⇒ 速度降为 walkSpeed，起滑门槛随之不可达，§2.2）。
            // 蹲伏/滑铲仍由 MotorActionDispatcher 统一处理（冲刺中按蹲 = 起滑）。
            if (!Ctx.CanSprint)
            {
                Fsm.RequestChange<WalkState>();
            }
        }
    }
}
