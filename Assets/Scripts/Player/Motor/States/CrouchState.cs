using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 蹲伏（独立低速模式，与冲刺互斥——设计决议 §2.2）。
    /// 进入：胶囊变矮；退出：恢复站立前做 overlap 检查，有阻挡则保持蹲伏。
    /// 蹲伏内可低速移动（蹲走），禁跳（需先起身）。
    /// 能力：实现 <see cref="ICrouchable"/>（蹲伏姿态属于"蹲伏能力集"，调度器对已蹲叶子的
    /// 蹲伏请求幂等跳过、保持由本状态内部处理）；**不实现 <see cref="IJumpable"/>** ——
    /// 蹲伏禁跳由"非跳跃能力"表达（替代原 <c>is CrouchState</c> 硬编码排除）。
    /// </summary>
    public sealed class CrouchState : MotorStateBase, ICrouchable
    {
        public override void OnEnter()
        {
            // 蹲伏胶囊 + 同步眼位目标（眼位 = 胶囊顶部，CrouchEye 效果源消费）
            Ctx.ApplyCapsule(true);
        }

        public override void ApplyVelocity(ref Vector3 velocity, float deltaTime)
        {
            ApplyGroundMove(ref velocity, Ctx.Profile.crouchSpeed, Ctx.Profile.stableMovementSharpness, deltaTime);
            ConsumeAddVelocity(ref velocity);
        }

        public override void ApplyRotation(ref Quaternion rotation, float deltaTime)
            => ApplyOrientation(ref rotation, deltaTime);

        public override void Update(float deltaTime)
        {
            if (Ctx.WantCrouch)
            {
                return; // 仍想蹲
            }

            // 起身：以站立尺寸探测，有阻挡则保持蹲伏
            var profile = Ctx.Profile;
            bool blocked = Ctx.Motor.CharacterOverlapCheck(
                Ctx.Motor.TransientPosition,
                Ctx.Motor.TransientRotation,
                profile.capsuleRadius,
                profile.standingHeight,
                profile.standingYOffset);

            if (!blocked)
            {
                Ctx.ApplyCapsule(false);
                Fsm.RequestChange<IdleState>();
            }
        }
    }
}
