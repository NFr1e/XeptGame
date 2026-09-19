using NUnit.Framework;
using UnityEngine;
using XeptGame.Player;

namespace XeptGame.Tests
{
    /// <summary>
    /// <see cref="MotorActionDispatcher"/> 准入与优先级回归（设计决议 §2.4/§2.5）：
    /// 动作优先级 slide &gt; crouch &gt; jump、能力 Marker 准入、退化路径（速度不足 → 蹲伏）、
    /// 蹲禁跳/滑铲可跳、跳跃消耗与平台垂直冲量、以及"调度器不越权做状态内退出"的边界。
    /// </summary>
    public class MotorActionDispatcherTests : MotorTestBase
    {
        [Test]
        public void 未进入初始状态_不动作()
        {
            var fsm = NewFsm();

            NewDispatcher(fsm).Dispatch();

            Assert.IsNull(fsm.CurrentState, "无当前状态时调度器应直接返回");
        }

        [Test]
        public void 蹲伏意图_从Idle进入Crouch()
        {
            var fsm = NewGroundedFsm();
            Input.CrouchIntent = true;

            NewDispatcher(fsm).Dispatch();

            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)));
        }

        [Test]
        public void 蹲伏保持_重复请求不打断蹲伏()
        {
            var fsm = NewGroundedFsm();
            Input.CrouchIntent = true;
            var dispatcher = NewDispatcher(fsm);

            dispatcher.Dispatch();
            dispatcher.Dispatch();
            dispatcher.Dispatch();

            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)));
        }

        [Test]
        public void 冲刺且速度达门槛_按蹲进入Slide而非Crouch()
        {
            var fsm = NewSprintingFsm(8f);
            Input.CrouchIntent = true;
            Input.SlideIntent = true;

            NewDispatcher(fsm).Dispatch();

            Assert.IsTrue(fsm.IsInHierarchy(typeof(SlideState)), "滑铲优先级高于蹲伏");
            Assert.IsFalse(fsm.IsInHierarchy(typeof(CrouchState)));
        }

        [Test]
        public void 冲刺中速度不足门槛_不滑也不蹲_保持奔跑()
        {
            // 意图优先级：奔跑 > 蹲伏（蹲伏不接入）；同时滑铲因速度不足不成立 → 保持奔跑
            var fsm = NewSprintingFsm(Profile.slideEntryMinSpeed - 1f);
            Input.CrouchIntent = true;
            Input.SlideIntent = true;

            NewDispatcher(fsm).Dispatch();

            Assert.IsTrue(fsm.IsInHierarchy(typeof(SprintState)));
            Assert.IsFalse(fsm.IsInHierarchy(typeof(SlideState)));
            Assert.IsFalse(fsm.IsInHierarchy(typeof(CrouchState)), "奔跑意图激活时不接入蹲伏");
        }

        [Test]
        public void 奔跑意图激活_持续蹲伏意图不接入()
        {
            var fsm = NewSprintingFsm(8f);
            Input.CrouchIntent = true; // 持续意图（无滑铲请求）

            NewDispatcher(fsm).Dispatch();

            Assert.IsTrue(fsm.IsInHierarchy(typeof(SprintState)), "意图优先级：奔跑 > 蹲伏");
        }

        [Test]
        public void 接地跳_冲量含平台垂直分量并转Airborne()
        {
            var fsm = NewGroundedFsm();
            Motor.SetVelocity(Vector3.forward * 2f, Vector3.up * 3f); // 升降平台携带
            Input.JumpIntent = true;

            NewDispatcher(fsm).Dispatch();

            Assert.IsTrue(fsm.IsInHierarchy(typeof(AirborneState)));
            Assert.IsTrue(fsm.IsInHierarchy(typeof(FallState)), "Airborne 初始子状态应为 Fall");
            Assert.IsTrue(Ctx.JumpConsumed);
            Assert.AreEqual(1, Motor.ForceUngroundCount);
            Assert.AreEqual(Profile.jumpUpSpeed + 3f, Ctx.PendingJumpImpulse.y, 1e-3f,
                "冲量应并入平台垂直速度（防升降平台起跳丢 Y 惯性）");
            Assert.AreEqual(0f, Ctx.PendingJumpImpulse.x, 1e-3f);
            Assert.AreEqual(0f, Ctx.PendingJumpImpulse.z, 1e-3f);
        }

        [Test]
        public void 蹲伏中按跳_不跳()
        {
            var fsm = NewGroundedFsm();
            Input.CrouchIntent = true;
            var dispatcher = NewDispatcher(fsm);
            dispatcher.Dispatch();
            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)));

            Input.JumpIntent = true;
            dispatcher.Dispatch();

            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)), "Crouch 不实现 IJumpable：蹲禁跳");
            Assert.IsFalse(Ctx.JumpConsumed);
            Assert.AreEqual(0, Motor.ForceUngroundCount);
        }

        [Test]
        public void 同帧蹲与跳_蹲优先()
        {
            var fsm = NewGroundedFsm();
            Input.CrouchIntent = true;
            Input.JumpIntent = true;

            NewDispatcher(fsm).Dispatch();

            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)));
            Assert.IsFalse(Ctx.JumpConsumed, "蹲伏优先时不应消耗跳跃");
        }

        [Test]
        public void 跳跃已消耗_不再跳()
        {
            var fsm = NewGroundedFsm();
            Ctx.JumpConsumed = true;
            Input.JumpIntent = true;

            NewDispatcher(fsm).Dispatch();

            Assert.IsTrue(fsm.IsInHierarchy(typeof(GroundedState)));
            Assert.AreEqual(0, Motor.ForceUngroundCount);
        }

        [Test]
        public void 滑铲中按跳_允许滑铲跳()
        {
            var fsm = NewSlidingFsm(8f);
            Input.JumpIntent = true;

            NewDispatcher(fsm).Dispatch();

            Assert.IsTrue(fsm.IsInHierarchy(typeof(AirborneState)), "Slide 实现 IJumpable：滑铲可跳");
            Assert.IsTrue(Ctx.JumpConsumed);
            Assert.AreEqual(1, Motor.ForceUngroundCount);
        }

        [Test]
        public void 滑铲中蹲伏意图保持_滑铲不被打断()
        {
            // 回归护栏：Slide **不实现 ICrouchable**——否则调度器的蹲伏动作会把滑铲立刻打断（Slide→Crouch 非幂等）
            var fsm = NewSlidingFsm(8f);
            Input.CrouchIntent = true;

            NewDispatcher(fsm).Dispatch();

            Assert.IsTrue(fsm.IsInHierarchy(typeof(SlideState)));
        }

        [Test]
        public void 滑铲意图终止_退出由状态自身处理_调度器不越权()
        {
            var fsm = NewSlidingFsm(8f);
            Input.CrouchIntent = false;

            NewDispatcher(fsm).Dispatch();
            Assert.IsTrue(fsm.IsInHierarchy(typeof(SlideState)), "调度器不负责滑铲退出（保持/退出归 SlideState）");

            Tick(fsm);
            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)), "SlideState.Update 读到意图终止后退出到 Crouch");
        }

        [Test]
        public void 空中_调度器不触发蹲伏与滑铲()
        {
            var fsm = NewGroundedFsm();
            SetAirborneGround();
            Tick(fsm);
            Assert.IsTrue(fsm.IsInHierarchy(typeof(FallState)));

            Input.CrouchIntent = true;
            NewDispatcher(fsm).Dispatch();

            Assert.IsTrue(fsm.IsInHierarchy(typeof(AirborneState)), "空中按蹲不应进蹲（Fall 非 ICrouchable/ISlidable）");
            Assert.IsFalse(fsm.IsInHierarchy(typeof(CrouchState)));
            Assert.IsFalse(fsm.IsInHierarchy(typeof(SlideState)));
        }
    }
}
