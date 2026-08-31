using UnityEngine;
using XeptKit.FSM;

namespace XeptGame.Player
{
    /// <summary>
    /// 电机状态基类：类型化 Context 访问 + KCC 电机回调的分发钩子。
    /// 依据 XeptKit.FSM 约定：状态实例按类型缓存复用，禁止字段承载会话数据（全部经 <see cref="Ctx"/>）；
    /// 字段仅可承载类型化配置/常量。
    /// 公共移动辅助（地面移动/空气控制/朝向/加力消费）供子状态复用，参考 KCC Example 的速度求解拆分。
    /// </summary>
    public abstract class MotorStateBase : StateBase
    {
        /// <summary>类型化上下文（构造时由 Fsm 注入）。</summary>
        protected PlayerMotorContext Ctx => Context as PlayerMotorContext;

        // ============================================================
        // KCC 回调分发钩子（由 PlayerCharacterController 调用；默认空实现）
        // ============================================================

        /// <summary>UpdateVelocity 分发：计算当前速度目标（唯一允许改速度的地方）。</summary>
        public virtual void ApplyVelocity(ref Vector3 velocity, float deltaTime)
        {
        }

        /// <summary>UpdateRotation 分发：计算当前旋转目标（唯一允许改旋转的地方）。</summary>
        public virtual void ApplyRotation(ref Quaternion rotation, float deltaTime)
        {
        }

        /// <summary>AfterCharacterUpdate 分发：帧后处理（起身 overlap 检查等）。</summary>
        public virtual void PostUpdate(float deltaTime)
        {
        }

        // ============================================================
        // 公共移动辅助（状态类复用）
        // ============================================================

        /// <summary>
        /// 地面移动：斜坡速度重定向 + 平滑逼近目标速度（KCC Stable 逻辑）。
        /// 目标速度含上下坡修正（B1）：下坡加速、上坡减速（重力切向 × slopeGravityInfluence），
        /// 上坡速度钳制到 speed × minSlopeUpSpeedFactor（避免陡坡卡住）；平地无影响。
        /// </summary>
        protected void ApplyGroundMove(ref Vector3 velocity, float speed, float sharpness, float deltaTime)
        {
            var groundNormal = Ctx.Motor.Ground.GroundNormal;
            var intent = Ctx.WorldMoveIntent;
            var profile = Ctx.Profile;

            // 斜坡速度重定向（保持速度模长、投影到地面切向）
            velocity = Ctx.Motor.GetDirectionTangentToSurface(velocity, groundNormal) * velocity.magnitude;

            // 目标速度：移动意图经地面法线重定向
            Vector3 target = Vector3.zero;
            if (intent.sqrMagnitude > 0f)
            {
                var inputRight = Vector3.Cross(intent, Ctx.Motor.CharacterUp);
                var reorientedInput = Vector3.Cross(groundNormal, inputRight).normalized * intent.magnitude;

                // 上下坡速度修正：下坡 target > speed（重力加速）、上坡 < speed（重力减速）
                float effectiveSpeed = speed;
                if (profile.slopeGravityInfluence > 0f)
                {
                    // 坡角（法线与 up 的夹角；平地 0 → 无修正）
                    float slopeAngle = Vector3.Angle(groundNormal, Ctx.Motor.CharacterUp);
                    // 上下坡方向：重力切向与移动方向点积（下坡为正、上坡为负）
                    Vector3 gravityTangent = Vector3.ProjectOnPlane(profile.gravity, groundNormal);
                    float slopeDir = Mathf.Sign(Vector3.Dot(gravityTangent, reorientedInput.normalized));

                    effectiveSpeed = speed * (1f + slopeDir * profile.slopeGravityInfluence * Mathf.Sin(slopeAngle * Mathf.Deg2Rad));
                    // 上坡最低速度（保证可爬坡）
                    effectiveSpeed = Mathf.Max(effectiveSpeed, speed * profile.minSlopeUpSpeedFactor);
                }

                target = reorientedInput * effectiveSpeed;
            }

            velocity = Vector3.Lerp(velocity, target, 1f - Mathf.Exp(-sharpness * deltaTime));
        }

        /// <summary>
        /// 空气控制（手感决议：保持惯性、弱转向）：空中**不沿速度方向加速**——
        /// 起跳后水平速度保持起跳时的地面速度；仅"转向"分量（垂直于当前水平速度的输入）
        /// 经 airControl 弱化生效，玩家空中可微调方向但速度大小基本不变。
        /// 无水平速度时（站立起跳/滞空启动）按输入方向弱化启动。
        /// 另含：防爬陡坡投影 + 重力 + 拖拽（KCC Air 逻辑）。
        /// </summary>
        protected void ApplyAirMove(ref Vector3 velocity, float deltaTime)
        {
            var profile = Ctx.Profile;
            var intent = Ctx.WorldMoveIntent;

            if (intent.sqrMagnitude > 0f)
            {
                var horizontal = Vector3.ProjectOnPlane(velocity, Ctx.Motor.CharacterUp);
                Vector3 desired = intent * profile.airAccelerationSpeed * deltaTime;

                Vector3 addedVelocity;
                if (horizontal.sqrMagnitude > 1e-6f)
                {
                    // 有水平速度：仅转向分量（desired 垂直速度方向的部分）生效，沿速度方向不加速
                    Vector3 velDir = horizontal.normalized;
                    Vector3 turn = desired - Vector3.Project(desired, velDir);
                    addedVelocity = turn * profile.airControl;
                }
                else
                {
                    // 无水平速度：弱化启动（乘 airControl 与转向手感一致）
                    addedVelocity = desired * profile.airControl;
                }

                // 防止沿不稳定坡面爬升（KCC 逻辑：恢复原版 Dot 条件 + 零向量防御）：
                // 仅当加速度与总速度同向叠加（"爬坡"方向）且存在坡面法线时才投影
                var g = Ctx.Motor.Ground;
                if (g.FoundAnyGround)
                {
                    var perpendicular = Vector3.Cross(
                        Vector3.Cross(Ctx.Motor.CharacterUp, g.GroundNormal),
                        Ctx.Motor.CharacterUp);
                    if (perpendicular.sqrMagnitude > 1e-6f
                        && Vector3.Dot(velocity + addedVelocity, addedVelocity) > 0f)
                    {
                        addedVelocity = Vector3.ProjectOnPlane(addedVelocity, perpendicular.normalized);
                    }
                }

                velocity += addedVelocity;
            }

            velocity += profile.gravity * deltaTime;
            velocity *= 1f / (1f + profile.drag * deltaTime);
        }

        /// <summary>
        /// 身体转向策略（FPS 决议 §2.2）：视角 yaw/pitch 由 PlayerLook 在相机上即时设置
        /// （"鼠标移到哪指哪"），身体（body，KCC 电机）保持初始旋转——避免 KCC 渲染插值
        /// 造成的视角缓冲。当前实现为 no-op（body 不随视角旋转）；
        /// 待有身体模型时再实现"身体朝向移动方向"的平滑转向。
        /// </summary>
        protected void ApplyOrientation(ref Quaternion rotation, float deltaTime)
        {
            // 保持当前旋转（不修改）：body 不随视角旋转
        }

        /// <summary>消费通用加力通道（AddVelocity 注入的击退/推力）。</summary>
        protected void ConsumeAddVelocity(ref Vector3 velocity)
        {
            if (Ctx.AddVelocityAccumulator.sqrMagnitude > 0f)
            {
                velocity += Ctx.AddVelocityAccumulator;
                Ctx.AddVelocityAccumulator = Vector3.zero;
            }
        }
    }
}
