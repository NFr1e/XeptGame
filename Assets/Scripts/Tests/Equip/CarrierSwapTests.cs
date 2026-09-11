using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Container;
using XeptGame.Equip;
using XeptGame.Inv;
using XeptGame.Items;
using XeptGame.Items.Operations;

namespace XeptGame.Tests
{
    /// <summary>
    /// 换包编排测试（Item_Instance_Design.md §4 / DP6）：
    /// 背槽空 = 直接背上；同一实例 = 无操作成功；旧包非空且无目的地 = 拒绝且零改动；
    /// 有目的地 = 旧包整包交接且两侧内容都不迁移；目的地拒绝 = 完整回滚；暂停中 = 拒绝。
    /// 假 Destination 记录"谁被交接了"，不引入世界层（T5 才落 WorldDrop）。
    /// </summary>
    public class CarrierSwapTests
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
        public void 换包_背槽为空_直接背上()
        {
            var (body, coordinator) = NewRig();
            var bag = NewBag(4);

            var receipt = coordinator.RequestSwapCarrier(bag, null);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
            Assert.AreSame(bag, body.GetInstance(BodySlotType.Back));
        }

        [Test]
        public void 换包_同一实例_无操作成功()
        {
            var (body, coordinator) = NewRig();
            var bag = NewBag(4);
            coordinator.RequestSwapCarrier(bag, null);

            var receipt = coordinator.RequestSwapCarrier(bag, null);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
            Assert.AreEqual("AlreadyEquipped", receipt.Reason);
            Assert.AreSame(bag, body.GetInstance(BodySlotType.Back));
        }

        [Test]
        public void 换包_旧包非空_无目的地_拒绝且零改动()
        {
            var (body, coordinator) = NewRig();
            var stone = NewDef("item.stone");
            var oldBag = NewBag(4);
            Assert.IsTrue(oldBag.Store.TryAdd(stone, 3), "旧包里有东西");
            coordinator.RequestSwapCarrier(oldBag, null);
            var incoming = NewBag(4);

            var receipt = coordinator.RequestSwapCarrier(incoming, null);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreEqual("NoCarrierDestination", receipt.Reason, "DP6：WorldDrop 接线前不静默丢包");
            Assert.AreSame(oldBag, body.GetInstance(BodySlotType.Back), "旧包仍在背上");
            Assert.AreEqual(3, oldBag.Store.CountOf(stone), "旧包内容零改动");
            Assert.AreEqual(0, incoming.Store.CountOf(stone));
        }

        [Test]
        public void 换包_空旧包_无目的地_也拒绝_规则单一()
        {
            var (body, coordinator) = NewRig();
            var oldBag = NewBag(4);
            coordinator.RequestSwapCarrier(oldBag, null);

            var receipt = coordinator.RequestSwapCarrier(NewBag(4), null);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status, "空包同样需要去向（规则单一，避免静默丢实例）");
            Assert.AreSame(oldBag, body.GetInstance(BodySlotType.Back));
        }

        [Test]
        public void 换包_有目的地_整包交接且内容不迁移()
        {
            var (body, coordinator) = NewRig();
            var stone = NewDef("item.stone");
            var iron = NewDef("item.iron");
            var oldBag = NewBag(4);
            var newBag = NewBag(6);
            Assert.IsTrue(oldBag.Store.TryAdd(stone, 3));
            Assert.IsTrue(newBag.Store.TryAdd(iron, 2));
            coordinator.RequestSwapCarrier(oldBag, null);
            var destination = new FakeDestination { Accept = true };

            var receipt = coordinator.RequestSwapCarrier(newBag, destination);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
            Assert.AreEqual("CarrierSwapped", receipt.Reason);
            Assert.AreSame(newBag, body.GetInstance(BodySlotType.Back), "新包上身");
            Assert.AreEqual(1, destination.Accepted.Count);
            Assert.AreSame(oldBag, destination.Accepted[0], "旧包整包被交接（同一个实例对象）");
            Assert.AreEqual(3, oldBag.Store.CountOf(stone), "旧包内容不迁移");
            Assert.AreEqual(2, newBag.Store.CountOf(iron), "新包内容不迁移");
            Assert.AreEqual(0, newBag.Store.CountOf(stone));
        }

        [Test]
        public void 换包_目的地拒绝_完整回滚()
        {
            var (body, coordinator) = NewRig();
            var stone = NewDef("item.stone");
            var oldBag = NewBag(4);
            var newBag = NewBag(6);
            Assert.IsTrue(oldBag.Store.TryAdd(stone, 1));
            Assert.IsTrue(newBag.Store.TryAdd(stone, 2));
            coordinator.RequestSwapCarrier(oldBag, null);
            var destination = new FakeDestination { Accept = false, Reason = "WorldNotReady" };

            var receipt = coordinator.RequestSwapCarrier(newBag, destination);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreEqual("WorldNotReady", receipt.Reason, "目的地的类型化原因原样进回执");
            Assert.AreSame(oldBag, body.GetInstance(BodySlotType.Back), "旧包放回背槽");
            Assert.AreEqual(1, oldBag.Store.CountOf(stone), "旧包内容未动");
            Assert.AreEqual(2, newBag.Store.CountOf(stone), "新包未被上身");
            Assert.AreEqual(0, destination.Accepted.Count);
        }

        [Test]
        public void 换包_暂停中_拒绝()
        {
            var (_, coordinator, behavior) = NewRigWithBehavior();
            behavior.SetPaused(true);
            var bag = NewBag(4);

            var receipt = coordinator.RequestSwapCarrier(bag, null);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreEqual("BusyOrPaused", receipt.Reason);
        }

        [Test]
        public void 换包_空实例_拒绝()
        {
            var (_, coordinator) = NewRig();

            var receipt = coordinator.RequestSwapCarrier(null, null);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreEqual("NoCarrier", receipt.Reason);
        }

        [Test]
        public void 换包_终局通知只发一次且写入LastResult()
        {
            var (body, coordinator) = NewRig();
            var receipts = new List<OperationReceipt>();
            coordinator.OperationFinished += receipts.Add;

            var receipt = coordinator.RequestSwapCarrier(NewBag(4), null);

            Assert.AreEqual(1, receipts.Count, "同步命令也只通知一次终局");
            Assert.AreSame(receipt, receipts[0]);
            Assert.AreSame(receipt, coordinator.LastResult);
        }

        private (Equipment Body, ItemOperationCoordinator Coordinator) NewRig()
        {
            var (body, coordinator, _) = NewRigWithBehavior();
            return (body, coordinator);
        }

        private (Equipment Body, ItemOperationCoordinator Coordinator, EquipController Behavior) NewRigWithBehavior()
        {
            var body = new Equipment(new SlotBase[] { new HandSlot(), new BackSlot() });
            var behavior = new EquipController();
            behavior.SetPaused(false);
            var coordinator = new ItemOperationCoordinator(body, behavior);
            _coordinators.Add(coordinator);
            return (body, coordinator, behavior);
        }

        private ContainerInstance NewBag(int slots)
        {
            var def = NewDef("item.bag." + slots);
            GetFacetsList(def).Add(new ContainerFacet()); // 背槽种类门控要求容器面；容器本体直接构造（工厂另测）
            return new ContainerInstance(++_nextId, def, new Inventory(slots));
        }

        private long _nextId;

        private ItemDefinition NewDef(string id)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            _owned.Add(def);
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

        private sealed class FakeDestination : ICarrierDestination
        {
            public readonly List<ContainerInstance> Accepted = new();
            public bool Accept;
            public string Reason = "DestinationRejected";

            public bool TryAccept(ContainerInstance carrier, out string reason)
            {
                reason = Reason;
                if (!Accept)
                {
                    return false;
                }

                Accepted.Add(carrier);
                return true;
            }
        }
    }
}
