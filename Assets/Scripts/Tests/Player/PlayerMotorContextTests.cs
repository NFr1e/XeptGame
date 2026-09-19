using NUnit.Framework;
using UnityEngine;
using XeptGame.Player;

namespace XeptGame.Tests
{
    /// <summary>
    /// <see cref="PlayerMotorContext"/> 派生判定回归（设计决议 §2.2 参考系/胶囊、§5.4 可站立互斥语义、§2.5 起滑门槛）：
    /// 参考系转换、胶囊与眼位同步、坡面/不可站立层/可滑面判定、起滑动态门槛（含平台被动携带排除）、着陆事件载荷。
    /// </summary>
    public class PlayerMotorContextTests : MotorTestBase
    {
        [Test]
        public void WorldMoveIntent_按body旋转转换()
        {
            Input.LocalMoveIntent = Vector3.forward;
            Motor.Rotation = Quaternion.Euler(0f, 90f, 0f);

            var world = Ctx.WorldMoveIntent;

            Assert.That(Vector3.Angle(world, Vector3.right), Is.LessThan(0.01f));
        }

        [Test]
        public void ApplyCapsule_站立与蹲伏_尺寸与眼位同步()
        {
            Ctx.ApplyCapsule(false);
            Assert.AreEqual(Profile.capsuleRadius, Motor.Capsule.Value.Radius, 1e-4f);
            Assert.AreEqual(Profile.standingHeight, Motor.Capsule.Value.Height, 1e-4f);
            Assert.AreEqual(Profile.standingYOffset, Motor.Capsule.Value.YOffset, 1e-4f);
            Assert.AreEqual(Profile.standingYOffset + Profile.standingHeight * 0.5f, Ctx.TargetEyeHeight, 1e-4f);

            Ctx.ApplyCapsule(true);
            Assert.AreEqual(Profile.crouchedHeight, Motor.Capsule.Value.Height, 1e-4f);
            Assert.AreEqual(Profile.crouchedYOffset, Motor.Capsule.Value.YOffset, 1e-4f);
            Assert.AreEqual(Profile.crouchedYOffset + Profile.crouchedHeight * 0.5f, Ctx.TargetEyeHeight, 1e-4f);
        }

        [Test]
        public void IsSlopeSlide_法线超稳定角为真()
        {
            Motor.GroundState = new MotorGroundState(true, true, Quaternion.Euler(60f, 0f, 0f) * Vector3.up);
            Assert.IsTrue(Ctx.IsSlopeSlide, "超过 MaxStableSlopeAngle(45°) 应判为坡面滑动");

            Motor.GroundState = new MotorGroundState(true, true, Quaternion.Euler(20f, 0f, 0f) * Vector3.up);
            Assert.IsFalse(Ctx.IsSlopeSlide);
        }

        [Test]
        public void IsOnNonStableLayer_按可站立层互斥判定()
        {
            Motor.GroundColliderLayerValue = -1;
            Assert.IsFalse(Ctx.IsOnNonStableLayer, "无接地对象时不应判为不可站立");

            Motor.GroundColliderLayerValue = 6; // StableGroundLayers 默认 = 1 << 6
            Assert.IsFalse(Ctx.IsOnNonStableLayer);

            Motor.GroundColliderLayerValue = 7;
            Assert.IsTrue(Ctx.IsOnNonStableLayer);
        }

        [Test]
        public void IsUnstableGroundSurface_坡面或不可站立层()
        {
            Motor.GroundState = new MotorGroundState(true, true, Vector3.up);
            Motor.GroundColliderLayerValue = 6;
            Assert.IsFalse(Ctx.IsUnstableGroundSurface, "平地 + 可站立层 = 稳定");

            Motor.GroundState = new MotorGroundState(true, true, Quaternion.Euler(60f, 0f, 0f) * Vector3.up);
            Assert.IsTrue(Ctx.IsUnstableGroundSurface, "陡坡应判为可滑面");

            Motor.GroundState = new MotorGroundState(true, true, Vector3.up);
            Motor.GroundColliderLayerValue = 7;
            Assert.IsTrue(Ctx.IsUnstableGroundSurface, "不可站立层应判为可滑面");
        }

        [Test]
        public void 动态道具_不算可滑面()
        {
            // §5.5 单向碰撞：动态道具是"纯障碍"——不在可站立集合，但也**不是**可滑面
            Motor.GroundState = new MotorGroundState(true, true, Vector3.up);
            Motor.GroundColliderLayerValue = 16; // DynamicProp
            Motor.GroundIsDynamicBodyValue = true;

            Assert.IsTrue(Ctx.IsOnNonStableLayer, "道具层不在可站立集合");
            Assert.IsFalse(Ctx.IsUnstableGroundSurface, "但动态道具不算可滑面（落上去保持 Fall，不吸附、不滑）");
        }

        [Test]
        public void 道具上的陡坡_仍按坡面滑动()
        {
            // 几何优先：道具上的陡坡仍走坡面规则（滑下去），不因"是道具"而被排除
            Motor.GroundState = new MotorGroundState(true, false, Quaternion.Euler(60f, 0f, 0f) * Vector3.up);
            Motor.GroundColliderLayerValue = 16;
            Motor.GroundIsDynamicBodyValue = true;

            Assert.IsTrue(Ctx.IsUnstableGroundSurface);
        }

        [Test]
        public void WantSprint_WantCrouch_读意图状态()
        {
            Assert.IsFalse(Ctx.WantSprint);
            Assert.IsFalse(Ctx.WantCrouch);

            Input.SprintIntent = true;
            Assert.IsTrue(Ctx.WantSprint);

            Input.CrouchIntent = true;
            Assert.IsTrue(Ctx.WantCrouch);
        }

        [Test]
        public void CanStartSlide_需滑铲请求与速度门槛()
        {
            Motor.SetVelocity(Vector3.forward * 8f);

            // 回归护栏：**持续蹲伏意图不构成滑铲请求**——滑铲进入看瞬时意图，
            // 否则"蹲伏→奔跑→到速"会再次命中滑铲规则而自激循环（§2.5）
            Input.CrouchIntent = true;
            Assert.IsFalse(Ctx.CanStartSlide, "只有持续蹲伏意图不可起滑");

            Input.SlideIntent = true;
            Assert.IsTrue(Ctx.CanStartSlide, "滑铲请求 + 速度达门槛即可起滑");

            Motor.SetVelocity(Vector3.forward * (Profile.slideEntryMinSpeed - 0.01f));
            Assert.IsFalse(Ctx.CanStartSlide, "低于门槛不可起滑");
        }

        [Test]
        public void CanStartSlide_排除移动平台被动携带()
        {
            Input.SlideIntent = true;
            // 平台带着玩家高速移动：合成速度很大，但自主速度为零
            Motor.SetVelocity(Vector3.zero, Vector3.forward * 20f);

            Assert.AreEqual(20f, Ctx.Motor.Velocity.magnitude, 1e-3f);
            Assert.IsFalse(Ctx.CanStartSlide, "被动携带不应触发起滑（用自主速度判定）");
        }

        [Test]
        public void CanStartSlide_只看水平分量()
        {
            Input.SlideIntent = true;
            // 垂直高速 + 水平静止：不应起滑
            Motor.SetVelocity(Vector3.up * 30f);
            Assert.IsFalse(Ctx.CanStartSlide);
        }

        [Test]
        public void CanSprint_需冲刺意图移动输入与前向()
        {
            Input.LocalMoveIntent = Vector3.forward;
            Input.MoveInput = new Vector2(0f, 1f); // 前向（只有前半球可冲刺）
            Assert.IsFalse(Ctx.CanSprint, "无冲刺意图不可奔跑");

            Input.SprintIntent = true;
            Assert.IsTrue(Ctx.CanSprint);

            Input.LocalMoveIntent = Vector3.zero;
            Assert.IsFalse(Ctx.CanSprint, "无移动输入不算可奔跑（奔跑意图不能停在原地生效）");
        }

        [Test]
        public void WantSlide_读瞬时滑铲请求()
        {
            Assert.IsFalse(Ctx.WantSlide);

            Input.SlideIntent = true;

            Assert.IsTrue(Ctx.WantSlide);
        }

        [Test]
        public void RaiseLanding_携带冲击速度与法线()
        {
            MotorLandingInfo? captured = null;
            Ctx.Landing += info => captured = info;

            Ctx.LastAirborneVerticalSpeed = 12.5f;
            Ctx.RaiseLanding(Vector3.up);

            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(12.5f, captured.Value.ImpactSpeed, 1e-4f);
            Assert.AreEqual(Vector3.up, captured.Value.GroundNormal);
        }

        [Test]
        public void 会话数据_默认为空闲()
        {
            Assert.IsFalse(Ctx.JumpConsumed);
            Assert.AreEqual(0f, Ctx.TimeSinceLastAbleToJump, 1e-6f);
            Assert.AreEqual(Vector3.zero, Ctx.PendingJumpImpulse);
            Assert.AreEqual(Vector3.zero, Ctx.AddVelocityAccumulator);
            Assert.AreEqual(0f, Ctx.SlideBoostRemaining, 1e-6f);
        }
    }
}
