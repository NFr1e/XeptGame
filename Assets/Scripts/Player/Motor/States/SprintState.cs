using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>冲刺（高速档）。规则：蹲伏与冲刺互斥——冲刺中按蹲直接转移 Crouch（视为冲刺取消）。</summary>
    public sealed class SprintState : MotorStateBase
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
            // 蹲伏意图由父状态（GroundedState）统一处理（冲刺中按蹲 = 取消冲刺进蹲，互斥语义）
            if (!Ctx.WantSprint || Ctx.WorldMoveIntent.sqrMagnitude <= 0f)
            {
                Fsm.RequestChange<WalkState>();
            }
        }
    }
}
