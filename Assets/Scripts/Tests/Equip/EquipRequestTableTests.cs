using NUnit.Framework;
using XeptGame.Equip;

namespace XeptGame.Tests
{
    /// <summary>行为请求转移表矩阵测试（行为文档 §3.2 的数据化；规则收口 EquipRequestTable 的回归护栏）。</summary>
    public class EquipRequestTableTests
    {
        [TestCase(EquipPhase.Empty, BehaviorRequest.Rejected)]
        [TestCase(EquipPhase.Stowed, BehaviorRequest.Started)]
        [TestCase(EquipPhase.Drawing, BehaviorRequest.AlreadyInProgress)]
        [TestCase(EquipPhase.Ready, BehaviorRequest.AlreadySatisfied)]
        [TestCase(EquipPhase.Stowing, BehaviorRequest.Rejected)]
        public void Draw_各相位(EquipPhase from, BehaviorRequest expect)
        {
            var outcome = EquipRequestTable.Evaluate(from, EquipRequest.Draw);

            Assert.AreEqual(expect, outcome.Reply);
            if (expect == BehaviorRequest.Started)
            {
                Assert.AreEqual(EquipPhase.Drawing, outcome.Target);
            }
        }

        [TestCase(EquipPhase.Empty, BehaviorRequest.AlreadySatisfied)]
        [TestCase(EquipPhase.Stowed, BehaviorRequest.AlreadySatisfied)]
        [TestCase(EquipPhase.Drawing, BehaviorRequest.Started)]
        [TestCase(EquipPhase.Ready, BehaviorRequest.Started)]
        [TestCase(EquipPhase.Stowing, BehaviorRequest.AlreadyInProgress)]
        public void Stow_各相位(EquipPhase from, BehaviorRequest expect)
        {
            var outcome = EquipRequestTable.Evaluate(from, EquipRequest.Stow);

            Assert.AreEqual(expect, outcome.Reply);
            if (expect == BehaviorRequest.Started)
            {
                Assert.AreEqual(EquipPhase.Stowing, outcome.Target);
            }
        }
    }
}
