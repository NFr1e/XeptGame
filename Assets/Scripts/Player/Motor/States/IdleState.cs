using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>静止（速度阻尼到零）。转移：有移动意图 → Walk。</summary>
    public sealed class IdleState : MotorStateBase
    {
        public override void ApplyVelocity(ref Vector3 velocity, float deltaTime)
        {
            ApplyGroundMove(ref velocity, 0f, Ctx.Profile.stableMovementSharpness, deltaTime);
            ConsumeAddVelocity(ref velocity);
        }

        public override void ApplyRotation(ref Quaternion rotation, float deltaTime)
            => ApplyOrientation(ref rotation, deltaTime);

        public override void Update(float deltaTime)
        {
            if (Ctx.WorldMoveIntent.sqrMagnitude > 0f)
            {
                Fsm.RequestChange<WalkState>();
            }
        }
    }
}
