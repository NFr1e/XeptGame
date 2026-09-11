using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Container;
using XeptGame.Items;

namespace XeptGame.Tests
{
    /// <summary>
    /// SlotStore（通用槽容器）测试（SlotStore_Design.md §6）：固定格数与格号稳定、扩容只追加、
    /// 缩容自动压缩 + 溢出丢弃（聚合轨 + 出口上报）、整理永不丢弃、每格上限按物品声明分格、
    /// 定向放入（归位用）、容量 0、提交门。
    /// 假 Definition 的背包面（maxStack）用反射注入 Facet（EquipTestBase 先例）。
    /// </summary>
    public class SlotStoreTests
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

        [Test]
        public void 扩容_尾部追加空格_内容与格号不动()
        {
            var store = new SlotStore(2);
            var a = NewDef("item.a");
            store.TryAdd(a, 1);
            var layouts = new List<LayoutChangeArgs>();
            store.LayoutChanged += layouts.Add;

            Assert.AreEqual(0, store.ApplyCapacity(4), "扩容不丢弃");

            Assert.AreEqual(4, store.Capacity);
            Assert.AreSame(a, ItemAt(store, 0), "既有格号与内容不动");
            Assert.IsTrue(store.Slots[2].IsEmpty);
            Assert.IsTrue(store.Slots[3].IsEmpty);
            Assert.AreEqual(1, layouts.Count, "扩容只发一条结构性变更（新格必然为空，无逐格事件）");
        }

        [Test]
        public void 扩容_同值_幂等无事件()
        {
            var store = new SlotStore(3);
            var layouts = 0;
            store.LayoutChanged += _ => layouts++;

            Assert.AreEqual(0, store.ApplyCapacity(3));

            Assert.AreEqual(3, store.Capacity);
            Assert.AreEqual(0, layouts, "同值 apply 为 no-op");
        }

        [Test]
        public void 缩容_自动压缩_保留相对顺序()
        {
            var store = new SlotStore(4);
            var a = NewDef("item.a");
            var b = NewDef("item.b");
            store.TryPlaceAt(new SlotId(1), a, 1);
            store.TryPlaceAt(new SlotId(3), b, 1);

            Assert.AreEqual(0, store.ApplyCapacity(2));

            Assert.AreEqual(2, store.Capacity);
            Assert.AreSame(a, ItemAt(store, 0), "保留区稳定缩进");
            Assert.AreSame(b, ItemAt(store, 1), "待裁格按序压入保留区");
        }

        [Test]
        public void 缩容_待裁格并入同类未满格()
        {
            var store = new SlotStore(3);
            var a = NewDef("item.a", 10);
            store.TryPlaceAt(new SlotId(0), a, 4);
            store.TryPlaceAt(new SlotId(2), a, 3);

            Assert.AreEqual(0, store.ApplyCapacity(1), "能放下就不丢弃");

            Assert.AreEqual(1, store.Capacity);
            Assert.AreEqual(7, CountAt(store, 0), "并入同类未满格");
        }

        [Test]
        public void 缩容_溢出丢弃_发聚合轨并上报出口()
        {
            var discarded = new List<(ItemDefinition Item, int Count)>();
            var store = new SlotStore(2, (item, count) => discarded.Add((item, count)));
            var a = NewDef("item.a", 3);
            store.TryPlaceAt(new SlotId(0), a, 3);
            store.TryPlaceAt(new SlotId(1), a, 2);
            var changes = new List<ContainerChangeArgs>();
            store.Changed += changes.Add;

            Assert.AreEqual(2, store.ApplyCapacity(1), "放不下的余量被丢弃");

            Assert.AreEqual(1, store.Capacity);
            Assert.AreEqual(3, store.CountOf(a));
            Assert.AreEqual(1, changes.Count, "丢弃改总数 → 聚合轨必须可见");
            Assert.AreEqual(5, changes[0].OldCount);
            Assert.AreEqual(3, changes[0].NewCount);
            Assert.AreEqual(1, discarded.Count);
            Assert.AreEqual(2, discarded[0].Count, "细则经丢弃出口上报（后期世界 Drop 落同一接缝）");
        }

        [Test]
        public void 缩容到0_全部丢弃_容量0恒拒绝()
        {
            var store = new SlotStore(2);
            var a = NewDef("item.a");
            store.TryAdd(a, 2);

            Assert.AreEqual(2, store.ApplyCapacity(0));

            Assert.AreEqual(0, store.Capacity);
            Assert.AreEqual(0, store.CountOf(a));
            Assert.IsFalse(store.TryAdd(a, 1), "容量 0 = 没有背包：恒失败（Move 会走目标拒绝并回滚源）");
        }

        [Test]
        public void 整理_永不丢弃_只缩进()
        {
            var discarded = 0;
            var store = new SlotStore(3, (_, count) => discarded += count);
            var a = NewDef("item.a", 2);
            var b = NewDef("item.b", 2);
            store.TryPlaceAt(new SlotId(1), a, 2);
            store.TryPlaceAt(new SlotId(2), b, 1);

            Assert.IsTrue(store.TryCompact());

            Assert.AreSame(a, ItemAt(store, 0));
            Assert.AreSame(b, ItemAt(store, 1));
            Assert.AreEqual(0, discarded, "整理没有溢出源，永不丢弃");
            Assert.IsFalse(store.TryCompact(), "已紧凑：无搬运");
        }

        [Test]
        public void 每格上限_按物品声明分格_先并入未满格()
        {
            var store = new SlotStore(3);
            var a = NewDef("item.a", 4);

            Assert.IsTrue(store.TryAdd(a, 6));

            Assert.AreEqual(4, CountAt(store, 0));
            Assert.AreEqual(2, CountAt(store, 1), "超过每格上限 → 溢出到下一格");

            Assert.IsTrue(store.TryAdd(a, 2));
            Assert.AreEqual(4, CountAt(store, 1), "再次分配先并入未满格");
            Assert.AreEqual(8, store.CountOf(a), "6 + 2");
        }

        [Test]
        public void 定向放入_成功与失败零改动()
        {
            var store = new SlotStore(3);
            var a = NewDef("item.a", 2);
            var b = NewDef("item.b", 2);

            Assert.IsTrue(store.TryPlaceAt(new SlotId(2), a, 2));
            Assert.IsTrue(store.Slots[0].IsEmpty && store.Slots[1].IsEmpty, "定向放入不占其它格");
            Assert.AreEqual(2, CountAt(store, 2));

            Assert.IsFalse(store.TryPlaceAt(new SlotId(2), a, 1), "超每格上限失败");
            Assert.IsFalse(store.TryPlaceAt(new SlotId(2), b, 1), "异类占用失败");
            Assert.IsFalse(store.TryPlaceAt(new SlotId(9), b, 1), "槽不存在失败");
            Assert.AreEqual(2, CountAt(store, 2), "失败零改动");
        }

        [Test]
        public void 提交门_布局变更中的写请求被拒()
        {
            var store = new SlotStore(2);
            var a = NewDef("item.a", 2);
            var b = NewDef("item.b", 2);
            var rejected = 0;
            store.LayoutChanged += _ =>
            {
                if (!store.TryAdd(b, 1))
                {
                    rejected++;
                }
            };

            store.TryAdd(a, 1);
            store.ApplyCapacity(3);

            Assert.AreEqual(1, rejected, "结构性变更（含事件派发）期间不得插入第二笔写");
            Assert.AreEqual(0, store.CountOf(b));
        }

        private static ItemDefinition ItemAt(SlotStore store, int index) => store.Slots[index].Item;

        private static int CountAt(SlotStore store, int index) => store.Slots[index].Count;

        private ItemDefinition NewDef(string id, int maxStack = 0)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            _owned.Add(def);

            if (maxStack > 0)
            {
                var profile = ScriptableObject.CreateInstance<InventoryFacetProfile>();
                _owned.Add(profile);
                profile.category = InventoryCategory.Resource;
                profile.maxStack = maxStack;
                GetFacetsList(def).Add(new InventoryFacet { profile = profile });
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
