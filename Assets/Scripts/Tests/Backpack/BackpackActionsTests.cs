using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Container;
using XeptGame.Equip;
using XeptGame.Inv;
using XeptGame.Items;
using XeptGame.Items.Operations;
using XeptGame.World;

namespace XeptGame.Tests
{
    /// <summary>
    /// 背包界面两个新请求的测试（Backpack_UI_Design.md B2）：<b>使用</b>（扣 1 + 播报一次）与
    /// <b>丢弃</b>（移除 → 落地 → 失败原样放回）。验收对应 B8 第三条：使用扣 1 且失败零改动；
    /// 丢弃 = 移除 → 落地 → 失败回滚（复用 <c>WorldDropTests</c> 夹具形状）。
    /// 界面本身（格视图）不在此；这里只测"界面只调两个口"的那两个口。
    /// </summary>
    public class BackpackActionsTests
    {
        private readonly List<Object> _owned = new();
        private readonly List<ItemOperationCoordinator> _coordinators = new();
        private long _nextId;

        [TearDown]
        public void TearDown()
        {
            foreach (var coordinator in _coordinators)
            {
                coordinator?.Dispose();
            }

            _coordinators.Clear();

            foreach (var obj in _owned)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _owned.Clear();
        }

        // ---- 使用 ----

        [Test]
        public void 使用_消耗品_扣一个并播报一次()
        {
            var (_, coordinator, bag, consumed) = NewRig(out _);
            var drug = NewDef("item.drug", new ConsumableFacet());
            bag.TryAdd(drug, 3);

            var receipt = coordinator.RequestConsume(bag, drug, 1);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
            Assert.AreEqual("Consumed", receipt.Reason);
            Assert.AreEqual(2, bag.CountOf(drug), "从背包扣 1");
            Assert.AreEqual(1, consumed.Count, "播报恰好一次");
            Assert.AreSame(drug, consumed[0].Item);
            Assert.AreEqual(1, consumed[0].Count);
        }

        [Test]
        public void 使用_未挂消耗面_拒绝且零改动()
        {
            var (_, coordinator, bag, consumed) = NewRig(out _);
            var wood = NewDef("item.wood");
            bag.TryAdd(wood, 2);

            var receipt = coordinator.RequestConsume(bag, wood, 1);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreEqual("NotConsumable", receipt.Reason);
            Assert.AreEqual(2, bag.CountOf(wood), "零改动");
            CollectionAssert.IsEmpty(consumed, "被拒不播报");
        }

        [Test]
        public void 使用_数量不足_拒绝且零改动()
        {
            var (_, coordinator, bag, consumed) = NewRig(out _);
            var drug = NewDef("item.drug", new ConsumableFacet());
            bag.TryAdd(drug, 1);

            var receipt = coordinator.RequestConsume(bag, drug, 2);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreEqual("ItemNotFound", receipt.Reason);
            Assert.AreEqual(1, bag.CountOf(drug), "原子扣减：不足则不动");
            CollectionAssert.IsEmpty(consumed);
        }

        [Test]
        public void 使用_暂停中_拒绝()
        {
            var (behavior, coordinator, bag, consumed) = NewRig(out _);
            var drug = NewDef("item.drug", new ConsumableFacet());
            bag.TryAdd(drug, 1);
            behavior.SetPaused(true);

            var receipt = coordinator.RequestConsume(bag, drug, 1);

            Assert.AreEqual("BusyOrPaused", receipt.Reason);
            Assert.AreEqual(1, bag.CountOf(drug));
            CollectionAssert.IsEmpty(consumed);
        }

        // ---- 丢弃：定义寻址 ----

        [Test]
        public void 丢弃_定义寻址_移除并落成一条世界记录()
        {
            var (_, coordinator, bag, _) = NewRig(out var records);
            var wood = NewDef("item.wood");
            bag.TryAdd(wood, 5);

            var receipt = coordinator.RequestDrop(bag, wood, 2);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
            Assert.AreEqual("Dropped", receipt.Reason);
            Assert.AreEqual(3, bag.CountOf(wood), "先移除");
            var dropped = records.RecordsIn("level.a");
            Assert.AreEqual(1, dropped.Count, "再落地（一条堆叠记录）");
            Assert.AreSame(wood, dropped[0].Definition);
            Assert.AreEqual(2, dropped[0].Count);
        }

        [Test]
        public void 丢弃_无掉落口_拒绝且零改动()
        {
            var (_, coordinator, bag) = NewRigWithoutDropPort();
            var wood = NewDef("item.wood");
            bag.TryAdd(wood, 5);

            var receipt = coordinator.RequestDrop(bag, wood, 2);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreEqual("NoWorldDrop", receipt.Reason);
            Assert.AreEqual(5, bag.CountOf(wood), "没有落地口就不动它（不静默丢）");
        }

        [Test]
        public void 丢弃_掉落被拒_原样放回背包()
        {
            var records = new WorldRecordStore(new InstanceIdAllocator(), 0, _ => { });
            var (_, coordinator, bag, _) = NewRigCore(() => new RejectingDropPort(), records);
            var wood = NewDef("item.wood");
            bag.TryAdd(wood, 5);

            var receipt = coordinator.RequestDrop(bag, wood, 2);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreEqual("DropRejected", receipt.Reason);
            Assert.AreEqual(5, bag.CountOf(wood), "落地失败 → 原样放回（既不丢也不复制）");
            Assert.AreEqual(0, records.Count, "落地被拒不该留下记录");
        }

        [Test]
        public void 丢弃_实例行_掉落被拒_放回原格且身份不变()
        {
            var records = new WorldRecordStore(new InstanceIdAllocator(), 0, _ => { });
            var (_, coordinator, bag, _) = NewRigCore(() => new RejectingDropPort(), records);
            var bagDef = NewDef("item.backpack", null, container: true);
            var stowed = NewBag(bagDef, 4);
            bag.TryPlaceInstance(stowed);

            var receipt = coordinator.RequestDrop(bag, new SlotId(0), 1);

            Assert.AreEqual("DropRejected", receipt.Reason);
            Assert.AreEqual(1, bag.CountOf(bagDef), "实例必须回到背包（I1/I7：不得凭空消失）");
            Assert.AreSame(stowed, bag.Slots[0].Instance, "放回**原格**且是同一个实例对象");
        }

        [Test]
        public void 丢弃_同定义两格_只动选中的那一格()
        {
            var (_, coordinator, bag, _) = NewRig(out var records);
            var stone = NewDef("item.stone");
            bag.TryPlaceAt(new SlotId(0), stone, 12); // 前格满
            bag.TryPlaceAt(new SlotId(1), stone, 5);  // 后格 5 个

            var receipt = coordinator.RequestDrop(bag, new SlotId(1), 1);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
            Assert.AreEqual(12, bag.Slots[0].Count, "前格必须一个不动");
            Assert.AreEqual(4, bag.Slots[1].Count, "只扣选中的后格");
            Assert.AreEqual(1, records.RecordsIn("level.a").Count);
        }

        [Test]
        public void 丢弃_同定义两格_落地被拒_放回原格且前格不动()
        {
            var records = new WorldRecordStore(new InstanceIdAllocator(), 0, _ => { });
            var (_, coordinator, bag, _) = NewRigCore(() => new RejectingDropPort(), records);
            var stone = NewDef("item.stone");
            bag.TryPlaceAt(new SlotId(0), stone, 12);
            bag.TryPlaceAt(new SlotId(1), stone, 5);

            var receipt = coordinator.RequestDrop(bag, new SlotId(1), 1);

            Assert.AreEqual("DropRejected", receipt.Reason);
            Assert.AreEqual(12, bag.Slots[0].Count, "回滚不得把东西塞到前格去");
            Assert.AreEqual(5, bag.Slots[1].Count, "原样放回**原格**");
        }

        [Test]
        public void 使用_同定义两格_只扣选中的那一格()
        {
            var (_, coordinator, bag, consumed) = NewRig(out _);
            var drug = NewDef("item.drug", new ConsumableFacet());
            bag.TryPlaceAt(new SlotId(0), drug, 12);
            bag.TryPlaceAt(new SlotId(1), drug, 5);

            var receipt = coordinator.RequestConsume(bag, new SlotId(1), 1);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
            Assert.AreEqual(12, bag.Slots[0].Count, "前格必须一个不动");
            Assert.AreEqual(4, bag.Slots[1].Count, "只扣选中的后格");
            Assert.AreEqual(1, consumed.Count);
        }

        [Test]
        public void 使用_格寻址_选中实例行_拒绝()
        {
            var (_, coordinator, bag, consumed) = NewRig(out _);
            var bagDef = NewDef("item.backpack", null, container: true);
            bag.TryPlaceInstance(NewBag(bagDef, 4));

            var receipt = coordinator.RequestConsume(bag, new SlotId(0), 1);

            Assert.AreEqual("NotConsumable", receipt.Reason, "实例行不是'能吃掉的东西'");
            Assert.AreEqual(1, bag.CountOf(bagDef), "零改动");
            CollectionAssert.IsEmpty(consumed);
        }

        [Test]
        public void 使用_定义寻址_仍然可用_从最靠前的同物格扣()
        {
            var (_, coordinator, bag, _) = NewRig(out _);
            var drug = NewDef("item.drug", new ConsumableFacet());
            bag.TryPlaceAt(new SlotId(0), drug, 12);
            bag.TryPlaceAt(new SlotId(1), drug, 5);

            // 无格上下文的调用方（将来的快捷栏"直接用掉一个"）走定义寻址，语义就是"最靠前的那格"
            var receipt = coordinator.RequestConsume(bag, drug, 1);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
            Assert.AreEqual(11, bag.Slots[0].Count);
            Assert.AreEqual(5, bag.Slots[1].Count);
        }

        // ---- 丢弃：格寻址（界面主路径） ----

        [Test]
        public void 丢弃_格寻址_实例行整包落地_携带同一实例()
        {
            var (_, coordinator, bag, _) = NewRig(out var records);
            var bagDef = NewDef("item.backpack", null, container: true);
            var stowed = NewBag(bagDef, 4);
            Assert.IsTrue(bag.TryPlaceInstance(stowed), "前提：背包里放着一个背包实例");

            var receipt = coordinator.RequestDrop(bag, new SlotId(0), 1);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
            Assert.AreEqual(0, bag.CountOf(bagDef), "实例已离开背包（I1：一个实例一个位置）");
            var dropped = records.RecordsIn("level.a");
            Assert.AreEqual(1, dropped.Count);
            Assert.IsTrue(dropped[0].IsInstanceRecord);
            Assert.AreSame(stowed, dropped[0].Instance, "整包落地：携带同一个实例");
        }

        [Test]
        public void 丢弃_格寻址_以格内实际内容为准()
        {
            var (_, coordinator, bag, _) = NewRig(out var records);
            var wood = NewDef("item.wood");
            bag.TryAdd(wood, 3);

            // 调用方给了一个"错的定义"（石头），以槽为真相 → 丢的是木头
            var stone = NewDef("item.stone");
            var receipt = coordinator.RequestDrop(bag, new SlotId(0), 1);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
            Assert.AreEqual(2, bag.CountOf(wood));
            Assert.AreSame(wood, records.RecordsIn("level.a")[0].Definition);
            Assert.AreEqual(0, bag.CountOf(stone));
        }

        [Test]
        public void 丢弃_格寻址_空格_拒绝()
        {
            var (_, coordinator, bag, _) = NewRig(out _);
            var wood = NewDef("item.wood");
            bag.TryAdd(wood, 1);

            var receipt = coordinator.RequestDrop(bag, new SlotId(5), 1);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreEqual("CellEmpty", receipt.Reason);
            Assert.AreEqual(1, bag.CountOf(wood), "零改动");
        }

        [Test]
        public void 丢弃_暂停中_拒绝()
        {
            var (behavior, coordinator, bag, _) = NewRig(out var records);
            var wood = NewDef("item.wood");
            bag.TryAdd(wood, 2);
            behavior.SetPaused(true);

            var receipt = coordinator.RequestDrop(bag, wood, 1);

            Assert.AreEqual("BusyOrPaused", receipt.Reason);
            Assert.AreEqual(2, bag.CountOf(wood));
            Assert.AreEqual(0, records.RecordsIn("level.a").Count);
        }

        // ---- 夹具 ----

        private (EquipController Behavior, ItemOperationCoordinator Coordinator, Inventory Bag, List<ItemConsumedEvent> Consumed)
            NewRig(out WorldRecordStore records)
        {
            records = new WorldRecordStore(new InstanceIdAllocator(), 0, _ => { });
            var destination = new WorldDropDestination(new WorldDropFactory(records), () => new Vector3(0f, 0f, 1f), "level.a");
            return NewRigCore(() => destination, records);
        }

        private (EquipController Behavior, ItemOperationCoordinator Coordinator, Inventory Bag)
            NewRigWithoutDropPort()
        {
            var records = new WorldRecordStore(new InstanceIdAllocator(), 0, _ => { });
            var (behavior, coordinator, bag, _) = NewRigCore(() => null, records);
            return (behavior, coordinator, bag);
        }

        private (EquipController Behavior, ItemOperationCoordinator Coordinator, Inventory Bag, List<ItemConsumedEvent> Consumed)
            NewRigCore(System.Func<IWorldDropPort> dropPort, WorldRecordStore records)
        {
            var body = new Equipment(new SlotBase[] { new HandSlot(), new BackSlot() });
            var behavior = new EquipController();
            behavior.SetPaused(false);
            var consumed = new List<ItemConsumedEvent>();
            var coordinator = new ItemOperationCoordinator(
                body, behavior,
                removeRecord: records.TryRemove,
                worldDrop: dropPort,
                publishConsumed: consumed.Add);
            _coordinators.Add(coordinator);
            return (behavior, coordinator, new Inventory(6), consumed);
        }

        private ContainerInstance NewBag(ItemDefinition definition, int slots)
            => new(++_nextId, definition, new Inventory(slots));

        /// <summary>一律拒绝的世界掉落口（验"落地失败必须原样放回"）。</summary>
        private sealed class RejectingDropPort : IWorldDropPort
        {
            public bool TryAccept(ContainerInstance carrier, out string reason)
            {
                reason = "DropRejected";
                return false;
            }

            public bool TryAcceptStack(ItemDefinition definition, int count, out string reason)
            {
                reason = "DropRejected";
                return false;
            }
        }

        private ItemDefinition NewDef(string id, IItemFacet facet = null, bool container = false)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            _owned.Add(def);
            var facets = GetFacetsList(def);
            if (container)
            {
                facets.Add(new ContainerFacet());
            }

            if (facet != null)
            {
                facets.Add(facet);
            }

            return def;
        }

        private static List<IItemFacet> GetFacetsList(ItemDefinition def)
        {
            var field = typeof(ItemDefinition).GetField("facets", BindingFlags.Instance | BindingFlags.NonPublic);
            var list = field.GetValue(def) as List<IItemFacet>;
            if (list == null)
            {
                list = new List<IItemFacet>();
                field.SetValue(def, list);
            }

            return list;
        }
    }
}
