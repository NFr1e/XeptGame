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
        public void CanStartSlide_需蹲伏意图与速度门槛()
        {
            Motor.SetVelocity(Vector3.forward * 8f);
            Input.CrouchIntent = false;
            Assert.IsFalse(Ctx.CanStartSlide, "无蹲伏意图不可起滑");

            Input.CrouchIntent = true;
            Motor.SetVelocity(Vector3.forward * (Profile.slideEntryMinSpeed - 0.01f));
            Assert.IsFalse(Ctx.CanStartSlide, "低于门槛不可起滑（应退化为蹲伏）");

            Motor.SetVelocity(Vector3.forward * Profile.slideEntryMinSpeed);
            Assert.IsTrue(Ctx.CanStartSlide, "达到门槛即可起滑");
        }

        [Test]
        public void CanStartSlide_排除移动平台被动携带()
        {
            Input.CrouchIntent = true;
            // 平台带着玩家高速移动：合成速度很大，但自主速度为零
            Motor.SetVelocity(Vector3.zero, Vector3.forward * 20f);

            Assert.AreEqual(20f, Ctx.Motor.Velocity.magnitude, 1e-3f);
            Assert.IsFalse(Ctx.CanStartSlide, "被动携带不应触发起滑（用自主速度判定）");
        }

        [Test]
        public void CanStartSlide_只看水平分量()
        {
            Input.CrouchIntent = true;
            // 垂直高速 + 水平静止：不应起滑
            Motor.SetVelocity(Vector3.up * 30f);
            Assert.IsFalse(Ctx.CanStartSlide);
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
