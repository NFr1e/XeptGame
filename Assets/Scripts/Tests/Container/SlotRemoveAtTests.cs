using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Container;
using XeptGame.Inv;
using XeptGame.Items;

namespace XeptGame.Tests
{
    /// <summary>
    /// 格寻址移除 <see cref="SlotContainer.TryRemoveAt"/> 的测试（Backpack_UI_Design.md §6.5 缺陷修复）：
    /// 存在的理由是"按定义移除从槽序最靠前的同物格开始扣"，界面上表现为**选中后格却扣了前格**。
    /// 这组测试把"只动那一格"钉死，作为该缺陷的回归网。
    /// </summary>
    public class SlotRemoveAtTests
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
        public void 定向移除_只扣指定格_同物前格不动()
        {
            var store = new SlotStore(4);
            var stone = NewDef("item.stone");
            store.TryPlaceAt(new SlotId(0), stone, 12);
            store.TryPlaceAt(new SlotId(3), stone, 5);

            Assert.IsTrue(store.TryRemoveAt(new SlotId(3), 1));

            Assert.AreEqual(12, store.Slots[0].Count, "前格不动——这正是本次修复要钉住的行为");
            Assert.AreEqual(4, store.Slots[3].Count);
            Assert.AreEqual(16, store.CountOf(stone));
        }

        [Test]
        public void 对照_按定义移除_扣的是最靠前的同物格()
        {
            var store = new SlotStore(4);
            var stone = NewDef("item.stone");
            store.TryPlaceAt(new SlotId(0), stone, 12);
            store.TryPlaceAt(new SlotId(3), stone, 5);

            Assert.IsTrue(store.TryRemove(stone, 1));

            Assert.AreEqual(11, store.Slots[0].Count, "按定义移除从槽序最靠前的同物格开始扣（这是既有语义，不是缺陷）");
            Assert.AreEqual(5, store.Slots[3].Count);
        }

        [Test]
        public void 定向移除_数量不足_拒绝且零改动()
        {
            var store = new SlotStore(2);
            var stone = NewDef("item.stone");
            store.TryPlaceAt(new SlotId(1), stone, 3);

            Assert.IsFalse(store.TryRemoveAt(new SlotId(1), 4));

            Assert.AreEqual(3, store.Slots[1].Count, "原子：不足则整笔失败");
        }

        [Test]
        public void 定向移除_实例行_拒绝且零改动()
        {
            var store = new SlotStore(2);
            var bagDef = NewDef("item.backpack");
            var instance = new ContainerInstance(1, bagDef, new Inventory(2));
            store.TryPlaceInstanceAt(new SlotId(0), instance);

            Assert.IsFalse(store.TryRemoveAt(new SlotId(0), 1), "实例行按数量扣减无意义（不变量 I2）");
            Assert.AreSame(instance, store.Slots[0].Instance, "实例原样留着");
        }

        [Test]
        public void 定向移除_空格与不存在的格_均拒绝()
        {
            var store = new SlotStore(2);
            var stone = NewDef("item.stone");
            store.TryPlaceAt(new SlotId(0), stone, 1);

            Assert.IsFalse(store.TryRemoveAt(new SlotId(1), 1), "空格");
            Assert.IsFalse(store.TryRemoveAt(new SlotId(9), 1), "格不存在");
            Assert.AreEqual(1, store.Slots[0].Count);
        }

        [Test]
        public void 定向移除_扣空即清格_且聚合轨报告前后总数()
        {
            var store = new SlotStore(2);
            var stone = NewDef("item.stone");
            store.TryPlaceAt(new SlotId(0), stone, 2);
            store.TryPlaceAt(new SlotId(1), stone, 3);

            var changes = new List<ContainerChangeArgs>();
            store.Changed += changes.Add;

            Assert.IsTrue(store.TryRemoveAt(new SlotId(0), 2));

            Assert.IsTrue(store.Slots[0].IsEmpty, "扣空即清格");
            Assert.AreEqual(1, changes.Count);
            Assert.AreSame(stone, changes[0].Item);
            Assert.AreEqual(5, changes[0].OldCount, "聚合口径：跨格总数");
            Assert.AreEqual(3, changes[0].NewCount);
        }

        [Test]
        public void 定向移除_扣到零的格_可再定向放回()
        {
            var store = new SlotStore(2);
            var stone = NewDef("item.stone");
            store.TryPlaceAt(new SlotId(0), stone, 12);
            store.TryPlaceAt(new SlotId(1), stone, 1);

            Assert.IsTrue(store.TryRemoveAt(new SlotId(1), 1), "丢后格");
            Assert.IsTrue(store.TryPlaceAt(new SlotId(1), stone, 1), "落地失败要能放回**原格**");

            Assert.AreEqual(12, store.Slots[0].Count, "前格始终不动");
            Assert.AreEqual(1, store.Slots[1].Count);
        }

        private ItemDefinition NewDef(string id)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            _owned.Add(def);
            return def;
        }
    }
}
