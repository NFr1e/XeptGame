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
    /// 拾取"世界上的背包实例"测试（Item_Instance_Design.md §6/§4；DP4/DP6）：
    /// 背槽空 → 整体背上 + 请求记录层删记录；背槽已占 → 按 DP6 拒绝（换包需旧包目的地）；
    /// 放置失败 → 原样放回源（不丢实例）；不可用源 → 拒绝；记录删除失败只告警不阻断。
    /// </summary>
    public class WorldCarrierPickupTests
    {
        private readonly List<Object> _owned = new();
        private readonly List<ItemOperationCoordinator> _coordinators = new();

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

        [Test]
        public void 背槽空_拾取背包_整体背上_并请求删记录()
        {
            var removed = new List<long>();
            var acquired = new List<ItemAcquiredEvent>();
            var (body, coordinator) = NewRig(removed, acquired);
            var bagDef = NewDef("item.bag", container: true);
            var carrier = NewBag(bagDef, 4);
            var source = new WorldInstanceSource(carrier, recordId: 7);

            var receipt = coordinator.RequestPickupCarrier(source, PickupIntent.Tap);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
            Assert.AreEqual("CarrierPickedUp", receipt.Reason);
            Assert.AreSame(carrier, body.GetInstance(BodySlotType.Back), "整体背上（同一个实例对象）");
            Assert.IsFalse(source.HasContent, "源已空 → 视图可隐藏");
            CollectionAssert.AreEqual(new long[] { 7 }, removed, "拾取成功 → 请求记录层删记录（I3）");
            Assert.AreEqual(1, acquired.Count, "获得播报一次");
            Assert.AreSame(bagDef, acquired[0].Item);
        }

        [Test]
        public void 背槽已占_点按_装进当前背包()
        {
            var removed = new List<long>();
            var (body, coordinator) = NewRig(removed, null);
            var bagDef = NewDef("item.bag", container: true);
            var worn = NewBag(bagDef, 4);
            coordinator.RequestSwapCarrier(worn, null);
            var second = new WorldInstanceSource(NewBag(bagDef, 4), recordId: 9);

            var receipt = coordinator.RequestPickupCarrier(second, PickupIntent.Tap);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status, "多一个背包是合法的（背槽只限制'同时戴一个'）");
            Assert.AreEqual("CarrierStowedInBag", receipt.Reason);
            Assert.AreSame(worn, body.GetInstance(BodySlotType.Back), "背上的包不变");
            Assert.AreEqual(1, worn.Store.CountOf(bagDef), "第二个背包进了当前背包的格子");
            Assert.IsTrue(worn.Store.Slots[0].HasInstance, "以实例行存在（数量恒 1）");
            Assert.IsFalse(second.HasContent, "源已空");
            CollectionAssert.AreEqual(new long[] { 9 }, removed, "记录随拾取删除");
        }

        [Test]
        public void 背槽已占_长按换包_拒绝且源与记录零改动()
        {
            var removed = new List<long>();
            var (body, coordinator) = NewRig(removed, null);
            var bagDef = NewDef("item.bag", container: true);
            var worn = NewBag(bagDef, 4);
            coordinator.RequestSwapCarrier(worn, null);
            var second = new WorldInstanceSource(NewBag(bagDef, 4), recordId: 9);

            var receipt = coordinator.RequestPickupCarrier(second, PickupIntent.ForceHold);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreEqual("NoCarrierDestination", receipt.Reason, "DP6：换包需要旧包去向（WorldDrop 在 T5）");
            Assert.AreSame(worn, body.GetInstance(BodySlotType.Back), "背上的包未被动");
            Assert.IsTrue(second.HasContent, "源仍持有该实例");
            Assert.AreEqual(0, removed.Count, "记录未删");
        }

        [Test]
        public void 当前背包满_点按_拒绝且原样放回源()
        {
            var removed = new List<long>();
            var (_, coordinator) = NewRig(removed, null);
            var bagDef = NewDef("item.bag", container: true);
            coordinator.RequestSwapCarrier(NewBag(bagDef, 0), null); // 0 格背包：装不下任何东西
            var second = new WorldInstanceSource(NewBag(bagDef, 4), recordId: 3);

            var receipt = coordinator.RequestPickupCarrier(second, PickupIntent.Tap);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreEqual("BagFull", receipt.Reason);
            Assert.IsTrue(second.HasContent, "原样放回源（不丢实例）");
            Assert.AreEqual(0, removed.Count);
        }

        [Test]
        public void 放置失败_原样放回源()
        {
            var removed = new List<long>();
            var (body, coordinator) = NewRig(removed, null);
            var plainDef = NewDef("item.plain", container: false); // 背槽门控拒绝：不是容器类
            var carrier = NewBag(plainDef, 4);
            var source = new WorldInstanceSource(carrier, recordId: 11);

            var receipt = coordinator.RequestPickupCarrier(source, PickupIntent.Tap);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreEqual("CarrierPlacementFailed", receipt.Reason);
            Assert.AreSame(carrier, source.Carrier, "原样放回源（不丢实例，I1/I7）");
            Assert.IsNull(body.GetInstance(BodySlotType.Back));
            Assert.AreEqual(0, removed.Count);
        }

        [Test]
        public void 源不可用或已空_拒绝()
        {
            var (_, coordinator) = NewRig(null, null);
            var bagDef = NewDef("item.bag", container: true);

            Assert.AreEqual("SourceUnavailable", coordinator.RequestPickupCarrier(null, PickupIntent.Tap).Reason);

            var unavailable = new WorldInstanceSource(NewBag(bagDef, 2), recordId: 1, available: () => false);
            Assert.AreEqual("SourceUnavailable", coordinator.RequestPickupCarrier(unavailable, PickupIntent.Tap).Reason);

            var emptied = new WorldInstanceSource(NewBag(bagDef, 2));
            emptied.TryTakeCarrier(out _);
            Assert.AreEqual("SourceUnavailable", coordinator.RequestPickupCarrier(emptied, PickupIntent.Tap).Reason);
        }

        [Test]
        public void 记录删除失败_不阻断业务()
        {
            var (body, coordinator) = NewRig(null, null, removeRecord: _ => false);
            var bagDef = NewDef("item.bag", container: true);
            var carrier = NewBag(bagDef, 3);
            var source = new WorldInstanceSource(carrier, recordId: 5);

            var receipt = coordinator.RequestPickupCarrier(source, PickupIntent.ForceHold);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status, "删记录失败只告警，不回滚已完成的拾取");
            Assert.AreSame(carrier, body.GetInstance(BodySlotType.Back));
        }

        [Test]
        public void 暂停中_拒绝()
        {
            var body = new Equipment(new SlotBase[] { new HandSlot(), new BackSlot() });
            var behavior = new EquipController();
            behavior.SetPaused(true);
            var coordinator = new ItemOperationCoordinator(body, behavior);
            _coordinators.Add(coordinator);
            var source = new WorldInstanceSource(NewBag(NewDef("item.bag", container: true), 2));

            Assert.AreEqual("BusyOrPaused", coordinator.RequestPickupCarrier(source, PickupIntent.Tap).Reason);
        }

        private (Equipment Body, ItemOperationCoordinator Coordinator) NewRig(
            List<long> removed, List<ItemAcquiredEvent> acquired, System.Func<long, bool> removeRecord = null)
        {
            var body = new Equipment(new SlotBase[] { new HandSlot(), new BackSlot() });
            var behavior = new EquipController();
            behavior.SetPaused(false);

            System.Action<ItemAcquiredEvent> publish = null;
            if (acquired != null)
            {
                publish = acquired.Add;
            }

            if (removeRecord == null && removed != null)
            {
                removeRecord = id =>
                {
                    removed.Add(id);
                    return true;
                };
            }

            var coordinator = new ItemOperationCoordinator(body, behavior, publish, removeRecord: removeRecord);
            _coordinators.Add(coordinator);
            return (body, coordinator);
        }

        private ContainerInstance NewBag(ItemDefinition definition, int slots)
            => new(++_nextId, definition, new Inventory(slots));

        private long _nextId;

        private ItemDefinition NewDef(string id, bool container)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            _owned.Add(def);
            if (container)
            {
                GetFacetsList(def).Add(new ContainerFacet());
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
