using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 自由落体：空气控制 + 重力 + 拖拽；消费起跳初速度（PendingJumpImpulse）。
    /// 转移：稳定接地 → Grounded；探到不稳定地面 → UnstableGround；土狼窗口内跳跃 → 施加冲量（不转移）。
    /// </summary>
    public sealed class FallState : MotorStateBase
    {
        public override void ApplyVelocity(ref Vector3 velocity, float deltaTime)
        {
            // 起跳初速度（跳跃转移时写入）：冲量 + 保留水平分量、替换垂直分量
            if (Ctx.PendingJumpImpulse.sqrMagnitude > 0f)
            {
                velocity = Ctx.PendingJumpImpulse + velocity - Vector3.Project(velocity, Ctx.Motor.CharacterUp);
                Ctx.PendingJumpImpulse = Vector3.zero;
            }

            ApplyAirMove(ref velocity, deltaTime);
            ConsumeAddVelocity(ref velocity);
        }

        public override void ApplyRotation(ref Quaternion rotation, float deltaTime)
            => ApplyOrientation(ref rotation, deltaTime);

        public override void Update(float deltaTime)
        {
            var ground = Ctx.Motor.Ground;

            // 稳定接地 → Grounded（跨层级冒泡到根机器）。
            // 防抖：ForceUnground 生效期间（起跳瞬间电机 GroundingStatus 可能滞后一物理帧，
            // 仍是"稳定接地"旧值）禁止落地转移，避免 Fall→Grounded 抖动重置 JumpConsumed 导致二段跳。
            // 排除不可站立层：非 StableGroundLayers 上 KCC 法线角判定为稳定（IsStableOnGround=true），
            // 但语义上不可站立 → 不转 Grounded，继续走到 UnstableGround 判定。
            if (ground.IsStableOnGround && !Ctx.Motor.MustUnground && !Ctx.IsOnNonStableLayer)
            {
                RootFsm.RequestChange<GroundedState>();
                return;
            }

            // 坡面滑动 / 不可站立层 → UnstableGround（子机器内转移）。
            // 判定：IsUnstableGroundSurface = 坡面（法线超稳定角）或接地不在 StableGroundLayers。
            // 防抖：ForceUnground 生效期间（刚起跳）禁止转 UnstableGround（同前）。
            if (Ctx.IsUnstableGroundSurface && !Ctx.Motor.MustUnground)
            {
                Fsm.RequestChange<UnstableGroundState>();
                return;
            }

            // 土狼时间跳跃（不转移：仍在空中，仅施加冲量）。
            // 与 PerformJump 一致：ForceUnground 跳过起跳后接地探测，防止 Fall→UnstableGround 链。
            if (Ctx.Input.JumpPressed && !Ctx.JumpConsumed
                && Ctx.TimeSinceLastAbleToJump <= Ctx.Profile.jumpPostGroundingGraceTime)
            {
                Ctx.PendingJumpImpulse = Ctx.Motor.CharacterUp * Ctx.Profile.jumpUpSpeed;
                Ctx.Motor.ForceUnground();
                Ctx.JumpConsumed = true;
            }
        }
    }
}
