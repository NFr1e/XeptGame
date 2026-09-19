using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 滑铲（主动高速蹲伏姿态，设计决议 §2.5）：冲刺中按蹲触发（结构能力 <see cref="ISlidable"/> +
    /// 速度门槛 <see cref="PlayerMotorContext.CanStartSlide"/>，由 MotorActionDispatcher 准入）。
    /// **进入需前向、滑铲中不可背向**——由两侧共同保证：
    /// ① **准入侧**：仅前半球可冲刺（<see cref="PlayerMotorContext.CanSprint"/>）⇒ 纯侧向与后向只能走（速度低于起滑门槛）⇒ 其无法起滑；
    /// ② **维持侧**：起滑锁定方向（<see cref="PlayerMotorContext.SlideDirection"/>）+ 转向剔除后向分量 ⇒ 中途不可能被转向背向
    /// （但滑铲**内**仍可转向至侧向，上限 90°）。
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

            // 起滑方向锁定（会话数据）：= 入口水平**自主**速度方向（零速兜底用移动意图方向）。
            // 转向时以它为"前半球"基准剔除后向分量 ⇒ 滑铲结构上不可能被转向背向（"没有后向滑铲"的维持侧保证）。
            // 起滑门槛（速度 ≥ slideEntryMinSpeed）保证此处方向非零。
            var up = Ctx.Motor.CharacterUp;
            var horizontalVelocity = Vector3.ProjectOnPlane(Ctx.Motor.OwnVelocity, up);
            Ctx.SlideDirection = horizontalVelocity.sqrMagnitude > 1e-6f
                ? horizontalVelocity.normalized
                : Vector3.ProjectOnPlane(Ctx.WorldMoveIntent, up).normalized;
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
            //    后向剔除：转向靶 = 输入中相对**起滑锁定方向**的前向 + 侧向分量（去掉后向分量）——
            //    靶恒落在起滑方向的前半球 ⇒ 滑铲**结构上不可能被转向背向**（物理上"只有前向和侧向"）；
            //    纯后向输入 ⇒ 去后向后退化为零向量 ⇒ 不转向（保持原动量，反向蹬地不改变滑行方向）。
            if (direction.sqrMagnitude > 0f && profile.slideSteerSpeed > 0f
                && TryGetSteerTarget(groundNormal, out var steerTarget))
            {
                float maxRadians = profile.slideSteerSpeed * Mathf.Deg2Rad * deltaTime;
                direction = Vector3.RotateTowards(direction, steerTarget, maxRadians, 0f);
            }

            velocity = direction * speed;

            // 6) 备选切向转向加速（默认 0 = 关闭）：沿输入切向附加弱加速（**会改变速度大小**）——
            //    仅当需要复刻参考实现的"切向弱加速"手感时才启用（见设计决议 §2.5/§6.1）。
            //    同样使用剔后向的转向靶，避免该通道把滑铲推向背向。
            if (profile.slideSteerAcceleration > 0f
                && TryGetSteerTarget(groundNormal, out var accelDir))
            {
                velocity += accelDir * (profile.slideSteerAcceleration * deltaTime);
            }

            ConsumeAddVelocity(ref velocity);
        }

        public override void ApplyRotation(ref Quaternion rotation, float deltaTime)
            => ApplyOrientation(ref rotation, deltaTime);

        /// <summary>
        /// 转向靶：世界移动意图投影到地面切向，并**剔除相对起滑锁定方向
        /// （<see cref="PlayerMotorContext.SlideDirection"/>）的后向分量**（只留前向 + 侧向）。
        /// 返回 false = 无有效转向靶（无输入，或纯后向输入被剔除后退化为零向量）——此时不转向。
        /// 该规则使转向靶恒落在起滑方向的前半球，因此滑铲**结构上不可能被转向背向**（§2.5）。
        /// </summary>
        private bool TryGetSteerTarget(Vector3 groundNormal, out Vector3 target)
        {
            target = Vector3.zero;

            var intent = Vector3.ProjectOnPlane(Ctx.WorldMoveIntent, groundNormal);
            if (intent.sqrMagnitude <= 1e-6f)
            {
                return false;
            }

            var forward = Ctx.SlideDirection;
            if (forward.sqrMagnitude > 1e-6f)
            {
                // forward 为单位向量 ⇒ 点积即"沿起滑方向的分量"；仅当为负（后向）时减去它
                intent -= forward * Mathf.Min(0f, Vector3.Dot(intent, forward));
            }

            if (intent.sqrMagnitude <= 1e-6f)
            {
                return false; // 纯后向输入：不转向（保持原动量方向——反向蹬地不改变滑行方向）
            }

            target = intent.normalized;
            return true;
        }

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
