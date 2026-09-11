using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Items;

namespace XeptGame.Tests
{
    /// <summary>
    /// 实例身份层测试（Item_Instance_Design.md §2.1 / DP7）：
    /// 签发单调且不复用、0 为"无实例"哨兵、<c>Next</c> 只读不消耗、实例保留定义引用与身份。
    /// </summary>
    public class ItemInstanceTests
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
        public void 实例_id为0或负_抛错()
        {
            var def = NewDef();

            // 口径与机制层一致：Guard.True → InvalidOperationException（"0 预留为无实例哨兵"）
            Assert.Throws<System.InvalidOperationException>(() => new ItemInstance(0, def), "0 预留为无实例哨兵");
            Assert.Throws<System.InvalidOperationException>(() => new ItemInstance(-1, def));
        }

        [Test]
        public void 实例_空定义_抛错()
        {
            Assert.Throws<System.ArgumentNullException>(() => new ItemInstance(1, null));
        }

        [Test]
        public void 实例_保留身份与定义引用()
        {
            var def = NewDef();
            var instance = new ItemInstance(42, def);

            Assert.AreEqual(42, instance.Id);
            Assert.AreSame(def, instance.Definition);
        }

        [Test]
        public void 分配器_默认首发1_小于1回落1()
        {
            Assert.AreEqual(1, new InstanceIdAllocator().Next);
            Assert.AreEqual(1, new InstanceIdAllocator(0).Next);
            Assert.AreEqual(1, new InstanceIdAllocator(-5).Next);
            Assert.AreEqual(100, new InstanceIdAllocator(100).Next, "存档续发：以已用最大值 + 1 起发");
        }

        [Test]
        public void 分配器_单调递增且不复用()
        {
            var allocator = new InstanceIdAllocator();
            var issued = new List<long>();

            for (int i = 0; i < 5; i++)
            {
                issued.Add(allocator.Allocate());
            }

            CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4, 5 }, issued);
            Assert.AreEqual(6, allocator.Next);
            Assert.AreEqual(6, allocator.Allocate(), "继续单调，不回退");
        }

        [Test]
        public void 分配器_Next只读不消耗()
        {
            var allocator = new InstanceIdAllocator(7);

            Assert.AreEqual(7, allocator.Next);
            Assert.AreEqual(7, allocator.Next, "重复读取不推进");
            Assert.AreEqual(7, allocator.Allocate());
            Assert.AreEqual(8, allocator.Next);
        }

        private ItemDefinition NewDef()
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            _owned.Add(def);
            return def;
        }
    }
}
