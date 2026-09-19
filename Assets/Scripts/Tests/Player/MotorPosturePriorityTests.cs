using NUnit.Framework;
using UnityEngine;
using XeptGame.Player;

namespace XeptGame.Tests
{
    /// <summary>
    /// **意图优先级：奔跑 &gt; 蹲伏** 回归（设计决议 §2.2/§2.5）。
    /// 规则：蹲伏态收到奔跑意图（<see cref="PlayerMotorContext.CanSprint"/>）→ 可站时起身进 Sprint；
    /// 不可站（头顶阻挡）或无移动输入 → 保持蹲伏；蹲伏意图**未被终止**，奔跑结束后重申。
    /// 另含两条关键护栏：**不逐帧翻转**、**"蹲伏→奔跑"不误触滑铲**（后者是"滑铲进入改看瞬时意图"的直接动因）。
    /// </summary>
    public class MotorPosturePriorityTests : MotorTestBase
    {
        [Test]
        public void 蹲伏中奔跑意图_可站时起身进Sprint()
        {
            var fsm = NewCrouchingFsm();
            Input.SprintIntent = true; // 移动意图已由 NewCrouchingFsm 给出

            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(SprintState)));
            Assert.AreEqual(Profile.standingHeight, Motor.Capsule.Value.Height, 1e-4f, "起身应恢复站立胶囊");
        }

        [Test]
        public void 蹲伏中奔跑意图_头顶阻挡时保持蹲伏()
        {
            var fsm = NewCrouchingFsm();
            Motor.OverlapBlocked = true; // 低矮空间：站不起来
            Input.SprintIntent = true;

            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)), "头顶阻挡时即使想跑也保持蹲伏");
            Assert.AreEqual(Profile.crouchedHeight, Motor.Capsule.Value.Height, 1e-4f);
        }

        [Test]
        public void 蹲伏中奔跑意图_无移动输入时保持蹲伏()
        {
            var fsm = NewCrouchingFsm();
            Input.LocalMoveIntent = Vector3.zero; // 原地：不构成"允许奔跑"
            Input.SprintIntent = true;

            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)));
        }

        [Test]
        public void 蹲伏中释放蹲伏且可奔跑_直接进Sprint()
        {
            var fsm = NewCrouchingFsm();
            Input.SprintIntent = true;
            Input.CrouchIntent = false;

            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(SprintState)), "可奔跑时直接进 Sprint（不经过 Idle 抖动）");
        }

        [Test]
        public void 奔跑结束后_蹲伏意图重申回蹲伏()
        {
            var fsm = NewCrouchingFsm();
            Input.SprintIntent = true;
            Tick(fsm);
            Assert.IsTrue(fsm.IsInHierarchy(typeof(SprintState))); // 先进入奔跑

            Input.SprintIntent = false; // 停止奔跑；蹲伏意图从未终止
            Tick(fsm);                  // Sprint → Walk
            NewDispatcher(fsm).Dispatch();

            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)), "蹲伏意图仍在：奔跑结束后重新生效");
        }

        [Test]
        public void 同时保持两意图_不逐帧翻转()
        {
            var fsm = NewCrouchingFsm();
            Input.SprintIntent = true; // 蹲伏意图保持为真

            for (int i = 0; i < 10; i++)
            {
                Tick(fsm);
                NewDispatcher(fsm).Dispatch();

                Assert.IsTrue(fsm.IsInHierarchy(typeof(SprintState)),
                    $"第 {i} 帧应保持 Sprint（防调度器与 CrouchState 互相翻转）");
            }
        }

        [Test]
        public void 蹲伏到奔跑_不误触滑铲()
        {
            // 回归护栏：滑铲进入看**瞬时意图**——若改回持续蹲伏意图，本用例会滑铲（进而 滑铲↔奔跑 自激循环）
            var fsm = NewCrouchingFsm();
            Motor.SetVelocity(Vector3.forward * 8f); // 已经跑得比起滑门槛快
            Input.SprintIntent = true;

            for (int i = 0; i < 10; i++)
            {
                Tick(fsm);
                NewDispatcher(fsm).Dispatch();

                Assert.IsFalse(fsm.IsInHierarchy(typeof(SlideState)),
                    $"第 {i} 帧不应因持续蹲伏意图起滑（滑铲请求为瞬时意图）");
            }

            Assert.IsTrue(fsm.IsInHierarchy(typeof(SprintState)));
        }

        [Test]
        public void 再按一次蹲伏_仍可滑铲()
        {
            // 与上一条互补：优先级不吞掉滑铲本事——奔跑中**新发出**滑铲请求仍能起滑
            var fsm = NewCrouchingFsm();
            Motor.SetVelocity(Vector3.forward * 8f);
            Input.SprintIntent = true;
            Tick(fsm);
            Assert.IsTrue(fsm.IsInHierarchy(typeof(SprintState)));

            Input.SlideIntent = true; // 新的滑铲请求（蹲伏意图 0→1 的那一帧）
            NewDispatcher(fsm).Dispatch();

            Assert.IsTrue(fsm.IsInHierarchy(typeof(SlideState)));
        }
    }
}
