using System.Collections.Generic;
using NUnit.Framework;
using XeptGame.Container;
using XeptGame.Inv;
using XeptGame.Items;

namespace XeptGame.Tests
{
    /// <summary>
    /// 槽载荷"实例行"测试（Item_Instance_Design.md §2.2/§2.3，不变量 I1/I2）：
    /// 数量恒 1 且不参与合并；聚合面包含实例行、按定义写入跳过实例行；事件两轨都带实例；
    /// 按格搬运（含跨容器与回滚）；压缩/缩容对实例行同规格搬运。
    /// 假 Definition 与容器工厂来自 <see cref="EquipTestBase"/>（反射注入 Facet 的既有先例）。
    /// </summary>
    public class SlotInstancePayloadTests : EquipTestBase
    {
        [Test]
        public void 实例占格_数量恒一_且拒绝按定义合并()
        {
            var bag = new Inventory(1);
            var def = NewDef("item.a", false);
            var instance = new ItemInstance(1, def);

            Assert.IsTrue(bag.TryPlaceInstanceAt(new SlotId(0), instance));

            Assert.AreEqual(1, bag.Slots[0].Count, "实例行数量恒 1");
            Assert.IsTrue(bag.Slots[0].HasInstance);
            Assert.AreSame(instance, bag.Slots[0].Instance);

            Assert.IsFalse(bag.TryAdd(def, 1), "实例行不参与合并，且容器已无空格");
            Assert.AreSame(instance, bag.Slots[0].Instance, "失败零改动");
        }

        [Test]
        public void 定向放入实例_非空格或异类_拒绝且零改动()
        {
            var bag = new Inventory(2);
            var a = NewDef("item.a", false);
            var b = NewDef("item.b", false);
            Assert.IsTrue(bag.TryAdd(a, 1));

            Assert.IsFalse(bag.TryPlaceInstanceAt(new SlotId(0), new ItemInstance(1, a)), "同定义但非空格：拒绝");
            Assert.IsTrue(bag.TryPlaceInstanceAt(new SlotId(1), new ItemInstance(2, b)), "空格且接纳：成功");
            Assert.IsFalse(bag.TryPlaceInstanceAt(new SlotId(1), new ItemInstance(3, b)), "同格第二个实例：拒绝");
            Assert.IsFalse(bag.TryPlaceInstanceAt(new SlotId(9), new ItemInstance(4, b)), "不存在的格：拒绝");

            Assert.AreEqual(1, bag.Slots[0].Count);
            Assert.IsFalse(bag.Slots[0].HasInstance, "失败零改动");
            Assert.AreSame(b, bag.Slots[1].Item);
            Assert.AreEqual(2, bag.Slots[1].Instance.Id, "撞格被拒后仍是先放进去的那个实例");
        }

        [Test]
        public void 取实例_空格或无状态行_失败且零改动()
        {
            var bag = new Inventory(2);
            var def = NewDef("item.a", false);
            Assert.IsTrue(bag.TryAdd(def, 2));

            Assert.IsFalse(bag.TryTakeInstanceAt(new SlotId(0), out var none));
            Assert.IsNull(none);
            Assert.IsFalse(bag.TryTakeInstanceAt(new SlotId(1), out _), "不存在的格");
            Assert.AreEqual(2, bag.CountOf(def));
            Assert.IsFalse(bag.Slots[0].HasInstance);
        }

        [Test]
        public void 聚合面_计入实例行_按定义写入跳过实例行()
        {
            var bag = new Inventory(2);
            var def = NewDef("item.a", false);
            var instance = new ItemInstance(9, def);
            Assert.IsTrue(bag.TryPlaceInstanceAt(new SlotId(0), instance));
            Assert.IsTrue(bag.TryAdd(def, 3), "无状态行落到 1 号格（实例行不参与合并）");

            Assert.AreEqual(4, bag.CountOf(def), "聚合 = 3（无状态）+ 1（实例）");
            Assert.IsTrue(bag.Contains(def));
            Assert.AreEqual(1, bag.Stacks.Count, "同定义聚合为一行");
            Assert.AreEqual(4, bag.Stacks[0].Count);

            Assert.IsFalse(bag.TryRemove(def, 4), "可写口径只有 3：整笔失败");
            Assert.IsTrue(bag.TryRemove(def, 3));
            Assert.AreEqual(1, bag.CountOf(def), "只扣掉无状态的那 3 个");
            Assert.IsTrue(bag.Slots[0].HasInstance, "实例行未被动");
            Assert.IsTrue(bag.Slots[1].IsEmpty);
        }

        [Test]
        public void 事件_槽级负载携带实例前后句柄()
        {
            var bag = new Inventory(1);
            var def = NewDef("item.a", false);
            var instance = new ItemInstance(3, def);
            var events = new List<SlotChangeArgs>();
            bag.SlotChanged += events.Add;

            Assert.IsTrue(bag.TryPlaceInstanceAt(new SlotId(0), instance));

            Assert.AreEqual(1, events.Count);
            Assert.IsNull(events[0].Old);
            Assert.IsNull(events[0].OldInstance);
            Assert.AreSame(def, events[0].New);
            Assert.AreEqual(1, events[0].NewCount);
            Assert.AreSame(instance, events[0].NewInstance);

            events.Clear();
            Assert.IsTrue(bag.TryTakeInstanceAt(new SlotId(0), out var taken));

            Assert.AreSame(instance, taken);
            Assert.AreEqual(1, events.Count);
            Assert.AreSame(def, events[0].Old);
            Assert.AreSame(instance, events[0].OldInstance);
            Assert.IsNull(events[0].New);
            Assert.AreEqual(0, events[0].NewCount);
            Assert.IsNull(events[0].NewInstance);
        }

        [Test]
        public void 事件_聚合轨以实例各计一()
        {
            var bag = new Inventory(1);
            var def = NewDef("item.a", false);
            var changes = new List<ContainerChangeArgs>();
            bag.Changed += changes.Add;

            Assert.IsTrue(bag.TryPlaceInstanceAt(new SlotId(0), new ItemInstance(1, def)));
            Assert.AreEqual(1, changes.Count);
            Assert.AreSame(def, changes[0].Item);
            Assert.AreEqual(0, changes[0].OldCount);
            Assert.AreEqual(1, changes[0].NewCount, "聚合口径：实例计 1");

            Assert.IsTrue(bag.TryTakeInstanceAt(new SlotId(0), out _));
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(1, changes[1].OldCount);
            Assert.AreEqual(0, changes[1].NewCount);
        }

        [Test]
        public void 手槽_接纳可持实例_拒绝不可持实例()
        {
            var body = NewBody();
            var handCell = body.Slots[0].Id;
            var holdable = NewDef("item.axe", true);
            var resource = NewDef("item.stone", false);
            var instance = new ItemInstance(5, holdable);

            Assert.IsTrue(body.TryPlaceInstanceAt(handCell, instance));
            Assert.AreSame(instance, body.Slots[0].Instance);
            Assert.AreEqual(1, body.CountOf(holdable));

            Assert.IsFalse(body.TryPlaceInstanceAt(handCell, new ItemInstance(6, resource)), "槽门控（种类）仍然生效");
            Assert.IsTrue(body.Slots[0].HasInstance);
        }

        [Test]
        public void 搬运_同一容器换格_身份不变()
        {
            var bag = new Inventory(3);
            var def = NewDef("item.a", false);
            var instance = new ItemInstance(1, def);
            Assert.IsTrue(bag.TryPlaceInstanceAt(new SlotId(0), instance));

            Assert.IsTrue(ContainerTransfer.MoveInstance(bag, new SlotId(0), bag, new SlotId(2)));

            Assert.IsTrue(bag.Slots[0].IsEmpty);
            Assert.AreSame(instance, bag.Slots[2].Instance, "搬的是同一个实例对象，不是复制");
            Assert.AreEqual(1, bag.CountOf(def));
        }

        [Test]
        public void 搬运_同一格_视为无操作成功()
        {
            var bag = new Inventory(1);
            var def = NewDef("item.a", false);
            var instance = new ItemInstance(1, def);
            Assert.IsTrue(bag.TryPlaceInstanceAt(new SlotId(0), instance));

            Assert.IsTrue(ContainerTransfer.MoveInstance(bag, new SlotId(0), bag, new SlotId(0)));
            Assert.AreSame(instance, bag.Slots[0].Instance);
        }

        [Test]
        public void 搬运_跨容器_源清目标持有()
        {
            var source = new Inventory(2);
            var destination = new Inventory(2);
            var def = NewDef("item.a", false);
            var instance = new ItemInstance(8, def);
            Assert.IsTrue(source.TryPlaceInstanceAt(new SlotId(0), instance));

            Assert.IsTrue(ContainerTransfer.MoveInstance(source, new SlotId(0), destination, new SlotId(1)));

            Assert.IsTrue(source.Slots[0].IsEmpty);
            Assert.AreEqual(0, source.CountOf(def));
            Assert.AreSame(instance, destination.Slots[1].Instance);
            Assert.AreEqual(1, destination.CountOf(def));
        }

        [Test]
        public void 搬运_目标拒绝_回滚源格()
        {
            var source = new Inventory(2);
            var noRoom = new SlotStore(0);
            var def = NewDef("item.a", false);
            var instance = new ItemInstance(1, def);
            Assert.IsTrue(source.TryPlaceInstanceAt(new SlotId(0), instance));

            Assert.IsFalse(ContainerTransfer.MoveInstance(source, new SlotId(0), noRoom, new SlotId(0)));

            Assert.AreSame(instance, source.Slots[0].Instance, "回滚：实例仍在源格");
            Assert.AreEqual(1, source.CountOf(def));
        }

        [Test]
        public void 整理_实例行随搬运移动且身份不变()
        {
            var store = new SlotStore(3);
            var def = NewDef("item.a", false);
            var instance = new ItemInstance(1, def);
            Assert.IsTrue(store.TryPlaceInstanceAt(new SlotId(2), instance));

            Assert.IsTrue(store.TryCompact());

            Assert.IsTrue(store.Slots[0].HasInstance);
            Assert.AreSame(instance, store.Slots[0].Instance);
            Assert.IsTrue(store.Slots[2].IsEmpty);
        }

        [Test]
        public void 缩容_实例搬入保留区_不丢弃()
        {
            var store = new SlotStore(3);
            var def = NewDef("item.a", false);
            var instance = new ItemInstance(1, def);
            Assert.IsTrue(store.TryPlaceInstanceAt(new SlotId(2), instance));

            Assert.AreEqual(0, store.ApplyCapacity(2), "保留区有空格，实例被安置而非丢弃");

            Assert.AreSame(instance, store.Slots[0].Instance);
            Assert.AreEqual(1, store.CountOf(def));
        }

        [Test]
        public void 缩容_实例放不下_走出口且聚合轨可见()
        {
            var def = NewDef("item.a", false);
            ItemDefinition discardedDef = null;
            var discardedCount = 0;
            var store = new SlotStore(2, (d, c) =>
            {
                discardedDef = d;
                discardedCount = c;
            });
            Assert.IsTrue(store.TryPlaceInstanceAt(new SlotId(0), new ItemInstance(1, def)));
            Assert.IsTrue(store.TryPlaceInstanceAt(new SlotId(1), new ItemInstance(2, def)));
            var changes = new List<ContainerChangeArgs>();
            store.Changed += changes.Add;

            Assert.AreEqual(1, store.ApplyCapacity(1), "两个实例、一格容量：丢弃 1 个");

            Assert.AreSame(def, discardedDef);
            Assert.AreEqual(1, discardedCount);
            Assert.AreEqual(1, store.CountOf(def));
            Assert.IsTrue(store.Slots[0].HasInstance);
            Assert.IsTrue(changes.Exists(c => c.OldCount == 2 && c.NewCount == 1), "丢弃必须可见（聚合轨）");
        }
    }
}
