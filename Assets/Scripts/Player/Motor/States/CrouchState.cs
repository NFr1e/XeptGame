using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 蹲伏（独立低速模式，与冲刺互斥——设计决议 §2.2）。
    /// **意图优先级：奔跑 &gt; 蹲伏**——蹲伏意图只在"未奔跑"时生效；奔跑意图激活
    /// （<see cref="PlayerMotorContext.CanSprint"/>）时本状态起身进 Sprint（蹲伏意图被**暂时压制**，
    /// 并未终止：奔跑结束后若意图仍在，会重新进入蹲伏）。
    /// 进入：胶囊变矮；退出：恢复站立前做 overlap 检查，有阻挡则保持蹲伏。
    /// 蹲伏内可低速移动（蹲走），禁跳（需先起身）。
    /// 能力：实现 <see cref="ICrouchable"/>（蹲伏姿态属于"蹲伏能力集"，调度器对已蹲叶子的
    /// 蹲伏请求幂等跳过、保持/退出由本状态内部处理）；**不实现 <see cref="IJumpable"/>** ——
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
            // 起身条件（两者其一）：
            // ① 蹲伏意图终止（长按=松开 / 点按=再按一次——差异已在输入层折叠）；
            // ② **奔跑意图压制蹲伏**（意图优先级：奔跑 > 蹲伏，§2.2）：只是暂时压制，不终止蹲伏意图。
            bool wantStand = !Ctx.WantCrouch || Ctx.CanSprint;
            if (!wantStand)
            {
                return; // 仍想蹲
            }

            // 起身：以站立尺寸探测，有阻挡则保持蹲伏（低矮空间即使想跑也站不起来 → 继续蹲，
            // 这正是"允许奔跑"的条件之一）
            var profile = Ctx.Profile;
            bool blocked = Ctx.Motor.CharacterOverlapCheck(
                Ctx.Motor.TransientPosition,
                Ctx.Motor.TransientRotation,
                profile.capsuleRadius,
                profile.standingHeight,
                profile.standingYOffset);

            if (blocked)
            {
                return;
            }

            Ctx.ApplyCapsule(false);

            // 因奔跑意图起身时直接进 Sprint（避免 Idle→Walk→Sprint 的两帧档位抖动）；
            // 否则回 Idle，由档位状态按移动意图自行推导（既有行为）
            if (Ctx.CanSprint)
            {
                Fsm.RequestChange<SprintState>();
            }
            else
            {
                Fsm.RequestChange<IdleState>();
            }
        }
    }
}
