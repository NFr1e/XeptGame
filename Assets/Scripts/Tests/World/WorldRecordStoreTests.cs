using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Items;
using XeptGame.World;

namespace XeptGame.Tests
{
    /// <summary>
    /// 世界记录表测试（Item_Instance_Design.md §5.1；不变量 I3/I8）：
    /// id 与实例同空间单调、两种记录（堆叠 / 实例）的数量纪律、分组计数与隔离、
    /// 分组条目上限拒绝 + 告警、增删改三条事件、只有记录层能改字段。
    /// </summary>
    public class WorldRecordStoreTests
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
        public void 新增_分配单调id_可查回()
        {
            var store = NewStore();
            var wood = NewDef("item.wood");

            var first = store.Add(wood, 3, Vector3.zero, "level.a");
            var second = store.Add(wood, 1, Vector3.one, "level.a");

            Assert.AreEqual(1, first.Id);
            Assert.AreEqual(2, second.Id, "与实例 id 共用签发器（DP3）：单调");
            Assert.AreEqual(2, store.Count);
            Assert.IsTrue(store.TryGet(first.Id, out var found));
            Assert.AreSame(first, found);
        }

        [Test]
        public void 实例记录_数量必须为一()
        {
            var store = NewStore();
            var bagDef = NewDef("item.bag");
            var instance = new ItemInstance(99, bagDef);

            Assert.Throws<System.ArgumentException>(() => store.Add(bagDef, 2, Vector3.zero, "level.a", instance));
            Assert.AreEqual(0, store.Count);
        }

        [Test]
        public void 分组上限_超限拒绝写入并告警()
        {
            var warnings = new List<string>();
            var store = new WorldRecordStore(new InstanceIdAllocator(), maxRecordsPerGroup: 2, warnings.Add);
            var wood = NewDef("item.wood");

            Assert.IsNotNull(store.Add(wood, 1, Vector3.zero, "level.a"));
            Assert.IsNotNull(store.Add(wood, 1, Vector3.zero, "level.a"));
            Assert.IsNull(store.Add(wood, 1, Vector3.zero, "level.a"), "超限：拒绝写入");

            Assert.AreEqual(2, store.CountIn("level.a"), "记录表必须有界（I8）");
            Assert.AreEqual(1, warnings.Count);
            Assert.IsNotNull(store.Add(wood, 1, Vector3.zero, "level.b"), "分组各自计数，别的组不受影响");
        }

        [Test]
        public void 删除_发Removed_且不再可查()
        {
            var store = NewStore();
            var wood = NewDef("item.wood");
            var record = store.Add(wood, 1, Vector3.zero, "level.a");
            var events = new List<WorldRecordChangeArgs>();
            store.Changed += events.Add;

            Assert.IsTrue(store.TryRemove(record.Id));
            Assert.IsFalse(store.TryRemove(record.Id), "重复删除 = false");

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(WorldRecordChangeKind.Removed, events[0].Kind);
            Assert.AreSame(record, events[0].Record, "删除事件仍带记录对象（视图按 id 回收）");
            Assert.IsFalse(store.TryGet(record.Id, out _));
            Assert.AreEqual(0, store.CountIn("level.a"));
        }

        [Test]
        public void 改数量_实例记录拒绝_堆叠记录发Updated()
        {
            var store = NewStore();
            var wood = NewDef("item.wood");
            var bagDef = NewDef("item.bag");
            var stack = store.Add(wood, 2, Vector3.zero, "level.a");
            var instanceRecord = store.Add(bagDef, 1, Vector3.zero, "level.a", new ItemInstance(7, bagDef));
            var kinds = new List<WorldRecordChangeKind>();
            store.Changed += args => kinds.Add(args.Kind);

            Assert.IsFalse(store.TrySetCount(instanceRecord.Id, 3), "实例记录数量恒 1（I2）：不可改");
            Assert.IsTrue(store.TrySetCount(stack.Id, 5));
            Assert.AreEqual(5, stack.Count);
            CollectionAssert.AreEqual(new[] { WorldRecordChangeKind.Updated }, kinds);
        }

        [Test]
        public void 改位置_发Updated且位置落地()
        {
            var store = NewStore();
            var record = store.Add(NewDef("item.wood"), 1, Vector3.zero, "level.a");
            var updated = 0;
            store.Changed += args =>
            {
                if (args.Kind == WorldRecordChangeKind.Updated)
                {
                    updated++;
                }
            };

            Assert.IsTrue(store.TrySetPosition(record.Id, new Vector3(1f, 2f, 3f)));
            Assert.IsFalse(store.TrySetPosition(record.Id + 100, Vector3.zero), "不存在的记录 = false");
            Assert.AreEqual(new Vector3(1f, 2f, 3f), record.Position);
            Assert.AreEqual(1, updated);
        }

        [Test]
        public void 分组隔离_计数与快照各归其组()
        {
            var store = NewStore();
            var wood = NewDef("item.wood");
            store.Add(wood, 1, Vector3.zero, "level.a");
            store.Add(wood, 1, Vector3.zero, "level.a");
            var b = store.Add(wood, 1, Vector3.zero, "level.b");

            Assert.AreEqual(2, store.CountIn("level.a"));
            Assert.AreEqual(1, store.CountIn("level.b"));
            Assert.AreEqual(0, store.CountIn("level.c"));
            var snapshot = store.RecordsIn("level.b");
            Assert.AreEqual(1, snapshot.Count);
            Assert.AreSame(b, snapshot[0]);
            Assert.AreEqual(0, store.RecordsIn("level.c").Count);
        }

        [Test]
        public void 分组留空_归入默认组()
        {
            var store = NewStore();
            store.Add(NewDef("item.wood"), 1, Vector3.zero, null);

            Assert.AreEqual(1, store.CountIn(WorldRecordStore.DefaultGroup));
        }

        private WorldRecordStore NewStore() => new(new InstanceIdAllocator());

        private ItemDefinition NewDef(string id)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            _owned.Add(def);
            return def;
        }
    }
}
