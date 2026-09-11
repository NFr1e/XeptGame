using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Container;
using XeptGame.Inv;
using XeptGame.Items;
using XeptGame.World;

namespace XeptGame.Tests
{
    /// <summary>
    /// 世界源测试（Item_Instance_Design.md §6）：
    /// 堆叠源 = 按定义扣减/回滚 + 占用标记 + 场景可用性；实例源 = <b>按定义写入一律拒绝</b>、
    /// 取放只能整体进行、聚合面仍按"实例各计 1"可见。
    /// </summary>
    public class WorldSourceTests
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
        public void 堆叠源_按定义扣减与回滚入口()
        {
            var wood = NewDef("item.wood");
            var source = new WorldStackSource(wood, 5, recordId: 42);
            var changes = new List<ContainerChangeArgs>();
            source.Changed += changes.Add;

            Assert.AreEqual(42, source.RecordId);
            Assert.AreSame(wood, source.DisplayDefinition);
            Assert.IsTrue(source.HasContent);
            Assert.AreEqual(5, source.Remaining);

            Assert.IsTrue(source.TryRemove(wood, 2));
            Assert.AreEqual(3, source.CountOf(wood));
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(5, changes[0].OldCount);
            Assert.AreEqual(3, changes[0].NewCount);

            Assert.IsTrue(source.TryAdd(wood, 2), "同定义回滚入口");
            Assert.AreEqual(5, source.CountOf(wood));
            Assert.IsFalse(source.TryRemove(NewDef("item.iron"), 1), "异定义：拒绝");
        }

        [Test]
        public void 堆叠源_占用标记_第二笔拒绝_释放后可再占用()
        {
            var source = new WorldStackSource(NewDef("item.wood"), 1);

            Assert.IsTrue(source.TryAcquire(1));
            Assert.IsTrue(source.IsBusy);
            Assert.IsFalse(source.TryAcquire(2), "同一时间只接纳一笔操作");
            Assert.IsTrue(source.TryAcquire(1), "同一操作重复标记 = 幂等成功");

            source.Release(2);
            Assert.IsTrue(source.IsBusy, "只有匹配的操作能释放");
            source.Release(1);
            Assert.IsFalse(source.IsBusy);
            Assert.IsTrue(source.TryAcquire(2));
        }

        [Test]
        public void 堆叠源_不可用_拒绝提交与占用()
        {
            var available = false;
            var source = new WorldStackSource(NewDef("item.wood"), 3, () => available);

            Assert.IsFalse(source.Available);
            Assert.IsFalse(source.TryRemove(source.DisplayDefinition, 1), "宿主不可用：不得提交");
            Assert.IsFalse(source.TryAcquire(1));

            available = true;
            Assert.IsTrue(source.TryRemove(source.DisplayDefinition, 1));
        }

        [Test]
        public void 实例源_按定义写入一律拒绝()
        {
            var bagDef = NewDef("item.bag");
            var source = new WorldInstanceSource(NewBag(bagDef, 4));

            Assert.IsFalse(source.TryAdd(bagDef, 1), "实例不可被'加进来'（身份不能凭空生成）");
            Assert.IsFalse(source.TryRemove(bagDef, 1), "实例不可按定义移除（身份不可按定义定位）");
        }

        [Test]
        public void 实例源_聚合面按实例计一()
        {
            var bagDef = NewDef("item.bag");
            var source = new WorldInstanceSource(NewBag(bagDef, 4));

            Assert.AreEqual(1, source.CountOf(bagDef));
            Assert.IsTrue(source.Contains(bagDef));
            Assert.AreEqual(1, source.Stacks.Count);
            Assert.AreEqual(1, source.Stacks[0].Count);
            Assert.AreSame(bagDef, source.DisplayDefinition);
            Assert.IsTrue(source.HasContent);
        }

        [Test]
        public void 实例源_整体取出与放回()
        {
            var bagDef = NewDef("item.bag");
            var bag = NewBag(bagDef, 4);
            var source = new WorldInstanceSource(bag, recordId: 8);
            var changes = new List<ContainerChangeArgs>();
            source.Changed += changes.Add;

            Assert.IsTrue(source.TryTakeCarrier(out var taken));
            Assert.AreSame(bag, taken, "取出的就是那一个实例（移动语义，不复制）");
            Assert.IsNull(source.Carrier);
            Assert.IsFalse(source.HasContent, "取出后本源为空 → 视图据此隐藏");
            Assert.IsFalse(source.Available, "空的实例源不可再提交");
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(1, changes[0].OldCount);
            Assert.AreEqual(0, changes[0].NewCount);

            Assert.IsFalse(source.TryTakeCarrier(out _), "已空：再次取出失败");

            Assert.IsTrue(source.TryReturnCarrier(bag), "回滚：整体放回");
            Assert.AreSame(bag, source.Carrier);
            Assert.IsTrue(source.HasContent);
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(0, changes[1].OldCount);
            Assert.AreEqual(1, changes[1].NewCount);
        }

        [Test]
        public void 实例源_放回_仅在空时()
        {
            var bagDef = NewDef("item.bag");
            var otherDef = NewDef("item.other");
            var source = new WorldInstanceSource(NewBag(bagDef, 2));

            Assert.IsFalse(source.TryReturnCarrier(NewBag(otherDef, 2)), "本源非空：拒绝");

            Assert.IsTrue(source.TryTakeCarrier(out _));
            Assert.IsFalse(source.TryReturnCarrier(null), "空实例：拒绝");
            Assert.IsTrue(source.TryReturnCarrier(NewBag(otherDef, 2)), "本源为空后放回任意实例（回滚语义按对象，不按定义）");
        }

        private ContainerInstance NewBag(ItemDefinition definition, int slots)
            => new(1, definition, new Inventory(slots));

        private ItemDefinition NewDef(string id)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            _owned.Add(def);
            return def;
        }
    }
}
