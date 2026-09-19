using NUnit.Framework;
using UnityEngine;
using XeptGame.Player;

namespace XeptGame.Tests
{
    /// <summary>
    /// **冲刺方向准入（仅前半球）与滑铲"只有前向/侧向"** 回归（设计决议 §2.2/§2.5）。
    /// 决议（取代早期"后半球禁止、侧向允许"版本）：**只有前半球可冲刺**（含前斜向）——
    /// **纯侧向与后向一律降级 Walk**（依据《孤岛惊魂 6》一手实测：其禁止后向冲刺）。
    /// 物理承诺由两侧共同保证：**准入侧**（侧向/后向无法进入 Sprint ⇒ 速度低于起滑门槛 ⇒ 无法起滑）与
    /// **维持侧**（起滑锁定方向 + 转向剔后向 ⇒ 滑铲中途不被转向背向；滑铲**内**仍可侧向转向）。
    /// </summary>
    public class MotorSprintDirectionTests : MotorTestBase
    {
        // ============================================================
        // 冲刺方向准入（CanSprint）
        // ============================================================

        [Test]
        public void CanSprint_后向输入为假()
        {
            Input.SprintIntent = true;
            Input.LocalMoveIntent = Vector3.back;
            Input.MoveInput = new Vector2(0f, -1f); // 正后：后半球

            Assert.IsFalse(Ctx.CanSprint, "后半球禁止冲刺");
        }

        [Test]
        public void CanSprint_后斜向为假()
        {
            Input.SprintIntent = true;
            Input.LocalMoveIntent = Vector3.back + Vector3.left;
            Input.MoveInput = new Vector2(-1f, -1f); // 后斜：仍在后半球

            Assert.IsFalse(Ctx.CanSprint);
        }

        [Test]
        public void CanSprint_前向与前斜向为真()
        {
            Input.SprintIntent = true;

            Input.MoveInput = new Vector2(0f, 1f); // 正前
            Input.LocalMoveIntent = Vector3.forward;
            Assert.IsTrue(Ctx.CanSprint);

            Input.MoveInput = new Vector2(1f, 1f); // 前斜：仍在前半球
            Input.LocalMoveIntent = (Vector3.forward + Vector3.right).normalized;
            Assert.IsTrue(Ctx.CanSprint, "前斜向算前半球，可冲刺");
        }

        [Test]
        public void CanSprint_纯侧向为假()
        {
            Input.SprintIntent = true;
            Input.MoveInput = new Vector2(1f, 0f); // 纯侧向（x 轴方向）
            Input.LocalMoveIntent = Vector3.right;

            Assert.IsFalse(Ctx.CanSprint, "只有前半球可冲刺：纯侧向降级 Walk（决议变更）");
        }

        [Test]
        public void CanSprint_中性带内不误判()
        {
            Input.SprintIntent = true;
            Input.LocalMoveIntent = Vector3.forward;

            Input.MoveInput = new Vector2(0f, 0.05f); // 未达前向阈值
            Assert.IsFalse(Ctx.CanSprint, "带内按未表达前向意图处理（避免摇杆抖动引起 Sprint↔Walk 抖动）");

            Input.MoveInput = new Vector2(0f, -0.05f); // 后侧带内
            Assert.IsFalse(Ctx.CanSprint);

            Input.MoveInput = new Vector2(0f, 0.2f); // 超过中性带
            Assert.IsTrue(Ctx.CanSprint);
        }

        [Test]
        public void 冲刺中按后退_立即降级Walk()
        {
            var fsm = NewSprintingFsm(8f);
            Input.MoveInput = new Vector2(0f, -1f);

            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(WalkState)));
            Assert.IsFalse(fsm.IsInHierarchy(typeof(SprintState)), "后半球禁止冲刺");
        }

        [Test]
        public void 冲刺中按纯侧向_立即降级Walk()
        {
            var fsm = NewSprintingFsm(8f);
            Input.MoveInput = new Vector2(1f, 0f);
            Input.LocalMoveIntent = Vector3.right;

            Tick(fsm);

            Assert.IsTrue(fsm.IsInHierarchy(typeof(WalkState)));
            Assert.IsFalse(fsm.IsInHierarchy(typeof(SprintState)), "纯侧向不可冲刺（决议变更）");
        }

        [Test]
        public void 后向输入_即使残余动量很高也无法起滑()
        {
            var fsm = NewGroundedFsm();
            Input.SprintIntent = true;
            Input.LocalMoveIntent = Vector3.back;
            Input.MoveInput = new Vector2(0f, -1f);
            Motor.SetVelocity(Vector3.back * 8f); // 模拟刚从冲刺降级、残余动量仍高于起滑门槛

            Tick(fsm);
            Assert.IsTrue(fsm.IsInHierarchy(typeof(WalkState)), "后向输入应降级为 Walk");

            Input.CrouchIntent = true;
            Input.SlideIntent = true;
            NewDispatcher(fsm).Dispatch();

            Assert.IsFalse(fsm.IsInHierarchy(typeof(SlideState)), "后向不得起滑（准入侧：不在 Sprint 态）");
            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)), "后向按蹲 = 普通蹲伏");
        }

        [Test]
        public void 侧向输入_不可起滑()
        {
            var fsm = NewSprintingFsm(8f);
            Input.MoveInput = new Vector2(1f, 0f); // 纯侧向：冲刺准入被拒
            Input.LocalMoveIntent = Vector3.right;
            Input.CrouchIntent = true;
            Input.SlideIntent = true;

            Tick(fsm); // Sprint → Walk（准入失效）
            Assert.IsTrue(fsm.IsInHierarchy(typeof(WalkState)), "纯侧向应降级 Walk");

            NewDispatcher(fsm).Dispatch();

            Assert.IsFalse(fsm.IsInHierarchy(typeof(SlideState)),
                "无侧向冲刺 ⇒ 无侧向滑铲（滑铲需 Sprint 态；侧向转向仍可在滑铲内进行）");
            Assert.IsTrue(fsm.IsInHierarchy(typeof(CrouchState)), "侧向按蹲 = 普通蹲伏");
        }

        // ============================================================
        // 滑铲维持侧：方向锁定 + 转向剔后向
        // ============================================================

        [Test]
        public void 滑铲进入_锁定起滑方向()
        {
            var fsm = NewSlidingFsm(8f);

            Assert.That(Vector3.Angle(Ctx.SlideDirection, Vector3.forward), Is.LessThan(0.01f));
        }

        [Test]
        public void 滑铲中纯后向输入_不转向()
        {
            var fsm = NewSlidingFsm(8f);
            Input.LocalMoveIntent = Vector3.back;
            Input.MoveInput = new Vector2(0f, -1f);

            var velocity = Vector3.forward * 8f;
            for (int i = 0; i < 10; i++)
            {
                Tick(fsm);
                LeafOf(fsm).ApplyVelocity(ref velocity, 0.05f);

                Assert.LessOrEqual(Vector3.Angle(velocity, Vector3.forward), 90f,
                    $"第 {i} 帧滑铲方向不应越过侧向（前半球约束）");
            }

            Assert.That(Vector3.Angle(velocity, Vector3.forward), Is.LessThan(0.01f),
                "纯后向输入不应改变滑行方向（反向蹬地不改变动量方向）");
            Assert.IsTrue(fsm.IsInHierarchy(typeof(SlideState)));
        }

        [Test]
        public void 滑铲中后斜向输入_转向被限制在前半球()
        {
            var fsm = NewSlidingFsm(8f);
            Input.LocalMoveIntent = (Vector3.back + Vector3.right).normalized;
            Input.MoveInput = new Vector2(1f, -1f); // 后斜：后向分量应被剔除

            var velocity = Vector3.forward * 8f;
            for (int i = 0; i < 12; i++)
            {
                Tick(fsm);
                LeafOf(fsm).ApplyVelocity(ref velocity, 0.05f);

                Assert.LessOrEqual(Vector3.Angle(velocity, Vector3.forward), 90.01f,
                    $"第 {i} 帧方向不应越过侧向（后向分量被剔除）");
            }

            Assert.Greater(Vector3.Angle(velocity, Vector3.forward), 60f,
                "侧向分量仍应生效（滑铲可侧向转向）");
        }
    }
}
