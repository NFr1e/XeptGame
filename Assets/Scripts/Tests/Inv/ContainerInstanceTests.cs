using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Container;
using XeptGame.Core;
using XeptGame.Equip;
using XeptGame.Game.Flow;
using XeptGame.Inv;
using XeptGame.Items;

namespace XeptGame.Tests
{
    /// <summary>
    /// 背包即物品（Item_Instance_Design.md §3 / DP5）：
    /// 容器面配置 → 实例工厂（签发 id + 装配容器格数）；背槽只收容器类物品；
    /// 会话的"当前背包"由背槽推导（无包 = null）；换包只换引用、实例内容永不迁移。
    /// 假 Definition 用反射注入 Facet（EquipTestBase 先例）。
    /// </summary>
    public class ContainerInstanceTests
    {
        private readonly List<Object> _owned = new();
        private readonly List<GameplaySessionContext> _sessions = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var session in _sessions)
            {
                session?.Dispose();
            }

            _sessions.Clear();

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
        public void 工厂_创建容器实例_id单调且格数取配置()
        {
            var factory = new ItemInstanceFactory(new InstanceIdAllocator());
            var def = NewDef("item.bag", baseSlots: 6);

            var first = factory.CreateContainer(def);
            var second = factory.CreateContainer(def);

            Assert.AreEqual(1, first.Id);
            Assert.AreEqual(2, second.Id, "id 会话内单调");
            Assert.AreEqual(6, first.Store.Capacity, "格数取 ContainerFacetProfile.ResolvedBaseSlots");
            Assert.AreSame(def, first.Definition);
            Assert.AreNotSame(first.Store, second.Store, "每个实例带自己的容器");
        }

        [Test]
        public void 工厂_未挂容器面_抛错()
        {
            var factory = new ItemInstanceFactory(new InstanceIdAllocator());
            var plain = NewDef("item.stone", isContainer: false);

            Assert.Throws<System.InvalidOperationException>(() => factory.CreateContainer(plain));
        }

        [Test]
        public void 工厂_容器面未配置资产_回落常量默认且_0格合法()
        {
            var factory = new ItemInstanceFactory(new InstanceIdAllocator());
            var noProfile = NewDef("item.bag.noprofile", isContainer: true, withProfile: false);
            var zeroSlots = NewDef("item.bag.zero", baseSlots: 0);

            Assert.AreEqual(XeptGameConsts.Inventory.DefaultCapacity, factory.CreateContainer(noProfile).Store.Capacity);
            Assert.AreEqual(0, factory.CreateContainer(zeroSlots).Store.Capacity, "容量下限 0（无格背包合法）");
        }

        [Test]
        public void 实例_空容器_抛错()
        {
            var def = NewDef("item.bag", baseSlots: 4);

            Assert.Throws<System.ArgumentNullException>(() => new ContainerInstance(1, def, null));
        }

        [Test]
        public void 背槽_只接纳容器类物品()
        {
            var body = new Equipment(new SlotBase[] { new HandSlot(), new BackSlot() });
            var backCell = new SlotId((int)BodySlotType.Back);
            var bagDef = NewDef("item.bag", baseSlots: 4);
            var holdableDef = NewDef("item.axe", isContainer: false);
            var bag = new ContainerInstance(1, bagDef, new Inventory(4));

            Assert.IsTrue(body.TryPlaceInstanceAt(backCell, bag));
            Assert.AreSame(bag, body.GetInstance(BodySlotType.Back));

            Assert.IsFalse(body.TryPlaceInstanceAt(backCell, new ItemInstance(2, holdableDef)), "非容器物：拒绝");
            Assert.AreSame(bag, body.GetInstance(BodySlotType.Back), "失败零改动");
        }

        [Test]
        public void 背槽_取出实例_身份保持()
        {
            var body = new Equipment(new SlotBase[] { new BackSlot() });
            var backCell = new SlotId((int)BodySlotType.Back);
            var bag = new ContainerInstance(7, NewDef("item.bag", baseSlots: 4), new Inventory(4));
            Assert.IsTrue(body.TryPlaceInstanceAt(backCell, bag));

            Assert.IsTrue(body.TryTakeInstanceAt(backCell, out var taken));

            Assert.AreSame(bag, taken, "取出的是同一个实例对象（移动语义）");
            Assert.IsNull(body.GetInstance(BodySlotType.Back));
        }

        [Test]
        public void 会话_无包时当前背包为空()
        {
            var session = NewSession();

            Assert.IsNull(session.Bag, "背槽空 ⇒ 无包");
            Assert.IsNull(session.Inventory, "Inventory 不预建、可为空（DP2）");
        }

        [Test]
        public void 会话_背上背包后Inventory指向其容器()
        {
            var session = NewSession();
            var bag = session.Instances.CreateContainer(NewDef("item.bag", baseSlots: 5));

            Assert.IsTrue(session.Equipment.TryPlaceInstanceAt(new SlotId((int)BodySlotType.Back), bag));

            Assert.AreSame(bag, session.Bag);
            Assert.AreSame(bag.Store, session.Inventory);
            Assert.AreEqual(5, session.Inventory.Capacity);
        }

        [Test]
        public void 会话_换包_只换引用_两个容器内容互不影响()
        {
            var session = NewSession();
            var backCell = new SlotId((int)BodySlotType.Back);
            var bagDef = NewDef("item.bag", baseSlots: 4);
            var stone = NewDef("item.stone", isContainer: false);

            var first = session.Instances.CreateContainer(bagDef);
            var second = session.Instances.CreateContainer(bagDef);
            Assert.IsTrue(first.Store.TryAdd(stone, 3), "第一个包里有东西");

            Assert.IsTrue(session.Equipment.TryPlaceInstanceAt(backCell, first));
            Assert.IsTrue(session.Equipment.TryTakeInstanceAt(backCell, out var taken));
            Assert.IsTrue(session.Equipment.TryPlaceInstanceAt(backCell, second));

            Assert.AreSame(first, taken);
            Assert.AreSame(second.Store, session.Inventory, "当前背包 = 新实例的容器");
            Assert.AreEqual(3, first.Store.CountOf(stone), "旧包内容永不迁移（换包 = 换实例）");
            Assert.AreEqual(0, second.Store.CountOf(stone));
        }

        private GameplaySessionContext NewSession()
        {
            var session = new GameplaySessionContext();
            _sessions.Add(session);
            return session;
        }

        private ItemDefinition NewDef(string id, bool isContainer = true, int baseSlots = 4, bool withProfile = true)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            _owned.Add(def);

            if (!isContainer)
            {
                return def;
            }

            ContainerFacetProfile profile = null;
            if (withProfile)
            {
                profile = ScriptableObject.CreateInstance<ContainerFacetProfile>();
                _owned.Add(profile);
                profile.baseSlots = baseSlots;
            }

            GetFacetsList(def).Add(new ContainerFacet { profile = profile });
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
