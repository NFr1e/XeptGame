using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Container;
using XeptGame.Items;
using XeptGame.UI.Backpack;

namespace XeptGame.Tests
{
    /// <summary>
    /// 背包格视图模型测试（Backpack_UI_Design.md B8 第一条）：格视图映射（格号 → 内容）、
    /// <c>SlotChanged</c> 增量只动该格、<c>LayoutChanged</c> 增删格、<c>Stacks</c> 不参与格视图。
    /// 纯 C#，不需要场景与预制体。
    /// </summary>
    public class BackpackUIDataTests
    {
        private readonly List<Object> _owned = new();
        private readonly List<BackpackUIData> _views = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var view in _views)
            {
                view?.Dispose();
            }

            _views.Clear();

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
        public void 初始_格号到内容映射_空格也在()
        {
            var store = new SlotStore(3);
            var wood = NewDef("item.wood");
            store.TryAdd(wood, 4);

            var view = NewView(store);

            Assert.AreEqual(3, view.Capacity, "空格也要有格视图（网格天然表达'还剩几格'）");
            Assert.AreEqual(0, view.Snapshot[0].Cell);
            Assert.AreSame(wood, view.Snapshot[0].Item);
            Assert.AreEqual(4, view.Snapshot[0].Count);
            Assert.IsTrue(view.Snapshot[1].IsEmpty);
            Assert.IsTrue(view.Snapshot[2].IsEmpty);
        }

        [Test]
        public void 增量_只通知变化的格()
        {
            var store = new SlotStore(4);
            var view = NewView(store);
            var changed = new List<int>();
            var rebuilds = 0;
            view.CellChanged += changed.Add;
            view.RebuildRequested += () => rebuilds++;

            var wood = NewDef("item.wood");
            store.TryAdd(wood, 2);

            CollectionAssert.AreEqual(new[] { 0 }, changed, "只动第 0 格");
            Assert.AreEqual(0, rebuilds, "槽位轨不该触发全量重建");
            Assert.IsTrue(view.IsConsistentWithSource());
        }

        [Test]
        public void 增量_内容未变不通知()
        {
            var store = new SlotStore(2);
            var axe = NewDef("item.axe");
            store.TryAdd(axe, 1);
            var view = NewView(store);
            var changed = new List<int>();
            view.CellChanged += changed.Add;

            store.TryCompact(); // 已在最前、无搬运 → 不该有任何格通知

            CollectionAssert.IsEmpty(changed);
        }

        [Test]
        public void 结构轨_扩容追加空格并通知新格数()
        {
            var store = new SlotStore(2);
            var view = NewView(store);
            var layouts = new List<int>();
            view.LayoutChanged += layouts.Add;

            store.ApplyCapacity(5);

            CollectionAssert.AreEqual(new[] { 5 }, layouts);
            Assert.AreEqual(5, view.Capacity);
            Assert.IsTrue(view.Snapshot[4].IsEmpty);
            Assert.IsTrue(view.IsConsistentWithSource());
        }

        [Test]
        public void 结构轨_缩容裁剪尾部且内容跟着压缩()
        {
            var store = new SlotStore(6);
            var wood = NewDef("item.wood");
            var stone = NewDef("item.stone");
            store.TryPlaceAt(new SlotId(4), wood, 2);
            store.TryPlaceAt(new SlotId(5), stone, 1);
            var view = NewView(store);
            var layouts = new List<int>();
            view.LayoutChanged += layouts.Add;

            store.ApplyCapacity(3);

            CollectionAssert.AreEqual(new[] { 3 }, layouts);
            Assert.AreEqual(3, view.Capacity, "格视图裁到新格数");
            Assert.IsTrue(view.IsConsistentWithSource());
            Assert.AreSame(wood, view.Snapshot[0].Item, "内容压缩后跟着动（格号身份不变，内容会搬）");
            Assert.AreSame(stone, view.Snapshot[1].Item);
        }

        [Test]
        public void 同定义占两格_格视图不合并_而Stacks合并()
        {
            var store = new SlotStore(4);
            var wood = NewDef("item.wood");
            store.TryPlaceAt(new SlotId(0), wood, 2);
            store.TryPlaceAt(new SlotId(2), wood, 3);

            var view = NewView(store);

            Assert.AreSame(wood, view.Snapshot[0].Item, "第 0 格");
            Assert.AreSame(wood, view.Snapshot[2].Item, "第 2 格：两格各画各的");
            Assert.AreEqual(2, view.Snapshot[0].Count);
            Assert.AreEqual(3, view.Snapshot[2].Count);
            Assert.IsTrue(view.Snapshot[1].IsEmpty);

            Assert.AreEqual(1, store.Stacks.Count, "聚合面把同定义合成一行——它不参与格视图");
            Assert.AreEqual(5, store.Stacks[0].Count);
        }

        [Test]
        public void 实例行_快照带身份_换另一个同款也能分辨()
        {
            var store = new SlotStore(3);
            var bagDef = NewDef("item.backpack");
            var first = new XeptGame.Inv.ContainerInstance(1, bagDef, new XeptGame.Inv.Inventory(2));
            store.TryPlaceInstance(first);

            var view = NewView(store);

            Assert.IsTrue(view.Snapshot[0].HasInstance);
            Assert.AreEqual(1, view.Snapshot[0].InstanceId);
            Assert.AreEqual(1, view.Snapshot[0].Count, "实例行数量恒 1");
        }

        [Test]
        public void 一串操作后_快照始终与真源一致()
        {
            var store = new SlotStore(5);
            var view = NewView(store);
            var wood = NewDef("item.wood");
            var stone = NewDef("item.stone");

            store.TryAdd(wood, 3);
            Assert.IsTrue(view.IsConsistentWithSource());

            store.TryPlaceAt(new SlotId(3), stone, 2);
            Assert.IsTrue(view.IsConsistentWithSource());

            store.TryRemove(wood, 1);
            Assert.IsTrue(view.IsConsistentWithSource());

            store.ApplyCapacity(7);
            Assert.IsTrue(view.IsConsistentWithSource());

            store.TryCompact();
            Assert.IsTrue(view.IsConsistentWithSource());

            store.ApplyCapacity(2);
            Assert.IsTrue(view.IsConsistentWithSource());
        }

        [Test]
        public void 关闭后_不再通知也不抛()
        {
            var store = new SlotStore(2);
            var view = NewView(store);
            var changed = 0;
            view.CellChanged += _ => changed++;

            view.Dispose();
            var wood = NewDef("item.wood");
            store.TryAdd(wood, 1);

            Assert.AreEqual(0, changed);
        }

        private BackpackUIData NewView(SlotStore store)
        {
            var view = new BackpackUIData(store);
            _views.Add(view);
            return view;
        }

        private ItemDefinition NewDef(string id)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            _owned.Add(def);
            return def;
        }
    }
}
