using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 滑铲（主动高速蹲伏姿态，设计决议 §2.5）：冲刺中按蹲触发（结构能力 <see cref="ISlidable"/> +
    /// 速度门槛 <see cref="PlayerMotorContext.CanStartSlide"/>，由 MotorActionDispatcher 准入）。
    /// 运动模型：**纯动量驱动、无计时器**——起滑 boost（窗口内线性摊入）+ 线性摩擦 +
    /// 坡面切向重力 + 限角速度转向（可选切向弱加速）。
    /// 与 <see cref="UnstableGroundState"/> 的语义区分：本状态是**主动**滑铲（输入触发、有起滑冲击、
    /// 速度由摩擦主导衰减）；UnstableGround 是**被动**表面滑动（地面语义驱动、无目标速度、无起滑冲击）。
    /// 转移：速度低于 slideExitSpeed **或** 蹲伏键释放 → Crouch（起身 overlap 检查统一由 CrouchState 处理，
    /// 故本状态不含任何起身逻辑）；跳跃 → Airborne（实现 <see cref="IJumpable"/>，复用调度器统一接地跳，
    /// 水平分量由 FallState 保留 ⇒ 滑铲跳保留动量）；离地/不可踏上表面 → Airborne（父状态
    /// <see cref="GroundedState"/> 物理转移，本状态无代码）。
    /// 注意：**不实现 <see cref="ICrouchable"/>**——滑铲中蹲伏键保持为 true，若实现，调度器的蹲伏动作
    /// 会把滑铲立刻打断（Slide→Crouch 非幂等）；保持/退出由本状态读 <see cref="PlayerMotorContext.WantCrouch"/> 处理。
    /// </summary>
    public sealed class SlideState : MotorStateBase, IJumpable
    {
        public override void OnEnter()
        {
            // 蹲伏胶囊 + 眼位目标（复用蹲伏尺寸；CrouchEye 效果源据 TargetEyeHeight 做相机下沉）
            Ctx.ApplyCapsule(true);

            // 起滑 boost 会话数据：每帧在 ApplyVelocity 中线性摊入并递减（会话数据进 Context，非状态字段）
            Ctx.SlideBoostRemaining = Ctx.Profile.slideBoostDuration;
        }

        public override void ApplyVelocity(ref Vector3 velocity, float deltaTime)
        {
            var profile = Ctx.Profile;
            var groundNormal = Ctx.Motor.Ground.GroundNormal;

            // 1) 投影到地面切向（保持模长）——与地面移动同口径：滑铲速度始终位于地面切平面内，
            //    贴地/坡面吸附由电机负责（同 Idle/Walk/Sprint，地面状态不额外加重力）。
            velocity = Ctx.Motor.GetDirectionTangentToSurface(velocity, groundNormal) * velocity.magnitude;

            float speed = velocity.magnitude;
            Vector3 direction = speed > 1e-4f ? velocity / speed : Vector3.zero;

            // 2) 起滑 boost：总增量在窗口内线性摊入（**不做方向锁定**——转向本身连续，
            //    方向锁定反而会与限角速度转向打架）。窗口结束后不再施加。
            if (Ctx.SlideBoostRemaining > 0f)
            {
                float window = Mathf.Max(profile.slideBoostDuration, 1e-4f);
                float step = Mathf.Min(deltaTime, Ctx.SlideBoostRemaining);
                speed += profile.slideStartBoost * step / window;
                Ctx.SlideBoostRemaining = Mathf.Max(0f, Ctx.SlideBoostRemaining - step);
            }

            // 3) 坡面驱动：重力切向沿运动方向的投影（下坡为正=加速、上坡为负=减速）；
            //    平地切向重力≈0 → 无影响（与 ApplyGroundMove 的坡面思路一致，但此处是"速度增量"，
            //    因为滑铲没有目标速度可修正）。
            if (direction.sqrMagnitude > 0f)
            {
                var slopeAccel = Vector3.ProjectOnPlane(profile.gravity, groundNormal);
                speed += Vector3.Dot(slopeAccel, direction) * profile.slideSlopeInfluence * deltaTime;
            }

            // 4) 摩擦（线性减速）：滑铲的主导衰减——无计时器，时长由本值与 slideExitSpeed 决定
            //    （时长 ≈ (入口速度 + 起滑增量 − slideExitSpeed) / slideFriction）。
            speed = Mathf.Max(0f, speed - profile.slideFriction * deltaTime);

            // 5) 转向：速度方向朝输入方向**限角速度**旋转——只改方向、不改模长
            //    （避免"转向即加速"的续速漏洞）。
            if (direction.sqrMagnitude > 0f && profile.slideSteerSpeed > 0f)
            {
                var intent = Vector3.ProjectOnPlane(Ctx.WorldMoveIntent, groundNormal);
                if (intent.sqrMagnitude > 1e-6f)
                {
                    float maxRadians = profile.slideSteerSpeed * Mathf.Deg2Rad * deltaTime;
                    direction = Vector3.RotateTowards(direction, intent.normalized, maxRadians, 0f);
                }
            }

            velocity = direction * speed;

            // 6) 备选切向转向加速（默认 0 = 关闭）：沿输入切向附加弱加速（**会改变速度大小**）——
            //    仅当需要复刻参考实现的"切向弱加速"手感时才启用（见设计决议 §2.5/§6.1）。
            if (profile.slideSteerAcceleration > 0f)
            {
                var intent = Vector3.ProjectOnPlane(Ctx.WorldMoveIntent, groundNormal);
                if (intent.sqrMagnitude > 1e-6f)
                {
                    velocity += intent.normalized * (profile.slideSteerAcceleration * deltaTime);
                }
            }

            ConsumeAddVelocity(ref velocity);
        }

        public override void ApplyRotation(ref Quaternion rotation, float deltaTime)
            => ApplyOrientation(ref rotation, deltaTime);

        public override void Update(float deltaTime)
        {
            // 蹲伏**意图**终止 → 退出滑铲（决策层只识别意图：长按模式=松开、点按模式=再按一次，
            // 差异全部折叠在输入层；本层不区分输入模式）；
            // 起身/头顶阻挡检查统一交 CrouchState（本状态不判能否站立）
            if (!Ctx.WantCrouch)
            {
                Fsm.RequestChange<CrouchState>();
                return;
            }

            // 速度低于下限 → 退出（与起滑门槛同口径：水平**自主**速度，排除移动平台被动携带）
            float horizontalSpeed = Vector3.ProjectOnPlane(Ctx.Motor.OwnVelocity, Ctx.Motor.CharacterUp).magnitude;
            if (horizontalSpeed < Ctx.Profile.slideExitSpeed)
            {
                Fsm.RequestChange<CrouchState>();
            }
        }
    }
}
