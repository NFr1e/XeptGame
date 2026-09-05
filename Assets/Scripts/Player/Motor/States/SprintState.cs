using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 冲刺（高速档）。规则：蹲伏与冲刺互斥——冲刺中按蹲直接转移 Crouch（视为冲刺取消，
    /// 由 MotorActionDispatcher 判定）。能力：可蹲可跳（<see cref="ICrouchable"/>/<see cref="IJumpable"/>）。
    /// </summary>
    public sealed class SprintState : MotorStateBase, ICrouchable, IJumpable
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
            // 蹲伏意图由 MotorActionDispatcher 统一处理（冲刺中按蹲 = 取消冲刺进蹲，互斥语义）
            if (!Ctx.WantSprint || Ctx.WorldMoveIntent.sqrMagnitude <= 0f)
            {
                Fsm.RequestChange<WalkState>();
            }
        }
    }
}
