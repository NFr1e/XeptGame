using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 行走（默认速度档）。转移：无意图 → Idle；冲刺意图 → Sprint（档位级输入留在子状态）。
    /// 蹲伏为姿态级动作，由 MotorActionDispatcher 统一判定（<see cref="ICrouchable"/>）。
    /// 能力：可蹲可跳（<see cref="ICrouchable"/>/<see cref="IJumpable"/>）。
    /// </summary>
    public sealed class WalkState : MotorStateBase, ICrouchable, IJumpable
    {
        public override void ApplyVelocity(ref Vector3 velocity, float deltaTime)
        {
            ApplyGroundMove(ref velocity, Ctx.Profile.walkSpeed, Ctx.Profile.stableMovementSharpness, deltaTime);
            ConsumeAddVelocity(ref velocity);
        }

        public override void ApplyRotation(ref Quaternion rotation, float deltaTime)
            => ApplyOrientation(ref rotation, deltaTime);

        public override void Update(float deltaTime)
        {
            if (Ctx.WorldMoveIntent.sqrMagnitude <= 0f)
            {
                Fsm.RequestChange<IdleState>();
                return;
            }

            if (Ctx.WantSprint)
            {
                Fsm.RequestChange<SprintState>();
            }
        }
    }
}
