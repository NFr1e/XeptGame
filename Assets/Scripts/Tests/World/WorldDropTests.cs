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
    /// 世界掉落落地测试（Item_Instance_Design.md §4/§5.3；T5a）：
    /// <see cref="WorldDropFactory"/> 造记录、<see cref="WorldDropDestination"/> 整包接收、
    /// 长按换包走"新包上身 + 旧包落世界（整包）+ 源记录删除"，且目的地失败时完整回滚。
    /// </summary>
    public class WorldDropTests
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
        public void 掉落_实例记录_携带同一实例且数量恒一()
        {
            var records = NewStore();
            var factory = new WorldDropFactory(records);
            var bagDef = NewDef("item.backpack", container: true);
            var bag = NewBag(bagDef, 3);

            var record = factory.DropCarrier(bag, new Vector3(1f, 0f, 2f), "level.a");

            Assert.IsNotNull(record);
            Assert.IsTrue(record.IsInstanceRecord);
            Assert.AreSame(bag, record.Instance, "整包落地：记录携带同一个实例对象（内容不动）");
            Assert.AreEqual(1, record.Count);
            Assert.AreEqual(new Vector3(1f, 0f, 2f), record.Position);
            Assert.AreEqual(1, records.CountIn("level.a"));
        }

        [Test]
        public void 掉落_目的地_按提供器给的位置落记录()
        {
            var records = NewStore();
            var destination = new WorldDropDestination(new WorldDropFactory(records), () => new Vector3(5f, 0f, 6f), "level.a");
            var bag = NewBag(NewDef("item.backpack", container: true), 3);

            Assert.IsTrue(destination.TryAccept(bag, out var reason));
            Assert.IsNull(reason);

            var dropped = records.RecordsIn("level.a");
            Assert.AreEqual(1, dropped.Count);
            Assert.AreSame(bag, dropped[0].Instance);
            Assert.AreEqual(new Vector3(5f, 0f, 6f), dropped[0].Position);
        }

        [Test]
        public void 掉落_记录超上限_接收失败()
        {
            var records = new WorldRecordStore(new InstanceIdAllocator(), maxRecordsPerGroup: 1);
            var factory = new WorldDropFactory(records);
            var bagDef = NewDef("item.backpack", container: true);
            Assert.IsNotNull(factory.DropCarrier(NewBag(bagDef, 3), Vector3.zero, "level.a"));

            var destination = new WorldDropDestination(factory, () => Vector3.zero, "level.a");
            Assert.IsFalse(destination.TryAccept(NewBag(bagDef, 3), out var reason));
            Assert.AreEqual("DropRejected", reason, "记录层拒绝 ⇒ 接收失败（编排器据此回滚）");
        }

        [Test]
        public void 长按换包_新包上身_旧包整包落世界_源记录删除()
        {
            var records = NewStore();
            var destination = new WorldDropDestination(new WorldDropFactory(records), () => new Vector3(0f, 0f, 1f), "level.a");
            var (body, coordinator) = NewRig(records, destination);
            var bagDef = NewDef("item.backpack", container: true);
            var worn = NewBag(bagDef, 3);
            coordinator.RequestSwapCarrier(worn, null);

            var incoming = NewBag(bagDef, 3);
            var source = new WorldInstanceSource(incoming, recordId: 42);
            var receipt = coordinator.RequestPickupCarrier(source, PickupIntent.ForceHold);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
            Assert.AreEqual("CarrierSwapped", receipt.Reason);
            Assert.AreSame(incoming, body.GetInstance(BodySlotType.Back), "新包上身");
            Assert.IsFalse(source.HasContent, "源已空");
            Assert.IsFalse(records.TryGet(42, out _), "源记录已删（拾取成功 → 记录层删）");

            var dropped = records.RecordsIn("level.a");
            Assert.AreEqual(1, dropped.Count, "旧包落成一条世界记录");
            Assert.AreSame(worn, dropped[0].Instance, "旧包整包落地（同一个实例对象）");
        }

        [Test]
        public void 长按换包_目的地失败_完整回滚()
        {
            var records = new WorldRecordStore(new InstanceIdAllocator(), maxRecordsPerGroup: 1);
            var bagDef = NewDef("item.backpack", container: true);
            Assert.IsNotNull(new WorldDropFactory(records).DropCarrier(NewBag(bagDef, 3), Vector3.zero, "level.a"));
            var destination = new WorldDropDestination(new WorldDropFactory(records), () => Vector3.zero, "level.a");

            var (body, coordinator) = NewRig(records, destination);
            var worn = NewBag(bagDef, 3);
            coordinator.RequestSwapCarrier(worn, null);
            var incoming = NewBag(bagDef, 3);
            var source = new WorldInstanceSource(incoming, recordId: 7);
            var removals = 0;
            records.Changed += args =>
            {
                if (args.Kind == WorldRecordChangeKind.Removed)
                {
                    removals++;
                }
            };

            var receipt = coordinator.RequestPickupCarrier(source, PickupIntent.ForceHold);

            Assert.AreEqual(OperationStatus.Rejected, receipt.Status);
            Assert.AreEqual("DropRejected", receipt.Reason, "目的地的类型化原因进回执");
            Assert.AreSame(worn, body.GetInstance(BodySlotType.Back), "旧包放回背槽");
            Assert.IsTrue(source.HasContent, "新包原样放回源（不丢实例）");
            Assert.AreEqual(0, removals, "失败路径不得触发任何记录删除");
            Assert.AreEqual(1, records.CountIn("level.a"), "记录表零改动");
        }

        [Test]
        public void 收起_背包满_有世界掉落口_丢到世界()
        {
            var records = NewStore();
            var destination = new WorldDropDestination(new WorldDropFactory(records), () => new Vector3(0f, 0f, 1f), "level.a");
            var (body, coordinator) = NewRig(records, destination);
            var axeDef = NewDef("item.axe", container: false);
            var fullBag = new Inventory(0); // 0 格 = 满

            // 先正常"拿"到手上（走真实拾取：1 单位 → 手上；余量 0 → 不碰背包）
            var world = new WorldStackSource(axeDef, 1);
            coordinator.RequestPickup(world, axeDef, 1, PickupIntent.ForceHold, fullBag);
            Pump(coordinator);
            Assert.AreSame(axeDef, body.Get(BodySlotType.Hand), "前提：手上拿着斧头");

            var receipt = coordinator.RequestUnequip(fullBag);
            Pump(coordinator);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status, "收起失败 → 落到世界，而不是停在失败");
            Assert.AreEqual("StowedToWorld", receipt.Reason);
            Assert.IsTrue(body.IsEmpty(BodySlotType.Hand), "东西已离开手槽");
            var dropped = records.RecordsIn("level.a");
            Assert.AreEqual(1, dropped.Count, "落成一条世界记录");
            Assert.AreSame(axeDef, dropped[0].Definition);
            Assert.AreEqual(1, dropped[0].Count);
        }

        [Test]
        public void 收起_背包满_无世界掉落口_失败且留在手上()
        {
            var records = NewStore();
            var body = new Equipment(new SlotBase[] { new HandSlot(), new BackSlot() });
            var behavior = new EquipController();
            behavior.SetPaused(false);
            var coordinator = new ItemOperationCoordinator(body, behavior, removeRecord: records.TryRemove);
            _coordinators.Add(coordinator);
            var axeDef = NewDef("item.axe", container: false);
            var fullBag = new Inventory(0);
            coordinator.RequestPickup(new WorldStackSource(axeDef, 1), axeDef, 1, PickupIntent.ForceHold, fullBag);
            Pump(coordinator);

            var receipt = coordinator.RequestUnequip(fullBag);
            Pump(coordinator);

            Assert.AreEqual(OperationStatus.Failed, receipt.Status, "未装配掉落口 → 维持原语义（明确失败）");
            Assert.AreEqual("DestinationRejected", receipt.Reason);
            Assert.AreSame(axeDef, body.Get(BodySlotType.Hand), "东西仍在手上（不静默丢）");
            Assert.AreEqual(0, records.CountIn("level.a"));
        }

        [Test]
        public void 收起_无包_有世界掉落口_丢到世界()
        {
            var records = NewStore();
            var destination = new WorldDropDestination(new WorldDropFactory(records), () => new Vector3(0f, 0f, 1f), "level.a");
            var (body, coordinator) = NewRig(records, destination);
            var axeDef = NewDef("item.axe", container: false);

            // 无包态长按拿取：一单位到手（没有背包可入，余量留在世界源里）
            coordinator.RequestPickup(new WorldStackSource(axeDef, 1), axeDef, 1, PickupIntent.ForceHold, null);
            Pump(coordinator);
            Assert.AreSame(axeDef, body.Get(BodySlotType.Hand), "前提：无包，手上拿着误拾的斧头");

            var receipt = coordinator.RequestUnequip(null); // 无包 = 没有容器收货
            Pump(coordinator);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status, "无包时收起 → 直接落地，不该被拒");
            Assert.AreEqual("StowedToWorld", receipt.Reason);
            Assert.IsTrue(body.IsEmpty(BodySlotType.Hand), "东西已离开手槽");
            var dropped = records.RecordsIn("level.a");
            Assert.AreEqual(1, dropped.Count, "落成一条世界记录");
            Assert.AreSame(axeDef, dropped[0].Definition);
            Assert.AreEqual(1, dropped[0].Count);
        }

        [Test]
        public void 收起_无包_无世界掉落口_失败且留在手上()
        {
            var records = NewStore();
            var body = new Equipment(new SlotBase[] { new HandSlot(), new BackSlot() });
            var behavior = new EquipController();
            behavior.SetPaused(false);
            var coordinator = new ItemOperationCoordinator(body, behavior, removeRecord: records.TryRemove);
            _coordinators.Add(coordinator);
            var axeDef = NewDef("item.axe", container: false);

            coordinator.RequestPickup(new WorldStackSource(axeDef, 1), axeDef, 1, PickupIntent.ForceHold, null);
            Pump(coordinator);

            var receipt = coordinator.RequestUnequip(null);
            Pump(coordinator);

            Assert.AreEqual(OperationStatus.Failed, receipt.Status, "未装配掉落口 → 明确失败");
            Assert.AreEqual("DestinationRejected", receipt.Reason);
            Assert.AreSame(axeDef, body.Get(BodySlotType.Hand), "东西仍在手上（不静默丢）");
            Assert.AreEqual(0, records.CountIn("level.a"));
        }

        [Test]
        public void 无包_手上有物_长按拿取_旧物落世界_新物到手()
        {
            var records = NewStore();
            var destination = new WorldDropDestination(new WorldDropFactory(records), () => new Vector3(0f, 0f, 1f), "level.a");
            var (body, coordinator) = NewRig(records, destination);
            var axeDef = NewDef("item.axe", container: false);
            var woodDef = NewDef("item.wood", container: false);

            // 无包先拿一手斧头
            coordinator.RequestPickup(new WorldStackSource(axeDef, 1), axeDef, 1, PickupIntent.ForceHold, null);
            Pump(coordinator);
            Assert.AreSame(axeDef, body.Get(BodySlotType.Hand), "前提：无包，手上拿着斧头");

            // 无包再长按拿木头：旧物（斧头）落到世界，新物（木头）到手
            var receipt = coordinator.RequestPickup(new WorldStackSource(woodDef, 2), woodDef, 2, PickupIntent.ForceHold, null);
            Pump(coordinator);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status, "旧物落世界 + 新物到手，不该被拒");
            Assert.IsTrue(receipt.OldItemTransferred, "回执标明旧物已转出");
            Assert.AreSame(woodDef, body.Get(BodySlotType.Hand), "新物到手");
            var dropped = records.RecordsIn("level.a");
            Assert.AreEqual(1, dropped.Count, "旧物落成一条世界记录");
            Assert.AreSame(axeDef, dropped[0].Definition);
            Assert.AreEqual(1, dropped[0].Count);
        }

        [Test]
        public void 无包_手上有同定义可持物_长按拿取_旧物落世界_新物到手()
        {
            var records = NewStore();
            var destination = new WorldDropDestination(new WorldDropFactory(records), () => new Vector3(0f, 0f, 1f), "level.a");
            var (body, coordinator) = NewRig(records, destination);
            var axeDef = NewDef("item.axe", container: false);

            // 无包先拿一手斧头
            coordinator.RequestPickup(new WorldStackSource(axeDef, 1), axeDef, 1, PickupIntent.ForceHold, null);
            Pump(coordinator);
            Assert.AreSame(axeDef, body.Get(BodySlotType.Hand), "前提：无包，手上拿着一把斧头");

            // 无包再长按另一把**同款**斧头：旧的那把落世界，新的那把到手
            var secondSource = new WorldStackSource(axeDef, 2);
            var receipt = coordinator.RequestPickup(secondSource, axeDef, 2, PickupIntent.ForceHold, null);
            Pump(coordinator);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status, "同定义换手也应成立");
            Assert.IsTrue(receipt.OldItemTransferred, "回执标明旧物已转出");
            Assert.AreSame(axeDef, body.Get(BodySlotType.Hand), "手上仍是一把斧头（无状态堆叠只按定义判定）");
            Assert.AreEqual(1, body.CountOf(axeDef), "手上仍只有一把");

            var dropped = records.RecordsIn("level.a");
            Assert.AreEqual(1, dropped.Count, "旧的那一把落成一条世界记录");
            Assert.AreSame(axeDef, dropped[0].Definition);
            Assert.AreEqual(1, dropped[0].Count);
            Assert.AreEqual(1, secondSource.Remaining, "新的那把被取走一个，余量留在源里");
        }

        [Test]
        public void 拿取一堆_背包满_一手到手_余量落成一条堆叠记录()
        {
            var records = NewStore();
            var destination = new WorldDropDestination(new WorldDropFactory(records), () => new Vector3(0f, 0f, 1f), "level.a");
            var (body, coordinator) = NewRig(records, destination);
            var woodDef = NewDef("item.wood", container: false);
            var fullBag = new Inventory(0);
            var source = new WorldStackSource(woodDef, 5);

            var receipt = coordinator.RequestPickup(source, woodDef, 5, PickupIntent.ForceHold, fullBag);
            Pump(coordinator);

            Assert.AreEqual(OperationStatus.Completed, receipt.Status, "不应整笔被拒：手上那一单位必须落地成功");
            Assert.AreEqual("RemainderDropped", receipt.Reason);
            Assert.AreSame(woodDef, body.Get(BodySlotType.Hand), "一单位到手");
            Assert.AreEqual(0, source.Remaining, "源被取空");

            var dropped = records.RecordsIn("level.a");
            Assert.AreEqual(1, dropped.Count, "余量 4 个落成**一条**堆叠记录（一个视图，不是 4 个预制体）");
            Assert.AreSame(woodDef, dropped[0].Definition);
            Assert.AreEqual(4, dropped[0].Count, "数量记在记录里");
        }

        /// <summary>推进到操作终局（Draw/Stow 按时间推进，需 Tick）。</summary>
        private static void Pump(ItemOperationCoordinator coordinator)
        {
            const float step = 1f / 60f;
            for (float t = 0f; t < 8f && coordinator.Current != null; t += step)
            {
                coordinator.Tick(step);
            }
        }

        private WorldRecordStore NewStore() => new(new InstanceIdAllocator(), 0, _ => { });

        private (Equipment Body, ItemOperationCoordinator Coordinator) NewRig(WorldRecordStore records, IWorldDropPort destination)
        {
            var body = new Equipment(new SlotBase[] { new HandSlot(), new BackSlot() });
            var behavior = new EquipController();
            behavior.SetPaused(false);
            var coordinator = new ItemOperationCoordinator(
                body, behavior,
                removeRecord: records.TryRemove,
                worldDrop: () => destination);
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
            // 非容器物品要在测试里"上手"，必须挂可持面（手槽的接纳谓词 = HoldableFacet）
            GetFacetsList(def).Add(container ? (IItemFacet)new ContainerFacet() : new HoldableFacet());
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
