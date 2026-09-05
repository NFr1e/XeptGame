using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Items;
using XeptGame.Inv;

namespace XeptGame.Tests
{
    /// <summary>
    /// Inventory（纯 C# 聚合计数容器）语义测试（ItemLoop_Design.md §4.2/§4.3）：
    /// 稳定顺序列表与同定义合并、TryRemove 原子（不足不改动）、CountOf/Contains、
    /// Changed 细粒度负载（OldCount/NewCount、行移除 = NewCount 0）、Clear 逐行推送。
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
            var changes = new List<InventoryChangeArgs>();
            inv.Changed += changes.Add;

            inv.Add(a, 5);

            Assert.AreEqual(1, inv.Slots.Count);
            Assert.AreSame(a, inv.Slots[0].Definition);
            Assert.AreEqual(5, inv.Slots[0].Count);
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

            inv.Add(a, 2);
            inv.Add(a, 3);

            Assert.AreEqual(1, inv.Slots.Count, "同定义必须合并到同一行");
            Assert.AreEqual(5, inv.Slots[0].Count);
        }

        [Test]
        public void Add_异定义_保持加入序()
        {
            var inv = new Inventory();
            var a = NewDef("item.a");
            var b = NewDef("item.b");

            inv.Add(a, 1);
            inv.Add(b, 1);
            inv.Add(a, 1); // 合并不移动 a 的行

            Assert.AreEqual(2, inv.Slots.Count);
            Assert.AreSame(a, inv.Slots[0].Definition);
            Assert.AreSame(b, inv.Slots[1].Definition);
            Assert.AreEqual(2, inv.Slots[0].Count);
        }

        [Test]
        public void CountOf_不存在返回0_Contains对应()
        {
            var inv = new Inventory();
            var a = NewDef("item.a");
            var b = NewDef("item.b");

            inv.Add(a, 3);

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
            inv.Add(a, 5);
            var changes = new List<InventoryChangeArgs>();
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
            inv.Add(a, 5);
            var changes = new List<InventoryChangeArgs>();
            inv.Changed += changes.Add;
            changes.Clear();

            Assert.IsTrue(inv.TryRemove(a, 5));
            Assert.AreEqual(0, inv.CountOf(a));
            Assert.IsFalse(inv.Contains(a));
            Assert.AreEqual(0, inv.Slots.Count, "扣至 0 的行必须被移除");

            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(5, changes[0].OldCount);
            Assert.AreEqual(0, changes[0].NewCount, "行移除以 NewCount=0 宣布");
        }

        [Test]
        public void TryRemove_不足_返回false且不改动不推送()
        {
            var inv = new Inventory();
            var a = NewDef("item.a");
            inv.Add(a, 5);
            var events = 0;
            inv.Changed += _ => events++;

            Assert.IsFalse(inv.TryRemove(a, 6), "数量不足必须失败");
            Assert.AreEqual(5, inv.CountOf(a), "原子性：不足时不得扣减");
            Assert.AreEqual(1, inv.Slots.Count);
            Assert.AreEqual(0, events, "未发生实际变化不得推送");
        }

        [Test]
        public void TryRemove_不存在的定义_返回false()
        {
            var inv = new Inventory();
            var a = NewDef("item.a");
            var ghost = NewDef("item.ghost");
            inv.Add(a, 5);
            var events = 0;
            inv.Changed += _ => events++;

            Assert.IsFalse(inv.TryRemove(ghost, 1));
            Assert.AreEqual(5, inv.CountOf(a));
            Assert.AreEqual(0, events);
        }

        [Test]
        public void Clear_清空所有行_每行推送NewCount0()
        {
            var inv = new Inventory();
            var a = NewDef("item.a");
            var b = NewDef("item.b");
            inv.Add(a, 2);
            inv.Add(b, 7);
            var changes = new List<InventoryChangeArgs>();
            inv.Changed += changes.Add;
            changes.Clear();

            inv.Clear();

            Assert.AreEqual(0, inv.Slots.Count);
            Assert.AreEqual(0, inv.CountOf(a));
            Assert.AreEqual(0, inv.CountOf(b));
            Assert.AreEqual(2, changes.Count, "每行一条移除推送");
            Assert.IsTrue(changes.TrueForAll(c => c.NewCount == 0));
        }
    }
}
