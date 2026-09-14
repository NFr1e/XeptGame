using System.Collections.Generic;
using NUnit.Framework;

namespace XeptGame.Tests
{
    /// <summary>
    /// 时间权威测试（Time_Authority_Design.md T3）：<b>只验原因集合与边缘触发</b>——
    /// 通过注入假 <see cref="IPauseEffect"/> 完成，<b>不碰 <c>Time.timeScale</c>、不碰输入层</b>
    /// （真实效果出口是全局副作用，EditMode 里会污染编辑器状态；设计 §11 第 3 条）。
    /// </summary>
    public class TimeAuthorityTests
    {
        /// <summary>记录下发序列的假效果出口。</summary>
        private sealed class FakePauseEffect : IPauseEffect
        {
            /// <summary>按时间顺序记录的每次下发值。</summary>
            public readonly List<bool> Calls = new();

            /// <summary>最近一次下发值（无下发时为 null）。</summary>
            public bool? Last => Calls.Count == 0 ? null : Calls[^1];

            public void SetPaused(bool paused) => Calls.Add(paused);
        }

        private static (TimeAuthority Authority, FakePauseEffect Effect) New()
        {
            var effect = new FakePauseEffect();
            return (new TimeAuthority(effect), effect);
        }

        [Test]
        public void 初始_未暂停_不下发任何效果()
        {
            var (authority, effect) = New();

            Assert.IsFalse(authority.IsPaused);
            Assert.AreEqual(0, authority.ReasonCount);
            CollectionAssert.IsEmpty(effect.Calls, "初始不该下发（默认时基本来就是游戏进行中）");
        }

        [Test]
        public void 请求_暂停_下发一次true()
        {
            var (authority, effect) = New();

            Assert.IsTrue(authority.Request(PauseReason.Backpack));

            Assert.IsTrue(authority.IsPaused);
            CollectionAssert.AreEqual(new[] { true }, effect.Calls);
        }

        [Test]
        public void 重复请求同一原因_幂等_不重复下发()
        {
            var (authority, effect) = New();

            authority.Request(PauseReason.Backpack);
            Assert.IsFalse(authority.Request(PauseReason.Backpack), "第二次请求同一原因 = 无状态变化");

            Assert.AreEqual(1, authority.ReasonCount);
            CollectionAssert.AreEqual(new[] { true }, effect.Calls, "边缘触发：不得重复写 timeScale");
        }

        [Test]
        public void 两个原因_加第二个不重复下发_先释放一个仍暂停()
        {
            var (authority, effect) = New();

            authority.Request(PauseReason.Backpack);
            authority.Request(PauseReason.Menu);

            Assert.AreEqual(2, authority.ReasonCount);
            CollectionAssert.AreEqual(new[] { true }, effect.Calls, "集合仍非空 → 不是翻转，不下发");

            Assert.IsTrue(authority.Release(PauseReason.Menu));

            Assert.IsTrue(authority.IsPaused, "背包那个原因还在 → 仍暂停");
            CollectionAssert.AreEqual(new[] { true }, effect.Calls, "仍暂停 → 仍不下发");
        }

        [Test]
        public void 释放全部原因_恢复_下发一次false()
        {
            var (authority, effect) = New();

            authority.Request(PauseReason.Backpack);
            authority.Request(PauseReason.Menu);
            authority.Release(PauseReason.Menu);
            authority.Release(PauseReason.Backpack);

            Assert.IsFalse(authority.IsPaused);
            Assert.AreEqual(0, authority.ReasonCount);
            CollectionAssert.AreEqual(new[] { true, false }, effect.Calls);
        }

        [Test]
        public void 释放未请求的原因_无状态变化_无副作用()
        {
            var (authority, effect) = New();

            Assert.IsFalse(authority.Release(PauseReason.Menu), "从未请求过 → 不得恢复别人的暂停");
            Assert.IsFalse(authority.IsPaused);

            authority.Request(PauseReason.Backpack);
            Assert.IsFalse(authority.Release(PauseReason.Menu), "释放未登记的原因不得解除背包暂停");

            Assert.IsTrue(authority.IsPaused);
            CollectionAssert.AreEqual(new[] { true }, effect.Calls);
        }

        [Test]
        public void 请求释放多次往返_每次翻转各下发一次()
        {
            var (authority, effect) = New();

            authority.Request(PauseReason.Backpack);
            authority.Release(PauseReason.Backpack);
            authority.Request(PauseReason.Backpack);
            authority.Release(PauseReason.Backpack);

            CollectionAssert.AreEqual(new[] { true, false, true, false }, effect.Calls);
        }

        [Test]
        public void 强制恢复_清空全部原因并下发恢复()
        {
            var (authority, effect) = New();

            authority.Request(PauseReason.Backpack);
            authority.Request(PauseReason.Menu);
            authority.ForceResume();

            Assert.IsFalse(authority.IsPaused);
            Assert.AreEqual(0, authority.ReasonCount);
            Assert.AreEqual(false, effect.Last, "收尾必须把时基还原（否则编辑器停在 timeScale = 0）");
        }

        [Test]
        public void 无原因时强制恢复_仍下发一次恢复()
        {
            var (authority, effect) = New();

            authority.ForceResume();

            CollectionAssert.AreEqual(new[] { false }, effect.Calls, "收尾是无条件幂等还原，不做状态判断");
        }
    }
}
