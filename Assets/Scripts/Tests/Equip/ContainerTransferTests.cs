using NUnit.Framework;
using XeptGame.Container;
using XeptGame.Items;

namespace XeptGame.Tests
{
    /// <summary>
    /// ContainerTransfer.Move 哑原语测试（Equip_FPV_Design.md §3.3，T1）：
    /// 源取走 → 目标放入 → 失败回滚（原子）；自转无操作；不携带意图。
    /// </summary>
    public class ContainerTransferTests : EquipTestBase
    {
        [Test]
        public void Move_背包到空手_成功()
        {
            var bag = NewBag();
            var body = NewBody();
            var a = NewDef("item.a", true);
            bag.TryAdd(a, 5);

            Assert.IsTrue(ContainerTransfer.Move(bag, body, a, 1));

            Assert.AreEqual(4, bag.CountOf(a));
            Assert.AreSame(a, body.Get(XeptGame.Equip.BodySlotType.Hand));
        }

        [Test]
        public void Move_目标满_失败并回滚源()
        {
            var bag = NewBag();
            var body = NewBody();
            var held = NewDef("item.held", true);
            var a = NewDef("item.a", true);
            body.TryAdd(held, 1);
            bag.TryAdd(a, 5);

            Assert.IsFalse(ContainerTransfer.Move(bag, body, a, 1), "目标（手槽）占用中必须失败");

            Assert.AreEqual(5, bag.CountOf(a), "回滚：源原样退回，数量不变");
            Assert.AreSame(held, body.Get(XeptGame.Equip.BodySlotType.Hand), "回滚：目标占用不变");
        }

        [Test]
        public void Move_数量超过单位语义_失败并回滚()
        {
            var bag = NewBag();
            var body = NewBody();
            var a = NewDef("item.a", true);
            bag.TryAdd(a, 5);

            Assert.IsFalse(ContainerTransfer.Move(bag, body, a, 2), "身体槽单位制拒绝 count≠1");
            Assert.AreEqual(5, bag.CountOf(a), "回滚后源不变");
            Assert.IsTrue(body.IsEmpty(XeptGame.Equip.BodySlotType.Hand));
        }

        [Test]
        public void Move_源不足_失败不改动()
        {
            var bag = NewBag();
            var body = NewBody();
            var a = NewDef("item.a", true);
            bag.TryAdd(a, 2);

            Assert.IsFalse(ContainerTransfer.Move(bag, body, a, 3));
            Assert.AreEqual(2, bag.CountOf(a));
            Assert.IsTrue(body.IsEmpty(XeptGame.Equip.BodySlotType.Hand));
        }

        [Test]
        public void Move_源等于目标_视为无操作成功()
        {
            var body = NewBody();
            var a = NewDef("item.a", true);
            body.TryAdd(a, 1);

            Assert.IsTrue(ContainerTransfer.Move(body, body, a, 1), "自转应成功无副作用");
            Assert.AreSame(a, body.Get(XeptGame.Equip.BodySlotType.Hand));
        }

        [Test]
        public void Move_目标不可接纳_失败并回滚()
        {
            var bag = NewBag();
            var body = NewBody();
            var c = NewDef("item.c", false); // 不可持
            bag.TryAdd(c, 3);

            Assert.IsFalse(ContainerTransfer.Move(bag, body, c, 1));
            Assert.AreEqual(3, bag.CountOf(c));
            Assert.IsTrue(body.IsEmpty(XeptGame.Equip.BodySlotType.Hand));
        }
    }
}
