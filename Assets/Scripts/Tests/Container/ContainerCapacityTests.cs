using System.Collections.Generic;
using NUnit.Framework;
using XeptGame.Container;

namespace XeptGame.Tests
{
    /// <summary>
    /// ContainerCapacity（容量合成）测试（SlotStore_Design.md §6）：基础 + 各来源加法叠加、
    /// 同键覆盖、移除 no-op、幂等不通知、守卫、大数不溢出。
    /// </summary>
    public class ContainerCapacityTests
    {
        [Test]
        public void 解析值_基础加各来源()
        {
            var capacity = new ContainerCapacity(10);
            Assert.AreEqual(10, capacity.Resolved);

            capacity.SetSource("slot:backpack", 5);
            capacity.SetSource("slot:belt", 3);

            Assert.AreEqual(18, capacity.Resolved, "加法叠加（不是优先级择一）");
            Assert.AreEqual(2, capacity.SourceCount);
            Assert.AreEqual(10, capacity.BaseSlots);
        }

        [Test]
        public void 同键覆盖_移除未注册noop()
        {
            var capacity = new ContainerCapacity(4);

            capacity.SetSource("k", 2);
            Assert.AreEqual(6, capacity.Resolved);

            capacity.SetSource("k", 5);
            Assert.AreEqual(9, capacity.Resolved, "同键 = 覆盖更新（后写生效）");

            Assert.IsFalse(capacity.RemoveSource("other"), "移除未注册键 = no-op");
            Assert.IsTrue(capacity.RemoveSource("k"));
            Assert.AreEqual(4, capacity.Resolved);
        }

        [Test]
        public void 变化才通知_同值幂等()
        {
            var capacity = new ContainerCapacity(4);
            var values = new List<int>();
            capacity.Changed += values.Add;

            Assert.IsTrue(capacity.SetSource("k", 2));
            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(6, values[0]);

            Assert.IsFalse(capacity.SetSource("k", 2), "同值覆盖不通知");
            Assert.IsFalse(capacity.SetBaseSlots(4), "同值基础不通知");
            Assert.AreEqual(1, values.Count);

            Assert.IsTrue(capacity.SetBaseSlots(6));
            Assert.AreEqual(2, values.Count);
            Assert.AreEqual(8, values[1], "基础变化同样通知");
        }

        [Test]
        public void 守卫_非法输入抛错()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ContainerCapacity(-1));

            var capacity = new ContainerCapacity(1);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => capacity.SetBaseSlots(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => capacity.SetSource("k", 0));
            Assert.Throws<System.ArgumentNullException>(() => capacity.SetSource(null, 1));
            Assert.Throws<System.ArgumentNullException>(() => capacity.RemoveSource(null));
        }

        [Test]
        public void 大数_封顶不溢出()
        {
            var capacity = new ContainerCapacity(int.MaxValue);
            capacity.SetSource("k", int.MaxValue);

            Assert.AreEqual(int.MaxValue, capacity.Resolved, "long 累加后封顶，而不是溢出为负");
        }
    }
}
