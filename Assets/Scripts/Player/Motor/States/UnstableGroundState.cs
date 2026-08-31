using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 不稳定地面（非支撑表面滑动）：
    /// 语义 = "可滑面"（接地但站不住）——稳定层陡坡（法线超稳定角）**或**
    /// 不可站立层（非 StableGroundLayers，互斥语义 §5.4）。
    /// 运动：保留原有速度（动量连续）+ 输入弱化合成（SlidingControl）+ 重力切向下滑。
    /// 转移：地面恢复可站立 → Grounded；离开可滑面 → Fall；可配允许起跳 → Fall。
    /// </summary>
    public sealed class UnstableGroundState : MotorStateBase
    {
        public override void ApplyVelocity(ref Vector3 velocity, float deltaTime)
        {
            // 捕获带入速度的垂直下落速度（落地事件用；必须在最顶部——ref 为投影处理前的真实速度）
            CaptureFallSpeed(ref velocity);

            // KCC 真实表面法线：接地探测已覆盖全部碰撞层（KCC 修改点，见设计决议 §5.4），
            // GroundingStatus.GroundNormal 为真实法线（不可站立层与稳定层坡面均适用）。
            var surfaceNormal = Ctx.Motor.Ground.GroundNormal;
            var profile = Ctx.Profile;

            // 受限运动（合成，不钳制）：**保留原有速度**（跑上滑动面动量连续，不减速），
            // 输入沿表面切向附加弱化加速——可操控滑动方向/脱离，避免卡死；
            // 不设目标速度（无 Lerp 钳制），"受限感"由弱化系数 + 有限加速度体现。
            var intent = Ctx.WorldMoveIntent;
            if (intent.sqrMagnitude > 0f && profile.slidingControl > 0f)
            {
                var inputTangent = Vector3.ProjectOnPlane(intent, surfaceNormal);
                if (inputTangent.sqrMagnitude > 1e-6f)
                {
                    velocity += inputTangent.normalized * profile.slidingAcceleration * profile.slidingControl * deltaTime;
                }
            }

            // 切向（全量）：重力沿表面切向分量——无输入时靠坡下滑。
            // 关键：若加速度含全量法向分量，速度会被拉向垂直，KCC 垂直 sweep 命中坡面后
            // 垂直速度投影到水平切向≈0 → 卡坡。显式切向分量保证持续沿坡下滑。
            velocity += Vector3.ProjectOnPlane(profile.gravity, surfaceNormal) * deltaTime;

            // 法向（微量）：未接触表面时缓慢压向表面（避免仅切向导致的悬空）；
            // 接触后由电机碰撞吸收，且不把速度方向拉向垂直（不破坏下滑）。
            velocity += Vector3.Project(profile.gravity, surfaceNormal) * deltaTime * 0.1f;

            ConsumeAddVelocity(ref velocity);
        }

        public override void ApplyRotation(ref Quaternion rotation, float deltaTime)
            => ApplyOrientation(ref rotation, deltaTime);

        public override void Update(float deltaTime)
        {
            var ground = Ctx.Motor.Ground;

            // 地面变稳定 → Grounded（跨层级冒泡到根机器）。
            // 防抖：ForceUnground 生效期间（刚起跳）禁止落地转移（同 Fall），
            // 避免 JumpConsumed 被重置导致二段跳。
            // 排除不可站立层：非 StableGroundLayers 上 KCC 法线角判定为稳定，但语义不可站立 → 保持 UnstableGround。
            if (ground.IsStableOnGround && !Ctx.Motor.MustUnground && !Ctx.IsOnNonStableLayer)
            {
                RootFsm.RequestChange<GroundedState>();
                return;
            }

            // 非"可滑面" → Fall（子机器内转移）：既非坡面（法线变平：滑出坡底/遇边缘）
            // 也非不可站立表面（探测不到非 StableGroundLayers 对象：离开不可站立地面）。
            // 与 Fall→UnstableGround 的 IsUnstableGroundSurface 判定对称，避免 UnstableGround 状态粘滞。
            // 注意：非稳定表面上 FoundAnyGround 恒为 false（KCC 接地探测忽略非稳定层），
            // 故不能以 FoundAnyGround 判断"地面丢失"。
            if (!Ctx.IsUnstableGroundSurface)
            {
                Fsm.RequestChange<FallState>();
                return;
            }

            // 滑动中起跳（沿地面法线方向）
            if (Ctx.Profile.allowJumpingWhenUnstableGround && Ctx.Input.JumpPressed)
            {
                Ctx.PendingJumpImpulse = ground.GroundNormal * Ctx.Profile.jumpUpSpeed;
                Ctx.Motor.ForceUnground();
                Ctx.JumpConsumed = true;
                Fsm.RequestChange<FallState>();
            }
        }
    }
}
