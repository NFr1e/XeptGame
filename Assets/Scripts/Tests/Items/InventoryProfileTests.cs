using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Core;
using XeptGame.Inv;
using XeptGame.Items;

namespace XeptGame.Tests
{
    /// <summary>
    /// 背包侧与扩容侧配置资产测试（SlotStore_Design.md §6）：
    /// InventoryProfile 基础格数（默认/坏数据回退/0 合法）、
    /// ContainerCapacityExpanderProfile 扩容格数（≥1 归一）、以及扩容面经 GetFacet 的取用形状。
    /// 假 Definition 的 Facet 用反射注入（EquipTestBase 先例）。
    /// </summary>
    public class InventoryProfileTests
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
        public void 背包配置_默认基础格数_与坏数据回退()
        {
            var profile = New<InventoryProfile>();

            Assert.AreEqual(XeptGameConsts.Inventory.DefaultCapacity, profile.baseSlots, "默认 = 常量（一个数字只放一处）");
            Assert.AreEqual(XeptGameConsts.Inventory.DefaultCapacity, profile.ResolvedBaseSlots);

            profile.baseSlots = -5;
            Assert.AreEqual(XeptGameConsts.Inventory.DefaultCapacity, profile.ResolvedBaseSlots, "负值 = 坏数据 → 回退默认");

            profile.baseSlots = 0;
            Assert.AreEqual(0, profile.ResolvedBaseSlots, "0 合法（没有背包），不回退");

            profile.baseSlots = 12;
            Assert.AreEqual(12, profile.ResolvedBaseSlots);
        }

        [Test]
        public void 扩容配置_扩容格数最小为1()
        {
            var profile = New<ContainerCapacityExpanderProfile>();

            Assert.AreEqual(1, profile.addedSlots);

            profile.addedSlots = 0;
            Assert.AreEqual(1, profile.ResolvedAddedSlots, "0 = 坏数据 → 至少加 1 格");

            profile.addedSlots = -3;
            Assert.AreEqual(1, profile.ResolvedAddedSlots);

            profile.addedSlots = 6;
            Assert.AreEqual(6, profile.ResolvedAddedSlots);
        }

        [Test]
        public void 定义挂扩容面_可经GetFacet取到配置()
        {
            var profile = New<ContainerCapacityExpanderProfile>();
            profile.addedSlots = 5;

            var def = New<ItemDefinition>();
            GetFacetsList(def).Add(new ContainerCapacityExpanderFacet { profile = profile });

            Assert.IsTrue(def.HasFacet<ContainerCapacityExpanderFacet>());
            Assert.AreSame(profile, def.GetFacet<ContainerCapacityExpanderFacet>().profile);
            Assert.AreEqual(5, def.GetFacet<ContainerCapacityExpanderFacet>().profile.ResolvedAddedSlots);
        }

        [Test]
        public void 定义未挂扩容面_查询为空且不抛()
        {
            var def = New<ItemDefinition>();

            Assert.IsFalse(def.HasFacet<ContainerCapacityExpanderFacet>());
            Assert.IsNull(def.GetFacet<ContainerCapacityExpanderFacet>());
            Assert.IsFalse(def.TryGetFacet<ContainerCapacityExpanderFacet>(out _));
        }

        private T New<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _owned.Add(asset);
            return asset;
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
