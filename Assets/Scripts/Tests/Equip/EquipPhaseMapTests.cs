using System;
using NUnit.Framework;
using XeptGame.Equip;

namespace XeptGame.Tests
{
    /// <summary>EquipPhaseMap（相位 ⇄ 状态类型单表）往返一致性测试。</summary>
    public class EquipPhaseMapTests
    {
        [TestCase(EquipPhase.Empty, typeof(EmptyState))]
        [TestCase(EquipPhase.Stowed, typeof(StowedState))]
        [TestCase(EquipPhase.Drawing, typeof(DrawingState))]
        [TestCase(EquipPhase.Ready, typeof(ReadyState))]
        [TestCase(EquipPhase.Stowing, typeof(StowingState))]
        public void 相位到状态再到相位_往返一致(EquipPhase phase, Type state)
        {
            Assert.AreEqual(state, EquipPhaseMap.ToState(phase));
            Assert.AreEqual(phase, EquipPhaseMap.FromState(state));
        }

        [Test]
        public void 未知相位或类型_抛错不做中性默认()
        {
            Assert.Throws<ArgumentException>(() => EquipPhaseMap.ToState((EquipPhase)999));
            Assert.Throws<ArgumentException>(() => EquipPhaseMap.FromState(typeof(string)));
        }
    }
}
