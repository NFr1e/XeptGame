using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Container;
using XeptGame.Items;

namespace XeptGame.Tests
{
    /// <summary>
    /// 槽位抽象与容器共享机制测试（SlotStore_Design.md §2/§3）：
    /// 槽持状态 + 双门控（种类/数目）、原子失败零改动、容器全量语义（装不下整批拒绝）、
    /// 跨槽分配与聚合、两条事件轨、提交门（提交区内的写请求被拒）。
    /// 注意：槽写口是 internal，测试程序集无法绕过容器写槽——本测试全部经容器公开面驱动（即纪律本身）。
    /// </summary>
    public class SlotTests
    {
        private readonly List<Object> _owned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _owned)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _owned.Clear();
        }

        [Test]
        public void 单位槽_第二件被拒_零改动无事件()
        {
            var bag = new Bag(new SlotBase[] { new UnitSlot(0) });
            var a = NewDef("item.a");
            var b = NewDef("item.b");
            var slotEvents = 0;
            bag.SlotChanged += _ => slotEvents++;

            Assert.IsTrue(bag.TryAdd(a, 1));
            slotEvents = 0;

            Assert.IsFalse(bag.TryAdd(b, 1), "单位槽占用中不接受第二件");
            Assert.AreEqual(1, bag.CountOf(a));
            Assert.AreEqual(0, bag.CountOf(b));
            Assert.AreEqual(0, slotEvents, "失败不得发布事件");
        }

        [Test]
        public void 堆叠槽_先并入未满槽再占新槽()
        {
            var bag = new Bag(new SlotBase[] { new StackSlot(0, 3), new StackSlot(1, 3) });
            var a = NewDef("item.a");

            Assert.IsTrue(bag.TryAdd(a, 5));

            Assert.AreEqual(5, bag.CountOf(a), "跨槽聚合求和");
            Assert.AreEqual(3, bag.Slots[0].Count, "先填满第一个槽");
            Assert.AreEqual(2, bag.Slots[1].Count, "余量落下一个槽");

            Assert.IsTrue(bag.TryAdd(a, 1));
            Assert.AreEqual(3, bag.Slots[1].Count, "再次分配仍先并入未满槽");
        }

        [Test]
        public void 容器_装不下_整批拒绝零改动()
        {
            var bag = new Bag(new SlotBase[] { new StackSlot(0, 3), new StackSlot(1, 3) });
            var a = NewDef("item.a");

            Assert.IsFalse(bag.TryAdd(a, 7), "全量语义：容量不足整批拒绝");

            Assert.AreEqual(0, bag.CountOf(a));
            Assert.IsTrue(bag.Slots[0].IsEmpty);
            Assert.IsTrue(bag.Slots[1].IsEmpty);
        }

        [Test]
        public void 容器_移除原子_不足不改动()
        {
            var bag = new Bag(new SlotBase[] { new StackSlot(0, 3), new StackSlot(1, 3) });
            var a = NewDef("item.a");
            bag.TryAdd(a, 5);

            Assert.IsFalse(bag.TryRemove(a, 6), "总量不足必须失败");
            Assert.AreEqual(5, bag.CountOf(a), "原子性：不足时不得扣减");
        }

        [Test]
        public void 容器_移除按槽序扣减_扣至零即清槽()
        {
            var bag = new Bag(new SlotBase[] { new StackSlot(0, 3), new StackSlot(1, 3) });
            var a = NewDef("item.a");
            bag.TryAdd(a, 5); // [3, 2]

            Assert.IsTrue(bag.TryRemove(a, 4));

            Assert.AreEqual(1, bag.CountOf(a));
            Assert.IsTrue(bag.Slots[0].IsEmpty, "按槽序先扣，扣至 0 即视为空");
            Assert.AreEqual(1, bag.Slots[1].Count);
        }

        [Test]
        public void 事件_成功发两条轨_失败不发()
        {
            var bag = new Bag(new SlotBase[] { new StackSlot(0, 3), new StackSlot(1, 3) });
            var a = NewDef("item.a");
            var slotEvents = new List<SlotChangeArgs>();
            var containerEvents = new List<ContainerChangeArgs>();
            bag.SlotChanged += slotEvents.Add;
            bag.Changed += containerEvents.Add;

            Assert.IsTrue(bag.TryAdd(a, 4));

            Assert.AreEqual(2, slotEvents.Count, "槽级轨：每个实际变化的槽一条");
            Assert.AreEqual(new SlotId(0), slotEvents[0].Slot);
            Assert.AreEqual(0, slotEvents[0].OldCount);
            Assert.AreEqual(3, slotEvents[0].NewCount);
            Assert.AreEqual(new SlotId(1), slotEvents[1].Slot);
            Assert.AreEqual(1, slotEvents[1].NewCount);

            Assert.AreEqual(1, containerEvents.Count, "聚合轨：提交点按物品一条");
            Assert.AreSame(a, containerEvents[0].Item);
            Assert.AreEqual(0, containerEvents[0].OldCount);
            Assert.AreEqual(4, containerEvents[0].NewCount);

            slotEvents.Clear();
            containerEvents.Clear();
            Assert.IsFalse(bag.TryAdd(a, 100));
            Assert.AreEqual(0, slotEvents.Count);
            Assert.AreEqual(0, containerEvents.Count);
        }

        [Test]
        public void 提交门_槽事件回调中的写请求被拒()
        {
            var bag = new Bag(new SlotBase[] { new StackSlot(0, 3), new StackSlot(1, 3) });
            var a = NewDef("item.a");
            var c = NewDef("item.c");
            var rejectedInsideCommit = 0;

            // 提交区内（含事件派发）的写请求一律被拒
            bag.SlotChanged += _ =>
            {
                if (!bag.TryAdd(c, 1))
                {
                    rejectedInsideCommit++;
                }
            };

            Assert.IsTrue(bag.TryAdd(a, 1));
            Assert.AreEqual(1, rejectedInsideCommit, "提交区内不得插入第二笔写");
            Assert.AreEqual(0, bag.CountOf(c));

            // 提交结束后写恢复正常（本次提交自身的事件回调仍处于提交区，故计数再 +1）
            Assert.IsTrue(bag.TryAdd(c, 1));
            Assert.AreEqual(1, bag.CountOf(c));
            Assert.AreEqual(2, rejectedInsideCommit);
        }

        [Test]
        public void 聚合视图_同物品合并_保持首现序()
        {
            var bag = new Bag(new SlotBase[] { new StackSlot(0, 3), new StackSlot(1, 3) });
            var a = NewDef("item.a");
            var b = NewDef("item.b");
            bag.TryAdd(a, 1);
            bag.TryAdd(b, 1);
            bag.TryAdd(a, 1);

            var rows = bag.Stacks;
            Assert.AreEqual(2, rows.Count);
            Assert.AreSame(a, rows[0].Definition);
            Assert.AreEqual(2, rows[0].Count, "同物品跨槽合并为一行");
            Assert.AreSame(b, rows[1].Definition);
        }

        [Test]
        public void 容器_重复槽身份_装配抛错()
        {
            Assert.Throws<System.ArgumentException>(
                () => new Bag(new SlotBase[] { new UnitSlot(0), new UnitSlot(0) }));
        }

        private ItemDefinition NewDef(string id)
        {
            // 容器按资产引用标识物品（ReferenceEquals），id 仅命名参考；假槽不读 Facet，无需注入
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            _owned.Add(def);
            return def;
        }

        /// <summary>假单位槽（容量恒 1，接纳任意物品）。</summary>
        private sealed class UnitSlot : SlotBase
        {
            public UnitSlot(int id) : base(new SlotId(id))
            {
            }

            public override bool Accepts(ItemDefinition definition) => definition != null;

            public override int CapacityFor(ItemDefinition definition) => definition == null ? 0 : 1;
        }

        /// <summary>假堆叠槽（每格上限给定）。</summary>
        private sealed class StackSlot : SlotBase
        {
            private readonly int _capacity;

            public StackSlot(int id, int capacity) : base(new SlotId(id)) => _capacity = capacity;

            public override bool Accepts(ItemDefinition definition) => definition != null;

            public override int CapacityFor(ItemDefinition definition) => definition == null ? 0 : _capacity;
        }

        /// <summary>可实例化的槽容器（生产实现 = Equipment / SlotStore）。</summary>
        private sealed class Bag : SlotContainer
        {
            public Bag(IReadOnlyList<SlotBase> slots) : base(slots)
            {
            }
        }
    }
}
