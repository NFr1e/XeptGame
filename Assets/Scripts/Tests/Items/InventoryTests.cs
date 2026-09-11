using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Container;
using XeptGame.Core;
using XeptGame.Items;
using XeptGame.Inv;

namespace XeptGame.Tests
{
    /// <summary>
    /// Inventory（背包容器 = 槽容器的背包域特化，SlotStore_Design.md §1/§6）**物品面**语义测试：
    /// 聚合行稳定顺序与同物品合并、TryRemove 原子（不足不改动）、CountOf/Contains、
    /// 聚合轨负载（OldCount/NewCount）、容量全量拒绝与扩缩容的结构性通知。
    /// 槽位面（分格/上限/压缩/丢弃）见 SlotTests / SlotStoreTests。
    /// 假 Definition 用 CreateInstance&lt;ItemDefinition&gt;()（Resolver 测试先例，EditMode 可跑）。
    /// </summary>
    public class InventoryTests
    {
        private readonly List<Object> _owned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _owned)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _owned.Clear();
        }

        private ItemDefinition NewDef(string id)
        {
            // Inventory 按资产引用标识物品（ReferenceEquals），id 仅命名参考，无需回写序列化字段
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            _owned.Add(def);
            return def;
        }

        [Test]
        public void Add_首次追加_按加入序并推送()
        {
            var inv = new Inventory();
            var a = NewDef("item.a");
            var changes = new List<ContainerChangeArgs>();
            inv.Changed += changes.Add;

            inv.TryAdd(a, 5);

            Assert.AreEqual(1, inv.Stacks.Count);
            Assert.AreSame(a, inv.Stacks[0].Definition);
            Assert.AreEqual(5, inv.Stacks[0].Count);
            Assert.AreEqual(5, inv.CountOf(a));
            Assert.IsTrue(inv.Contains(a));

            Assert.AreEqual(1, changes.Count);
            Assert.AreSame(a, changes[0].Item);
            Assert.AreEqual(0, changes[0].OldCount);
            Assert.AreEqual(5, changes[0].NewCount);
        }

        [Test]
        public void Add_同定义_合并就地不新增行()
        {
            var inv = new Inventory();
            var a = NewDef("item.a");

            inv.TryAdd(a, 2);
            inv.TryAdd(a, 3);

            Assert.AreEqual(1, inv.Stacks.Count, "同定义必须合并到同一行");
            Assert.AreEqual(5, inv.Stacks[0].Count);
        }

        [Test]
        public void Add_异定义_保持加入序()
        {
            var inv = new Inventory();
            var a = NewDef("item.a");
            var b = NewDef("item.b");

            inv.TryAdd(a, 1);
            inv.TryAdd(b, 1);
            inv.TryAdd(a, 1); // 合并不移动 a 的行

            Assert.AreEqual(2, inv.Stacks.Count);
            Assert.AreSame(a, inv.Stacks[0].Definition);
            Assert.AreSame(b, inv.Stacks[1].Definition);
            Assert.AreEqual(2, inv.Stacks[0].Count);
        }

        [Test]
        public void CountOf_不存在返回0_Contains对应()
        {
            var inv = new Inventory();
            var a = NewDef("item.a");
            var b = NewDef("item.b");

            inv.TryAdd(a, 3);

            Assert.AreEqual(3, inv.CountOf(a));
            Assert.AreEqual(0, inv.CountOf(b));
            Assert.IsTrue(inv.Contains(a));
            Assert.IsFalse(inv.Contains(b));
        }

        [Test]
        public void TryRemove_足额_扣减并推送()
        {
            var inv = new Inventory();
            var a = NewDef("item.a");
            inv.TryAdd(a, 5);
            var changes = new List<ContainerChangeArgs>();
            inv.Changed += changes.Add;
            changes.Clear(); // 只观察移除

            Assert.IsTrue(inv.TryRemove(a, 2));
            Assert.AreEqual(3, inv.CountOf(a));
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(5, changes[0].OldCount);
            Assert.AreEqual(3, changes[0].NewCount);
        }

        [Test]
        public void TryRemove_扣至零_移除该行()
        {
            var inv = new Inventory();
            var a = NewDef("item.a");
            inv.TryAdd(a, 5);
            var changes = new List<ContainerChangeArgs>();
            inv.Changed += changes.Add;
            changes.Clear();

            Assert.IsTrue(inv.TryRemove(a, 5));
            Assert.AreEqual(0, inv.CountOf(a));
            Assert.IsFalse(inv.Contains(a));
            Assert.AreEqual(0, inv.Stacks.Count, "扣至 0 的行必须被移除");

            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(5, changes[0].OldCount);
            Assert.AreEqual(0, changes[0].NewCount, "行移除以 NewCount=0 宣布");
        }

        [Test]
        public void TryRemove_不足_返回false且不改动不推送()
        {
            var inv = new Inventory();
            var a = NewDef("item.a");
            inv.TryAdd(a, 5);
            var events = 0;
            inv.Changed += _ => events++;

            Assert.IsFalse(inv.TryRemove(a, 6), "数量不足必须失败");
            Assert.AreEqual(5, inv.CountOf(a), "原子性：不足时不得扣减");
            Assert.AreEqual(1, inv.Stacks.Count);
            Assert.AreEqual(0, events, "未发生实际变化不得推送");
        }

        [Test]
        public void TryRemove_不存在的定义_返回false()
        {
            var inv = new Inventory();
            var a = NewDef("item.a");
            var ghost = NewDef("item.ghost");
            inv.TryAdd(a, 5);
            var events = 0;
            inv.Changed += _ => events++;

            Assert.IsFalse(inv.TryRemove(ghost, 1));
            Assert.AreEqual(5, inv.CountOf(a));
            Assert.AreEqual(0, events);
        }

        [Test]
        public void 容量满_整批拒绝零改动()
        {
            var inv = new Inventory(1);
            var a = NewDef("item.a");
            var b = NewDef("item.b");

            Assert.IsTrue(inv.TryAdd(a, 3));
            Assert.IsFalse(inv.TryAdd(b, 1), "只有一格且已被异类占用 → 全量语义整批拒绝");

            Assert.AreEqual(0, inv.CountOf(b));
            Assert.AreEqual(3, inv.CountOf(a));
            Assert.AreEqual(1, inv.Slots.Count);
            Assert.IsFalse(inv.Slots[0].IsEmpty);
        }

        [Test]
        public void 容量变化_扩容追加空格_不动内容()
        {
            var inv = new Inventory(2);
            var a = NewDef("item.a");
            inv.TryAdd(a, 1);
            var layouts = new List<LayoutChangeArgs>();
            inv.LayoutChanged += layouts.Add;

            Assert.AreEqual(0, inv.ApplyCapacity(4), "扩容不丢弃");

            Assert.AreEqual(4, inv.Capacity);
            Assert.AreEqual(1, layouts.Count, "扩容发一条结构性变更");
            Assert.AreEqual(4, layouts[0].Capacity);
            Assert.AreEqual(1, inv.CountOf(a), "扩容不动内容");
            Assert.AreSame(a, inv.Stacks[0].Definition);
        }

        [Test]
        public void 容量来源_扩容者登记与移除_自动应用()
        {
            var inv = new Inventory(3);
            var a = NewDef("item.a");
            inv.TryAdd(a, 1);

            Assert.IsTrue(inv.SetCapacitySource("body:backpack", 2));

            Assert.AreEqual(5, inv.Capacity, "扩容来源自动应用（尾部追加）");
            Assert.AreEqual(1, inv.CountOf(a), "扩容不动内容");
            Assert.AreEqual(1, inv.CapacitySourceCount);

            Assert.IsTrue(inv.RemoveCapacitySource("body:backpack"));
            Assert.AreEqual(3, inv.Capacity, "卸载扩容者 → 自动缩容");
            Assert.AreEqual(0, inv.CapacitySourceCount);
        }

        [Test]
        public void 容量来源_缩容溢出走丢弃出口()
        {
            var discarded = new List<(ItemDefinition Item, int Count)>();
            var inv = new Inventory(1, (item, count) => discarded.Add((item, count)));
            var a = NewDef("item.a");
            var b = NewDef("item.b");
            var c = NewDef("item.c");
            inv.SetCapacitySource("body:backpack", 2); // 1 + 2 = 3
            inv.TryAdd(a, 1);
            inv.TryAdd(b, 1);
            inv.TryAdd(c, 1);
            var changes = new List<ContainerChangeArgs>();
            inv.Changed += changes.Add;

            Assert.IsTrue(inv.RemoveCapacitySource("body:backpack")); // 缩回 1 格

            Assert.AreEqual(1, inv.Capacity);
            Assert.AreEqual(1, inv.CountOf(a));
            Assert.AreEqual(0, inv.CountOf(b), "搬不下的被丢弃");
            Assert.AreEqual(0, inv.CountOf(c));
            Assert.AreEqual(2, discarded.Count, "细则经丢弃出口上报");
            Assert.AreEqual(2, changes.Count, "丢弃改总数 → 聚合轨逐物品可见");
        }

        [Test]
        public void 背包配置_驱动基础格数()
        {
            var profile = ScriptableObject.CreateInstance<InventoryProfile>();
            _owned.Add(profile);
            profile.baseSlots = 7;

            var inv = new Inventory(profile);

            Assert.AreEqual(7, inv.Capacity);
        }

        [Test]
        public void 后置注入配置_幂等且可回退默认()
        {
            var inv = new Inventory(2);
            var profile = ScriptableObject.CreateInstance<InventoryProfile>();
            _owned.Add(profile);
            profile.baseSlots = 9;

            Assert.IsTrue(inv.ApplyProfile(profile), "装配点（基座场景模块）后置注入：配置变化 → 应用");
            Assert.AreEqual(9, inv.Capacity);

            Assert.IsFalse(inv.ApplyProfile(profile), "同值注入不动作（换关场景重载会重复走一遍）");
            Assert.AreEqual(9, inv.Capacity);

            Assert.IsTrue(inv.ApplyProfile(null), "配置留空 → 回退常量默认");
            Assert.AreEqual(XeptGameConsts.Inventory.DefaultCapacity, inv.Capacity);
        }
    }
}
