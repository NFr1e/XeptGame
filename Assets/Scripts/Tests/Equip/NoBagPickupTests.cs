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
    /// 无包态拾取分流测试（Item_Instance_Design.md §3；Hand 槽与背包解耦）：
    /// <b>长按拿取</b>=先上一手、余量入包 → 无包时不受影响（一单位到手，余量留在世界源）；
    /// <b>点按拾取</b>=整批入包 → 无包时拒绝 <c>NoBag</c>；<b>不可持物</b>进不了手 → 同样拒绝。
    /// 手上已有别的可持物时不再直接拒绝：旧物去向 = 世界（有掉落口则落地、新物到手；
    /// 无掉落口则失败且旧物留在手上——落地路径见 <see cref="WorldDropTests"/>）。
    /// 另回归：有包时长按仍是"一手 + 余量入包"，且一次拒绝不会把协调器打成一致性故障。
    /// </summary>
    public class NoBagPickupTests
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
        public void 无包_长按拿取可持物_一手到手_余量留在世界()
        {
            var (body, coordinator) = NewRig();
            var wood = NewDef("item.wood", holdable: true);
            var world = NewWorldSource(wood, 5);

            var receipt = coordinator.RequestPickup(world, wood, world.Remaining, PickupIntent.ForceHold, null);

            Assert.AreNotEqual(OperationStatus.Rejected, receipt.Status, "无包不应挡住长按拿取");
            Assert.AreSame(wood, body.Get(BodySlotType.Hand), "一单位到手");
            Assert.AreEqual(1, body.CountOf(wood));
            Assert.AreEqual(4, world.Remaining, "余量留在世界源里（没有背包可放）");

            Pump(coordinator);
            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
        }

        [Test]
        public void 无包_点按拾取_拒绝NoBag且零改动()
        {
            var (body, coordinator) = NewRig();
            var wood = NewDef("item.wood", holdable: true);
            var world = NewWorldSource(wood, 5);

            var receipt = coordinator.RequestPickup(world, wood, world.Remaining, PickupIntent.Tap, null);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreEqual("NoBag", receipt.Reason, "点按整批入包 → 无包必失败");
            Assert.IsTrue(body.IsEmpty(BodySlotType.Hand));
            Assert.AreEqual(5, world.Remaining, "零改动");
        }

        [Test]
        public void 无包_手上有别的可持物_长按拿取_无掉落口_失败且旧物留在手上()
        {
            var (body, coordinator) = NewRig();
            var axe = NewDef("item.axe", holdable: true);
            var wood = NewDef("item.wood", holdable: true);
            var woodSource = NewWorldSource(wood, 3);

            // 旧物也必须经真实拾取上手：直接 body.TryAdd 会被协调器的"外部占用巡检"判成 ExternalOccupancyChange
            coordinator.RequestPickup(NewWorldSource(axe, 1), axe, 1, PickupIntent.ForceHold, null);
            Pump(coordinator);
            Assert.AreSame(axe, body.Get(BodySlotType.Hand), "前提：无包，手上拿着斧头");

            var receipt = coordinator.RequestPickup(woodSource, wood, woodSource.Remaining, PickupIntent.ForceHold, null);
            Pump(coordinator);

            Assert.AreEqual(OperationStatus.Failed, receipt.Status, "旧物去向=世界，但本夹具没有掉落口 → 明确失败");
            Assert.AreEqual("DestinationRejected", receipt.Reason);
            Assert.AreSame(axe, body.Get(BodySlotType.Hand), "手上旧物未被动（不静默丢）");
            Assert.AreEqual(3, woodSource.Remaining, "新物仍在世界源里");
        }

        [Test]
        public void 无包_手上有同定义可持物_长按拿取_无掉落口_失败且零改动()
        {
            var (body, coordinator) = NewRig();
            var axe = NewDef("item.axe", holdable: true);

            coordinator.RequestPickup(NewWorldSource(axe, 1), axe, 1, PickupIntent.ForceHold, null);
            Pump(coordinator);
            Assert.AreSame(axe, body.Get(BodySlotType.Hand), "前提：无包，手上拿着一把斧头");

            var second = NewWorldSource(axe, 2);
            var receipt = coordinator.RequestPickup(second, axe, 2, PickupIntent.ForceHold, null);
            Pump(coordinator);

            Assert.AreEqual(OperationStatus.Failed, receipt.Status, "同定义也按换手处理；本夹具无掉落口 → 明确失败");
            Assert.AreEqual("DestinationRejected", receipt.Reason);
            Assert.AreEqual(1, body.CountOf(axe), "手上仍只有一把（不静默丢）");
            Assert.AreEqual(2, second.Remaining, "源零改动");
        }

        [Test]
        public void 无包_长按拿取不可持物_拒绝NoBag()
        {
            var (body, coordinator) = NewRig();
            var stone = NewDef("item.stone", holdable: false);
            var world = NewWorldSource(stone, 2);

            var receipt = coordinator.RequestPickup(world, stone, world.Remaining, PickupIntent.ForceHold, null);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreEqual("NoBag", receipt.Reason, "不可持物只能入包 → 无包时没有去向");
            Assert.AreEqual(2, world.Remaining);
        }

        [Test]
        public void 有包_长按拿取_一手到手_余量入包()
        {
            var (body, coordinator) = NewRig();
            var wood = NewDef("item.wood", holdable: true);
            var bag = new Inventory(4);
            var world = NewWorldSource(wood, 5);

            var receipt = coordinator.RequestPickup(world, wood, world.Remaining, PickupIntent.ForceHold, bag);

            Assert.AreNotEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreSame(wood, body.Get(BodySlotType.Hand));
            Assert.AreEqual(4, bag.CountOf(wood), "余量入包（回归：有包行为不变）");
            Assert.AreEqual(0, world.Remaining);

            Pump(coordinator);
            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
        }

        [Test]
        public void 无包拒绝后_协调器仍可用()
        {
            var (body, coordinator) = NewRig();
            var wood = NewDef("item.wood", holdable: true);
            var first = NewWorldSource(wood, 1);
            Assert.AreEqual("NoBag", coordinator.RequestPickup(first, wood, 1, PickupIntent.Tap, null).Reason);

            var bag = new Inventory(2);
            var second = NewWorldSource(wood, 2);
            var receipt = coordinator.RequestPickup(second, wood, 2, PickupIntent.Tap, bag);

            Assert.AreNotEqual(OperationStatus.Rejected, receipt.Status, "一次拒绝不应把协调器打成一致性故障");
            Pump(coordinator);
            Assert.AreSame(wood, body.Get(BodySlotType.Hand), "手空时点按：一单位上手（与既有路由一致）");
            Assert.AreEqual(1, bag.CountOf(wood), "余量入包");
        }

        private (Equipment Body, ItemOperationCoordinator Coordinator) NewRig()
        {
            var body = new Equipment(new SlotBase[] { new HandSlot(), new BackSlot() });
            var behavior = new EquipController();
            behavior.SetPaused(false);
            var coordinator = new ItemOperationCoordinator(body, behavior);
            _coordinators.Add(coordinator);
            return (body, coordinator);
        }

        /// <summary>推进到操作终局（Draw 阶段按时间推进，需 Tick）。</summary>
        private static void Pump(ItemOperationCoordinator coordinator)
        {
            const float step = 1f / 60f;
            for (float t = 0f; t < 5f && coordinator.Current != null; t += step)
            {
                coordinator.Tick(step);
            }
        }

        private WorldStackSource NewWorldSource(ItemDefinition definition, int count)
            => new WorldStackSource(definition, count, () => true, () => { });

        private ItemDefinition NewDef(string id, bool holdable)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            _owned.Add(def);
            GetFacetsList(def).Add(holdable ? (IItemFacet)new HoldableFacet() : new ResourceFacet());
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
