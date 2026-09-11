using System;
using System.Collections.Generic;
using NUnit.Framework;
using XeptGame.Container;
using XeptGame.Equip;
using XeptGame.Inv;
using XeptGame.Items;
using XeptGame.Items.Operations;

namespace XeptGame.Tests
{
    /// <summary>旧路由的最终占用语义迁移；以假世界端点和显式时间验证新提交时序。</summary>
    public class ItemOperationTests : EquipTestBase
    {
        private Equipment _body;
        private Inventory _bag;
        private EquipController _behavior;
        private ItemOperationCoordinator _ops;
        private List<ItemAcquiredEvent> _events;
        [SetUp]
        public void Setup()
        {
            _body = NewBody();
            _bag = NewBag();
            _events = new();
            _behavior = new EquipController();
            _ops = new ItemOperationCoordinator(_body, _behavior, _events.Add, _ => new EquipTiming(1f, 1f));
        }

        [TearDown]
        public void Cleanup()
        {
            _ops.Dispose();
            _behavior.Dispose();
        }

        private OperationReceipt Pickup(ItemDefinition item, int count = 1, PickupIntent intent = PickupIntent.Tap) => _ops.RequestPickup(new WorldItemContainer(item, count), item, count, intent, _bag);
        private void Hold(ItemDefinition item)
        {
            Pickup(item);
            _ops.Tick(1);
        }

        [TestCase(1)]
        [TestCase(3)]
        public void Tap_手空_一手余数入包_完成不双播(int count)
        {
            var item = NewDef("stone", true);
            var changes = new List<ContainerChangeArgs>();
            _bag.Changed += changes.Add;
            var receipt = Pickup(item, count);
            Assert.AreEqual(OperationStatus.Pending, receipt.Status);
            Assert.AreEqual(EquipPhase.Drawing, _behavior.Snapshot.Phase);
            Assert.AreSame(item, _body.Get(BodySlotType.Hand));
            Assert.AreEqual(count - 1, _bag.CountOf(item));
            Assert.AreEqual(count > 1 ? 1 : 0, changes.Count);
            Assert.AreEqual(1, _events.Count);
            Assert.AreSame(item, _events[0].Item);
            Assert.AreEqual(count, _events[0].Count);
            _ops.Tick(1);
            _ops.Tick(1);
            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
            Assert.AreEqual(1, _events.Count);
        }

        [TestCase(PickupIntent.Tap)]
        [TestCase(PickupIntent.ForceHold)]
        public void 不可持_不影响原手持物(PickupIntent intent)
        {
            var held = NewDef("held", true);
            Hold(held);
            _events.Clear();
            var item = NewDef("drug", false);
            Assert.AreEqual(OperationStatus.Completed, Pickup(item, 3, intent).Status);
            Assert.AreSame(held, _body.Get(BodySlotType.Hand));
            Assert.AreEqual(EquipPhase.Ready, _behavior.Snapshot.Phase);
            Assert.AreEqual(3, _bag.CountOf(item));
            Assert.AreEqual(3, _events[0].Count);
        }

        [Test]
        public void Tap_手满全部入包()
        {
            var held = NewDef("held", true);
            Hold(held);
            var item = NewDef("new", true);
            Pickup(item, 2);
            Assert.AreSame(held, _body.Get(BodySlotType.Hand));
            Assert.AreEqual(2, _bag.CountOf(item));
        }

        [Test]
        public void Hold_手满等待收回才换手()
        {
            var held = NewDef("held", true);
            Hold(held);
            _events.Clear();
            var item = NewDef("new", true);
            var source = new WorldItemContainer(item, 2);
            var receipt = _ops.RequestPickup(source, item, 2, PickupIntent.ForceHold, _bag);
            Assert.AreSame(held, _body.Get(BodySlotType.Hand));
            Assert.AreEqual(0, _bag.CountOf(held));
            Assert.AreEqual(2, source.Remaining);
            Assert.IsEmpty(_events);
            Assert.AreSame(receipt, _ops.RequestPickup(source, item, 2, PickupIntent.ForceHold, _bag));
            _ops.Tick(1);
            Assert.AreSame(item, _body.Get(BodySlotType.Hand));
            Assert.AreEqual(1, _bag.CountOf(held));
            Assert.AreEqual(1, _bag.CountOf(item));
            Assert.AreEqual(0, source.Remaining);
            Assert.IsFalse(source.IsBusy);
            Assert.AreEqual(2, _events[0].Count);
            _ops.Tick(1);
            Assert.AreEqual(1, _events.Count);
        }

        [Test]
        public void Hold_同物只入包_空手可以拿出()
        {
            var item = NewDef("stone", true);
            Pickup(item, 1, PickupIntent.ForceHold);
            _ops.Tick(1);
            Pickup(item, 2, PickupIntent.ForceHold);
            Assert.AreEqual(2, _bag.CountOf(item));
            Assert.AreEqual(1, _body.CountOf(item));
        }

        [Test]
        public void 收回_等待提交合并无补位无播报()
        {
            Assert.IsFalse(_ops.RequestUnequip(_bag).Accepted);
            var item = NewDef("stone", true);
            Hold(item);
            _bag.TryAdd(item, 2);
            var receipt = _ops.RequestUnequip(_bag);
            Assert.AreEqual(2, _bag.CountOf(item));
            Assert.AreSame(item, _body.Get(BodySlotType.Hand));
            Assert.AreSame(receipt, _ops.RequestUnequip(_bag));
            _ops.Tick(1);
            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
            Assert.IsTrue(_body.IsEmpty(BodySlotType.Hand));
            Assert.AreEqual(3, _bag.CountOf(item));
            Assert.AreEqual(1, _events.Count);
        }

        [Test]
        public void 守卫零副作用()
        {
            var item = NewDef("stone", true);
            var source = new WorldItemContainer(item, 1);
            Assert.IsFalse(_ops.RequestPickup(source, null, 1, PickupIntent.Tap, _bag).Accepted);
            Assert.IsFalse(_ops.RequestPickup(source, item, 0, PickupIntent.Tap, _bag).Accepted);
            Assert.IsFalse(_ops.RequestPickup(source, item, -1, PickupIntent.ForceHold, _bag).Accepted);
            Assert.AreEqual(1, source.Remaining);
            Assert.IsEmpty(_events);
            Assert.IsTrue(_body.IsEmpty(BodySlotType.Hand));
        }

        [Test]
        public void 拿出中收回_取消不撤销获得()
        {
            var item = NewDef("stone", true);
            var first = Pickup(item);
            _ops.RequestUnequip(_bag);
            _ops.Tick(1);
            Assert.AreEqual(OperationStatus.Cancelled, first.Status);
            Assert.AreEqual(1, _bag.CountOf(item));
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual(EquipPhase.Empty, _behavior.Snapshot.Phase);
        }

        [Test]
        public void 转出拒绝_停驻Stowed_可重试()
        {
            var item = NewDef("stone", true);
            Hold(item);
            var receipt = _ops.RequestUnequip(new RejectingContainer());
            _ops.Tick(1);
            Assert.AreEqual(OperationStatus.Failed, receipt.Status);
            Assert.AreEqual(EquipPhase.Stowed, _behavior.Snapshot.Phase);
            Assert.AreSame(item, _body.Get(BodySlotType.Hand));
            Assert.AreEqual(OperationStatus.Completed, _ops.RequestUnequip(_bag).Status);
        }

        [Test]
        public void 换物源失效_取消与会话终止释放标记()
        {
            Hold(NewDef("held", true));
            var item = NewDef("new", true);
            bool available = true;
            var source = new WorldItemContainer(item, 1, () => available);
            var receipt = _ops.RequestPickup(source, item, 1, PickupIntent.ForceHold, _bag);
            available = false;
            _ops.Tick(1);
            Assert.AreEqual(OperationStatus.Failed, receipt.Status);
            Assert.IsFalse(source.IsBusy);
            Assert.AreEqual(1, source.Remaining);
        }

        [Test]
        public void 事件重入拒绝_暂停不推进()
        {
            var item = NewDef("stone", true);
            OperationReceipt nested = null;
            _body.SlotChanged += _ => nested = _ops.RequestUnequip(_bag);
            Pickup(item);
            Assert.AreEqual(OperationStatus.Rejected, nested.Status);
            _behavior.SetPaused(true);
            _ops.Tick(5);
            Assert.AreEqual(EquipPhase.Drawing, _behavior.Snapshot.Phase);
            Assert.IsFalse(_ops.RequestUnequip(_bag).Accepted);
            _behavior.SetPaused(false);
            _ops.Tick(1);
            Assert.AreEqual(EquipPhase.Ready, _behavior.Snapshot.Phase);
        }

        [Test]
        public void 部分提交仅播实际量()
        {
            var item = NewDef("stone", true);
            var source = new WorldItemContainer(item, 3);
            var receipt = _ops.RequestPickup(source, item, 3, PickupIntent.Tap, new RejectingContainer());
            Assert.AreEqual(OperationStatus.Failed, receipt.Status);
            Assert.AreEqual(2, source.Remaining);
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual(1, _events[0].Count);
            Assert.AreSame(item, _body.Get(BodySlotType.Hand));
            Assert.AreEqual(EquipPhase.Stowed, _behavior.Snapshot.Phase);
        }

        [Test]
        public void 从同一背包换物_不属于自转()
        {
            var held = NewDef("held", true);
            Hold(held);
            var next = NewDef("next", true);
            _bag.TryAdd(next, 1);
            var receipt = _ops.RequestEquip(_bag, next, _bag);
            Assert.IsTrue(receipt.Accepted);
            _ops.Tick(1);
            _ops.Tick(1);
            Assert.AreEqual(OperationStatus.Completed, receipt.Status);
            Assert.AreSame(next, _body.Get(BodySlotType.Hand));
            Assert.AreEqual(1, _bag.CountOf(held));
            Assert.AreEqual(0, _bag.CountOf(next));
            Assert.AreEqual(1, _events.Count);
        }

        [Test]
        public void 取消等待与结束会话_源锁释放且终局单次()
        {
            Hold(NewDef("held", true));
            var next = NewDef("next", true);
            var source = new WorldItemContainer(next, 1);
            int finishes = 0;
            _ops.OperationFinished += _ => finishes++;
            var receipt = _ops.RequestPickup(source, next, 1, PickupIntent.ForceHold, _bag);
            Assert.IsTrue(source.IsBusy);
            Assert.IsTrue(_ops.CancelOperation(receipt.Id));
            Assert.IsFalse(source.IsBusy);
            Assert.IsFalse(_ops.CancelOperation(receipt.Id));
            _ops.Tick(1);
            Assert.AreEqual(EquipPhase.Stowed, _behavior.Snapshot.Phase);
            var second = _ops.RequestPickup(source, next, 1, PickupIntent.ForceHold, _bag);
            _ops.Dispose();
            _ops.Dispose();
            Assert.AreEqual(OperationStatus.Cancelled, second.Status);
            Assert.AreEqual(2, finishes);
            Assert.IsFalse(source.IsBusy);
        }

        [Test]
        public void 同定义被外部移出再入_占用版本更新()
        {
            var item = NewDef("same", true);
            Hold(item);
            var version = _behavior.Snapshot.OccupancyVersion;
            _body.TryRemove(item, 1);
            _body.TryAdd(item, 1);
            _ops.Tick(0);
            Assert.Greater(_behavior.Snapshot.OccupancyVersion, version);
            Assert.AreEqual(EquipPhase.Stowed, _behavior.Snapshot.Phase);
        }

        [Test]
        public void 转出之后新源不足_保留已提交步骤()
        {
            var held = NewDef("held", true);
            Hold(held);
            var next = NewDef("next", true);
            var source = NewBag();
            source.TryAdd(next, 1);
            var receipt = _ops.RequestEquip(source, next, _bag);
            _bag.Changed += _ => source.TryRemove(next, 1);
            _ops.Tick(1);
            Assert.AreEqual(OperationStatus.Failed, receipt.Status);
            Assert.IsTrue(receipt.OldItemTransferred);
            Assert.IsTrue(_body.IsEmpty(BodySlotType.Hand));
            Assert.AreEqual(1, _bag.CountOf(held));
        }

        [Test]
        public void 收起_优先归位回原格()
        {
            var bag = new Inventory(4);
            var blocker = NewDef("item.blocker", false); // 不可持：只为占格
            var item = NewDef("item.held", true);
            bag.TryAdd(blocker, 1);    // 格 0
            bag.TryAdd(item, 1);       // 格 1
            bag.TryRemove(blocker, 1); // 格 0 空出 → "最小空格"≠ 原格，用于区分归位与兜底

            var equip = _ops.RequestEquip(bag, item, bag);
            _ops.Tick(1);
            _ops.Tick(1);
            Assert.AreEqual(OperationStatus.Completed, equip.Status);
            Assert.AreSame(item, _body.Get(BodySlotType.Hand));

            var stow = _ops.RequestUnequip(bag);
            _ops.Tick(1);
            _ops.Tick(1);

            Assert.IsTrue(stow.Accepted);
            Assert.AreSame(item, bag.Slots[1].Item, "收起应回到装备时的原格");
            Assert.IsTrue(bag.Slots[0].IsEmpty, "没有落到更小的空格（兜底分配）");
        }

        [Test]
        public void 收起_原格被占_落回常规分配()
        {
            var bag = new Inventory(4);
            var blocker = NewDef("item.blocker", false);
            var item = NewDef("item.held", true);
            var later = NewDef("item.later", false);
            var other = NewDef("item.other", false);
            bag.TryAdd(blocker, 1);    // 格 0
            bag.TryAdd(item, 1);       // 格 1
            bag.TryRemove(blocker, 1); // 格 0 空

            var equip = _ops.RequestEquip(bag, item, bag);
            _ops.Tick(1);
            _ops.Tick(1);
            Assert.AreEqual(OperationStatus.Completed, equip.Status);

            bag.TryAdd(later, 1); // → 格 0（最小空格）
            bag.TryAdd(other, 1); // → 格 1：正好占了归位目标

            var stow = _ops.RequestUnequip(bag);
            _ops.Tick(1);
            _ops.Tick(1);

            Assert.IsTrue(stow.Accepted);
            Assert.AreSame(item, bag.Slots[2].Item, "归位失败 → 落回常规分配（最小空格）");
            Assert.AreSame(other, bag.Slots[1].Item, "原格占用者不受影响");
        }

        [Test]
        public void 收起_目标换容器_不归位()
        {
            var source = new Inventory(2);
            var target = new Inventory(2);
            var item = NewDef("item.held", true);
            source.TryAdd(item, 1); // 格 0

            var equip = _ops.RequestEquip(source, item, source);
            _ops.Tick(1);
            _ops.Tick(1);
            Assert.AreEqual(OperationStatus.Completed, equip.Status);

            var stow = _ops.RequestUnequip(target);
            _ops.Tick(1);
            _ops.Tick(1);

            Assert.IsTrue(stow.Accepted);
            Assert.AreSame(item, target.Slots[0].Item, "换容器时不归位：进目标容器常规分配");
            Assert.AreEqual(0, source.CountOf(item));
        }

        [Test]
        public void 收起_目标满_失败且仍占手槽()
        {
            var source = new Inventory(2);
            var full = new Inventory(1);
            var blocker = NewDef("item.blocker", false);
            var item = NewDef("item.held", true);
            source.TryAdd(item, 1);
            full.TryAdd(blocker, 1); // 目标唯一格被占

            var equip = _ops.RequestEquip(source, item, source);
            _ops.Tick(1);
            _ops.Tick(1);
            Assert.AreEqual(OperationStatus.Completed, equip.Status);

            var stow = _ops.RequestUnequip(full);
            _ops.Tick(1);
            _ops.Tick(1);

            Assert.AreEqual(OperationStatus.Failed, stow.Status);
            Assert.AreSame(item, _body.Get(BodySlotType.Hand), "转出拒绝 → 仍占手槽");
            Assert.AreEqual(EquipPhase.Stowed, _behavior.Snapshot.Phase);
        }

        private sealed class RejectingContainer : IItemContainer
        {
            public IReadOnlyList<ItemStack> Stacks => Array.Empty<ItemStack>();

            public int CountOf(ItemDefinition item) => 0;
            public bool Contains(ItemDefinition item) => false;
            public bool TryAdd(ItemDefinition item, int count) => false;
            public bool TryRemove(ItemDefinition item, int count) => false;
            public event Action<ContainerChangeArgs> Changed
            {
                add
                {
                }

                remove
                {
                }
            }
        }
    }
}
