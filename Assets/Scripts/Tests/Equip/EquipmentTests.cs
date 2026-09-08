using System.Collections.Generic;
using NUnit.Framework;
using XeptGame.Equip;
using XeptGame.Inv;
using XeptGame.Items;

namespace XeptGame.Tests
{
    /// <summary>
    /// Equipment（身体容器）语义测试（Equip_FPV_Design.md §2.2/§3.2，T2）：
    /// 单位制（无视可堆叠、容量恒 1、空且接纳才 Add）、接纳谓词、按槽键控事件、容器面负载。
    /// </summary>
    public class EquipmentTests : EquipTestBase
    {
        [Test]
        public void 初始_手槽空()
        {
            var body = NewBody();

            Assert.IsNull(body.Get(BodySlotType.Hand));
            Assert.IsTrue(body.IsEmpty(BodySlotType.Hand));
            Assert.IsEmpty(body.Stacks);
            Assert.AreEqual(0, body.CountOf(NewDef("item.ghost", true)));
            Assert.IsFalse(body.Contains(NewDef("item.ghost2", true)));
        }

        [Test]
        public void TryAdd_可持单件_成功并双发事件()
        {
            var body = NewBody();
            var a = NewDef("item.a", true);
            var slots = new List<SlotChangeArgs>();
            var containerChanges = new List<InventoryChangeArgs>();
            body.SlotChanged += slots.Add;
            body.Changed += containerChanges.Add;

            Assert.IsTrue(body.TryAdd(a, 1));

            Assert.AreSame(a, body.Get(BodySlotType.Hand));
            Assert.IsFalse(body.IsEmpty(BodySlotType.Hand));
            Assert.AreEqual(1, body.Stacks.Count);
            Assert.AreSame(a, body.Stacks[0].Definition);
            Assert.AreEqual(1, body.Stacks[0].Count);
            Assert.AreEqual(1, body.CountOf(a));
            Assert.IsTrue(body.Contains(a));

            Assert.AreEqual(1, slots.Count, "槽级事件一条（按槽键控）");
            Assert.AreEqual(BodySlotType.Hand, slots[0].Slot);
            Assert.IsNull(slots[0].Old);
            Assert.AreSame(a, slots[0].New);

            Assert.AreEqual(1, containerChanges.Count, "容器级事件同源双发（单位 0→1）");
            Assert.AreSame(a, containerChanges[0].Item);
            Assert.AreEqual(0, containerChanges[0].OldCount);
            Assert.AreEqual(1, containerChanges[0].NewCount);
        }

        [Test]
        public void TryAdd_已占用_第二件失败不改动()
        {
            var body = NewBody();
            var a = NewDef("item.a", true);
            var b = NewDef("item.b", true);
            body.TryAdd(a, 1);
            var slotEvents = 0;
            body.SlotChanged += _ => slotEvents++;

            Assert.IsFalse(body.TryAdd(b, 1), "单位制：槽占用中不接受第二件");

            Assert.AreSame(a, body.Get(BodySlotType.Hand));
            Assert.AreEqual(1, body.Stacks.Count);
            Assert.AreEqual(0, body.CountOf(b));
            Assert.AreEqual(0, slotEvents, "失败不得发布事件");
        }

        [Test]
        public void TryAdd_数量非一_失败()
        {
            var body = NewBody();
            var a = NewDef("item.a", true);

            Assert.IsFalse(body.TryAdd(a, 2), "单位制：一次只收 1 单位");
            Assert.IsTrue(body.IsEmpty(BodySlotType.Hand));
        }

        [Test]
        public void TryAdd_不可持物_被接纳谓词拒绝()
        {
            var body = NewBody();
            var c = NewDef("item.c", false); // ResourceFacet，无 HoldableFacet

            Assert.IsFalse(body.TryAdd(c, 1));
            Assert.IsFalse(body.SlotAccepts(BodySlotType.Hand, c));
            Assert.IsTrue(body.IsEmpty(BodySlotType.Hand));
        }

        [Test]
        public void TryRemove_正确物_清空并双发事件()
        {
            var body = NewBody();
            var a = NewDef("item.a", true);
            body.TryAdd(a, 1);
            var slots = new List<SlotChangeArgs>();
            var containerChanges = new List<InventoryChangeArgs>();
            body.SlotChanged += slots.Add;
            body.Changed += containerChanges.Add;

            Assert.IsTrue(body.TryRemove(a, 1));

            Assert.IsNull(body.Get(BodySlotType.Hand));
            Assert.IsTrue(body.IsEmpty(BodySlotType.Hand));
            Assert.IsEmpty(body.Stacks);
            Assert.AreEqual(0, body.CountOf(a));

            Assert.AreEqual(1, slots.Count);
            Assert.AreSame(a, slots[0].Old);
            Assert.IsNull(slots[0].New);

            Assert.AreEqual(1, containerChanges.Count);
            Assert.AreEqual(1, containerChanges[0].OldCount);
            Assert.AreEqual(0, containerChanges[0].NewCount);
        }

        [Test]
        public void TryRemove_异物或数量非一_失败不改动()
        {
            var body = NewBody();
            var a = NewDef("item.a", true);
            var b = NewDef("item.b", true);
            body.TryAdd(a, 1);

            Assert.IsFalse(body.TryRemove(b, 1));
            Assert.IsFalse(body.TryRemove(a, 2));

            Assert.AreSame(a, body.Get(BodySlotType.Hand));
            Assert.AreEqual(1, body.CountOf(a));
        }
    }
}
