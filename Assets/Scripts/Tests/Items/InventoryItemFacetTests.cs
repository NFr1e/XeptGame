using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Items;

namespace XeptGame.Tests
{
    /// <summary>
    /// InventoryItemFacet / InventoryItemFacetProfile 数据契约测试（Inventory_Facet_Design.md §3/§7）：
    /// 默认值、读取兜底、未配置 profile 的判空、以及经 <c>ItemDefinition.GetFacet</c> 的取用形状。
    /// 假 Definition 用 CreateInstance + 反射注入 Facet 列表（EquipTestBase 先例：产品不提供运行时改 Facets 的口）。
    /// </summary>
    public class InventoryItemFacetTests
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

        [Test]
        public void Profile_默认值_未分类且每格上限为1()
        {
            var profile = New<InventoryItemFacetProfile>();

            Assert.AreEqual(InventoryCategory.None, profile.category, "默认必须未分类（落兜底页），不替作者决定");
            Assert.AreEqual(1, profile.maxStack, "默认 1 = 不可堆叠：须显式声明，不设魔法默认值");
            Assert.AreEqual(1, profile.ResolvedMaxStack);
        }

        [Test]
        public void ResolvedMaxStack_坏数据_兜底为1()
        {
            var profile = New<InventoryItemFacetProfile>();

            profile.maxStack = 0;
            Assert.AreEqual(1, profile.ResolvedMaxStack, "0 = 坏数据（旧资产/手改）→ 兜底 1");

            profile.maxStack = -7;
            Assert.AreEqual(1, profile.ResolvedMaxStack, "负数同义");
        }

        [Test]
        public void ResolvedMaxStack_合法值_原样返回()
        {
            var profile = New<InventoryItemFacetProfile>();
            profile.category = InventoryCategory.Resource;
            profile.maxStack = 20;

            Assert.AreEqual(InventoryCategory.Resource, profile.category);
            Assert.AreEqual(20, profile.ResolvedMaxStack);
        }

        [Test]
        public void Facet_未配置profile_条目存在但引用为空()
        {
            var facet = new InventoryItemFacet();

            Assert.IsNull(facet.profile, "未配置 = 能力已声明、配置未给（Object 引用判空可靠）");
        }

        [Test]
        public void Definition_挂InventoryItemFacet_可经GetFacet取到配置()
        {
            var profile = New<InventoryItemFacetProfile>();
            profile.category = InventoryCategory.Consumable;
            profile.maxStack = 5;

            var def = New<ItemDefinition>();
            GetFacetsList(def).Add(new InventoryItemFacet { profile = profile });

            Assert.IsTrue(def.HasFacet<InventoryItemFacet>());
            Assert.AreSame(profile, def.GetFacet<InventoryItemFacet>().profile);
            Assert.AreEqual(5, def.GetFacet<InventoryItemFacet>().profile.ResolvedMaxStack);
        }

        [Test]
        public void Definition_未挂InventoryItemFacet_查询为空且不抛()
        {
            var def = New<ItemDefinition>();

            Assert.IsFalse(def.HasFacet<InventoryItemFacet>());
            Assert.IsNull(def.GetFacet<InventoryItemFacet>());
            Assert.IsFalse(def.TryGetFacet<InventoryItemFacet>(out _));
        }
    }
}
