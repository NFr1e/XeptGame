using NUnit.Framework;
using XeptGame.Items;

namespace XeptGame.Tests
{
    /// <summary>
    /// EquipTiming 配置语义测试：区分"单项 0（该项立即，合法）"与"双轴全 0（未配置，回退默认）"——
    /// 修正 IsSet 曾要求双轴都 &gt;0、导致 (0, X) 被整体回退的出入。
    /// </summary>
    public class EquipTimingTests
    {
        [Test]
        public void IsSet_双轴全0_视为未配置()
        {
            var t = new EquipTiming(0f, 0f);
            Assert.IsFalse(t.IsSet, "全 0 = 未配置（旧资产/留空）→ ResolvedTiming 回退默认");
        }

        [Test]
        public void IsSet_单项为0_视为已配置()
        {
            Assert.IsTrue(new EquipTiming(0f, 0.3f).IsSet, "draw=0（立即拿出）+ stow=0.3 应保留该项 0");
            Assert.IsTrue(new EquipTiming(0.35f, 0f).IsSet, "stow=0（立即收回）同样应保留");
        }

        [Test]
        public void 只读访问_返回构造值()
        {
            var t = new EquipTiming(0.5f, 0.2f);
            Assert.AreEqual(0.5f, t.DrawSeconds);
            Assert.AreEqual(0.2f, t.StowSeconds);
        }
    }
}
