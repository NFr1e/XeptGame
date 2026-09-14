using NUnit.Framework;
using UnityEngine;
using XeptGame.Player;

namespace XeptGame.Tests
{
    /// <summary>
    /// 电机状态行为回归：滑铲运动模型（boost 摊入 / 线性摩擦 / 限角速度转向 / 坡面驱动）与退出条件、
    /// 蹲伏起身重叠检查、接地物理转移（失稳 → Airborne、不可站立层 → UnstableGround、着陆事件）、
    /// Fall 土狼跳窗口，以及"滑铲离地直接转 Airborne（v1 无滞空容忍）"这一已决议边界。
    /// </summary>
    public class MotorStateTests : MotorTestBase
    {
        // ============================================================
        // 滑铲：进入 / 运动模型
        // ============================================================

        [Test]
        public void 滑铲进入_蹲伏胶囊与boost窗口()
        {
            var fsm = NewSlidingFsm();

            Assert.AreEqual(Profile.crouchedHeight, Motor.Capsule.Value.Height, 1e-4f);
            Assert.AreEqual(Profile.crouchedYOffset, Motor.Capsule.Value.YOffset, 1e-4f);
            Assert.AreEqual(Profile.crouchedYOffset + Profile.crouchedHeight * 0.5f, Ctx.TargetEyeHeight, 1e-4f);
            Assert.AreEqual(Profile.slideBoostDuration, Ctx.SlideBoostRemaining, 1e-4f);
        }

        [Test]
        public void 滑铲摩擦_线性减速()
        {
            var fsm = NewSlidingFsm();
            Ctx.SlideBoostRemaining = 0f; // 隔离 boost，只测摩擦
            var velocity = Vector3.forward * 8f;

            LeafOf(fsm).ApplyVelocity(ref velocity, 0.1f);

            Assert.AreEqual(8f - Profile.slideFriction * 0.1f, velocity.magnitude, 1e-3f);
        }

        [Test]
        public void 滑铲起滑boost_总量在窗口内摊入()
        {
            var fsm = NewSlidingFsm();
            var velocity = Vector3.forward * 8f;

            LeafOf(fsm).ApplyVelocity(ref velocity, 0.05f);
            LeafOf(fsm).ApplyVelocity(ref velocity, 0.05f);

            // 8 + 2（boost 总量）− 1（摩擦 10 m/s² × 0.1s）= 9
            Assert.AreEqual(9f, velocity.magnitude, 1e-3f);
            Assert.AreEqual(0f, Ctx.SlideBoostRemaining, 1e-4f, "窗口用尽后不应继续加速");
        }

        [Test]
        public void 滑铲转向_限角速度且模长不变()
        {
            var fsm = NewSlidingFsm();
            Ctx.SlideBoostRemaining = 0f;
            Input.LocalMoveIntent = Vector3.right; // 目标方向：正右
            var velocity = Vector3.forward * 8f;

            LeafOf(fsm).ApplyVelocity(ref velocity, 0.02f);

            // 转向角速度 150°/s × 0.02s = 3°（只改方向，不因转向而加速）
            Assert.AreEqual(3f, Vector3.Angle(velocity, Vector3.forward), 0.5f);
            Assert.AreEqual(8f - Profile.slideFriction * 0.02f, velocity.magnitude, 1e-3f);
        }

        [Test]
        public void 滑铲转向_无输入时保持方向()
        {
            var fsm = NewSlidingFsm();
            Ctx.SlideBoostRemaining = 0f;
            Input.LocalMoveIntent = Vector3.zero;
            var velocity = Vector3.forward * 8f;

            LeafOf(fsm).ApplyVelocity(ref velocity, 0.05f);

            Assert.That(Vector3.Angle(velocity, Vector3.forward), Is.LessThan(0.01f));
        }

        [Test]
        public void 滑铲坡面_下坡加速_上坡减速()
        {
            var fsm = NewSlidingFsm();
            Ctx.SlideBoostRemaining = 0f;

            // 法线朝 +Z 倾斜 = 沿 +Z 下坡；朝 -Z 倾斜 = 沿 +Z 上坡
            var downhillNormal = Quaternion.Euler(20f, 0f, 0f) * Vector3.up;
            var uphillNormal = Quaternion.Euler(-20f, 0f, 0f) * Vector3.up;

            Motor.GroundState = new MotorGroundState(true, true, downhillNormal);
            var down = Vector3.forward * 5f;
            LeafOf(fsm).ApplyVelocity(ref down, 0.1f);

            Motor.GroundState = new MotorGroundState(true, true, uphillNormal);
            var up = Vector3.forward * 5f;
            LeafOf(fsm).ApplyVelocity(ref up, 0.1f);

            Motor.GroundState = new MotorGroundState(true, true, Vector3.up);
            var flat = Vector3.forward * 5f;
            LeafOf(fsm).ApplyVelocity(ref flat, 0.1f);

            Assert.Greater(down.magnitude, flat.magnitude, "下坡应加速");
            Assert.Less(up.magnitude, flat.magnitude, "上坡应减速");
        }

        // ============================================================
        // 滑铲：退出
        // ============================================================

        [Test]
        public void 滑铲退出_意图终止回Crouch()
        {
            var fsm = NewSlidingFsm();
            Motor.OverlapBlocked = true; // 起身被阻挡 → 稳定停在蹲伏，便于断言
            Input.CrouchIntent = false;

            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)));
            Assert.IsFalse(fsm.IsInHierarchy(typeof(SlideState)));
        }

        [Test]
        public void 滑铲退出_速度低于下限回Crouch()
        {
            var fsm = NewSlidingFsm();
            Motor.OverlapBlocked = true;
            Motor.SetVelocity(Vector3.forward * (Profile.slideExitSpeed - 0.1f));

            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)));
        }

        [Test]
        public void 滑铲速度高于下限且意图保持_继续滑铲()
        {
            var fsm = NewSlidingFsm();
            var velocity = Vector3.forward * 8f;

            Tick(fsm);
            LeafOf(fsm).ApplyVelocity(ref velocity, 0.05f); // boost 由电机回调消费（Tick 只跑状态转移）

            Assert.IsTrue(fsm.IsInHierarchy(typeof(SlideState)));
            Assert.Less(Ctx.SlideBoostRemaining, Profile.slideBoostDuration, "滑铲中 boost 窗口应随 ApplyVelocity 消耗");
        }

        [Test]
        public void 滑铲离地_直接转Airborne_v1无滞空容忍()
        {
            // 已决议边界（§2.5）：不做参考实现的 0.25s 空中容忍——滑铲是 Grounded 子状态，
            // 父状态一判定失稳即转 Airborne（避免 GroundedState 为叶子开口子）
            var fsm = NewSlidingFsm();
            SetAirborneGround();

            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(AirborneState)));
            Assert.IsFalse(fsm.IsInHierarchy(typeof(SlideState)));
        }

        // ============================================================
        // 蹲伏：起身重叠检查
        // ============================================================

        [Test]
        public void 蹲伏起身_头顶阻挡时保持蹲伏()
        {
            var fsm = NewGroundedFsm();
            Input.CrouchIntent = true;
            NewDispatcher(fsm).Dispatch();
            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)));

            Input.CrouchIntent = false;
            Motor.OverlapBlocked = true;
            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)));
        }

        [Test]
        public void 蹲伏起身_无阻挡回Idle()
        {
            var fsm = NewGroundedFsm();
            Input.CrouchIntent = true;
            NewDispatcher(fsm).Dispatch();
            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)));

            Input.LocalMoveIntent = Vector3.zero;
            Input.CrouchIntent = false;
            Motor.OverlapBlocked = false;
            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(IdleState)));
        }

        // ============================================================
        // 接地物理转移（父状态 / 子状态判定）
        // ============================================================

        [Test]
        public void 稳定接地的可站立层_保持Grounded()
        {
            var fsm = NewGroundedFsm();
            SetStableGround(Vector3.up);

            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(GroundedState)));
        }

        [Test]
        public void 失稳_转Airborne并落Fall()
        {
            var fsm = NewGroundedFsm();
            SetAirborneGround();

            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(FallState)));
        }

        [Test]
        public void 不可站立层_经Fall落UnstableGround()
        {
            var fsm = NewGroundedFsm();
            // KCC 法线角判为稳定（IsStableOnGround = true），但层不可站立 → 语义应转 Airborne
            Motor.GroundState = new MotorGroundState(true, true, Vector3.up);
            Motor.GroundColliderLayerValue = 7;

            Tick(fsm);
            Assert.IsTrue(fsm.IsInHierarchy(typeof(AirborneState)), "不可站立层应主动转 Airborne");

            Motor.ClearForceUnground();
            Tick(fsm);
            Assert.IsTrue(fsm.IsInHierarchy(typeof(UnstableGroundState)));
        }

        [Test]
        public void 可滑面恢复可站立_回Grounded()
        {
            var fsm = NewGroundedFsm();
            // KCC 语义：陡坡 = 探到地面但"不稳定接地"（FoundAnyGround=true / IsStableOnGround=false）；
            // 归 Fall 还是 UnstableGround 由业务法线判定（IsSlopeSlide）——层保持可站立以隔离坡面分支
            Motor.GroundState = new MotorGroundState(true, false, Quaternion.Euler(60f, 0f, 0f) * Vector3.up);
            Motor.GroundColliderLayerValue = 6;
            Motor.ClearForceUnground();
            Tick(fsm);
            Assert.IsTrue(fsm.IsInHierarchy(typeof(UnstableGroundState)), "陡坡应落 UnstableGround");

            // 坡面变平（法线恢复）→ 回到 Grounded
            SetStableGround(Vector3.up);
            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(GroundedState)));
        }

        [Test]
        public void 自由落体_稳定接地回Grounded并触发着陆事件()
        {
            var fsm = NewGroundedFsm();
            SetAirborneGround();
            Tick(fsm);
            Assert.IsTrue(fsm.IsInHierarchy(typeof(FallState)));

            MotorLandingInfo? landing = null;
            Ctx.Landing += info => landing = info;
            Ctx.LastAirborneVerticalSpeed = 12.5f; // AirborneState.OnEnter 已重置，落地前由子状态捕获
            Motor.ClearForceUnground();
            SetStableGround(Vector3.up);

            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(GroundedState)));
            Assert.IsTrue(landing.HasValue, "Airborne → Grounded 应触发着陆事件");
            Assert.AreEqual(12.5f, landing.Value.ImpactSpeed, 1e-4f);
        }

        [Test]
        public void 初始进入Grounded_不触发着陆事件()
        {
            var fsm = NewFsm();
            MotorLandingInfo? landing = null;
            Ctx.Landing += info => landing = info;

            fsm.RequestChange<GroundedState>();

            Assert.IsFalse(landing.HasValue, "初始进场（上一状态为 null）不应触发出生伪落地");
        }

        // ============================================================
        // Fall：土狼跳
        // ============================================================

        [Test]
        public void 土狼跳_窗口内施加冲量且不转移()
        {
            var fsm = NewGroundedFsm();
            SetAirborneGround();
            Tick(fsm);
            Assert.IsTrue(fsm.IsInHierarchy(typeof(FallState)));

            Ctx.TimeSinceLastAbleToJump = Profile.jumpPostGroundingGraceTime - 0.05f;
            Input.JumpIntent = true;

            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(FallState)), "土狼跳不转移（仍在空中）");
            Assert.IsTrue(Ctx.JumpConsumed);
            Assert.AreEqual(Profile.jumpUpSpeed, Ctx.PendingJumpImpulse.y, 1e-3f);
            Assert.AreEqual(1, Motor.ForceUngroundCount);
        }

        [Test]
        public void 土狼跳_窗口外不跳()
        {
            var fsm = NewGroundedFsm();
            SetAirborneGround();
            Tick(fsm);

            Ctx.TimeSinceLastAbleToJump = Profile.jumpPostGroundingGraceTime + 0.5f;
            Input.JumpIntent = true;

            Tick(fsm);

            Assert.IsFalse(Ctx.JumpConsumed);
            Assert.AreEqual(Vector3.zero, Ctx.PendingJumpImpulse);
            Assert.AreEqual(0, Motor.ForceUngroundCount);
        }
    }
}
