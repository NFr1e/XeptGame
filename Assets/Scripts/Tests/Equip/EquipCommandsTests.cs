using System.Collections.Generic;
using NUnit.Framework;
using XeptGame.Equip;
using XeptGame.Inv;
using XeptGame.Items;

namespace XeptGame.Tests
{
    /// <summary>
    /// EquipCommands 命令层测试（Equip_FPV_Design.md §2.4/§3.3 + §8 路由矩阵，T3）：
    /// tap（手空 1 上手 N−1 入包 / 手满全入包 / 不可持全入包）、hold（强制上手、手满先回包）、
    /// 已持同物全入包、PutAway（转移 + 意图终止、不补位）、守卫零副作用、播报总量一次。
    /// </summary>
    public class EquipCommandsTests : EquipTestBase
    {
        private EquipCommands _commands;
        private Inventory _bag;
        private Equipment _body;
        private List<ItemAcquiredEvent> _acquired;

        [SetUp]
        public void SetUp()
        {
            _bag = NewBag();
            _body = NewBody();
            // 收集器随 SetUp 重建：Test Runner 可能复用夹具实例，readonly 字段只在构造时初始化一次，
            // 否则播报事件会在用例间累积（本类历史失败根因）。
            _acquired = new List<ItemAcquiredEvent>();
            _commands = new EquipCommands(_bag, _body, _acquired.Add);
        }

        [Test]
        public void Tap_手空count1_整份上手不収包()
        {
            var a = NewDef("item.a", true);

            Assert.IsTrue(_commands.TryPickupRoute(a, 1, PickupMode.Tap));

            Assert.AreSame(a, _body.Get(BodySlotType.Hand));
            Assert.AreEqual(0, _bag.CountOf(a));
            Assert.AreEqual(1, _acquired.Count, "播报总量一次");
            Assert.AreSame(a, _acquired[0].Item);
            Assert.AreEqual(1, _acquired[0].Count);
        }

        [Test]
        public void Tap_手空count3_一手二包()
        {
            var a = NewDef("item.a", true);
            var bagChanges = new List<InventoryChangeArgs>();
            _bag.Changed += bagChanges.Add;

            Assert.IsTrue(_commands.TryPickupRoute(a, 3, PickupMode.Tap));

            Assert.AreSame(a, _body.Get(BodySlotType.Hand));
            Assert.AreEqual(2, _bag.CountOf(a));
            Assert.AreEqual(1, bagChanges.Count, "包仅余数变更一条（0→2）");
            Assert.AreEqual(3, _acquired[0].Count);
        }

        [Test]
        public void Tap_手满_全入包()
        {
            var held = NewDef("item.held", true);
            _body.TryAdd(held, 1);
            var b = NewDef("item.b", true);

            Assert.IsTrue(_commands.TryPickupRoute(b, 2, PickupMode.Tap));

            Assert.AreSame(held, _body.Get(BodySlotType.Hand), "手槽保持原物");
            Assert.AreEqual(2, _bag.CountOf(b));
            Assert.AreEqual(2, _acquired[0].Count);
        }

        [Test]
        public void Tap_不可持物_全入包()
        {
            var c = NewDef("item.c", false);

            Assert.IsTrue(_commands.TryPickupRoute(c, 3, PickupMode.Tap));

            Assert.IsTrue(_body.IsEmpty(BodySlotType.Hand));
            Assert.AreEqual(3, _bag.CountOf(c));
            Assert.AreEqual(3, _acquired[0].Count);
        }

        [Test]
        public void ForceHold_手满_当前物回包后换手()
        {
            var held = NewDef("item.held", true);
            _body.TryAdd(held, 1);
            var b = NewDef("item.b", true);

            Assert.IsTrue(_commands.TryPickupRoute(b, 2, PickupMode.ForceHold));

            Assert.AreSame(b, _body.Get(BodySlotType.Hand), "新物上手");
            Assert.AreEqual(1, _bag.CountOf(held), "原持物回包 1");
            Assert.AreEqual(1, _bag.CountOf(b), "余数 1 入包");
            Assert.AreEqual(2, _acquired[0].Count, "播报按总量（新物 2）一次");
        }

        [Test]
        public void ForceHold_已持同物_全入包不可叠手()
        {
            var a = NewDef("item.a", true);
            _body.TryAdd(a, 1);

            Assert.IsTrue(_commands.TryPickupRoute(a, 2, PickupMode.ForceHold));

            Assert.AreSame(a, _body.Get(BodySlotType.Hand), "手槽原样（1 单位）");
            Assert.AreEqual(2, _bag.CountOf(a), "新增全入包");
            Assert.AreEqual(2, _acquired[0].Count);
        }

        [Test]
        public void ForceHold_手空_同Tap直接上手()
        {
            var a = NewDef("item.a", true);

            Assert.IsTrue(_commands.TryPickupRoute(a, 1, PickupMode.ForceHold));

            Assert.AreSame(a, _body.Get(BodySlotType.Hand));
            Assert.AreEqual(0, _bag.CountOf(a));
        }

        [Test]
        public void 守卫_def为空或数量非正_返回false零副作用()
        {
            var a = NewDef("item.a", true);
            var events = 0;
            _body.SlotChanged += _ => events++;
            _bag.Changed += _ => events++;

            Assert.IsFalse(_commands.TryPickupRoute(null, 1, PickupMode.Tap));
            Assert.IsFalse(_commands.TryPickupRoute(a, 0, PickupMode.Tap));
            Assert.IsFalse(_commands.TryPickupRoute(a, -1, PickupMode.ForceHold));

            Assert.IsTrue(_body.IsEmpty(BodySlotType.Hand));
            Assert.AreEqual(0, _bag.CountOf(a));
            Assert.IsEmpty(_acquired);
            Assert.AreEqual(0, events);
        }

        [Test]
        public void PutAway_空手_返回false零副作用()
        {
            var events = 0;
            _body.SlotChanged += _ => events++;
            _bag.Changed += _ => events++;

            Assert.IsFalse(_commands.PutAway());
            Assert.AreEqual(0, events);
        }

        [Test]
        public void PutAway_持物_回包合并并清手槽不补位()
        {
            var a = NewDef("item.a", true);
            _commands.TryPickupRoute(a, 1, PickupMode.Tap);
            Assert.AreEqual(0, _bag.CountOf(a));
            var slotChanges = new List<SlotChangeArgs>();
            _body.SlotChanged += slotChanges.Add;

            Assert.IsTrue(_commands.PutAway());

            Assert.IsTrue(_body.IsEmpty(BodySlotType.Hand), "手槽释放、无补位（意图终止）");
            Assert.AreEqual(1, _bag.CountOf(a), "回包合并");
            Assert.AreEqual(1, slotChanges.Count, "仅手槽清空一条");
            Assert.AreEqual(1, _acquired.Count, "PutAway 不产生拾取播报");
        }
    }
}
