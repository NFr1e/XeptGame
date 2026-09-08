using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Equip;
using XeptGame.Inv;
using XeptGame.Items;

namespace XeptGame.Tests
{
    /// <summary>
    /// 装备域测试基座（Equip_FPV_Implement.md T1–T3）：
    /// 假 Definition（CreateInstance + 反射注入 Facet 列表——产品不提供运行时改 Facets 的口，
    /// 测试专用装配，见 ItemDefinition 序列化纪律）、容器工厂与资源回收。
    /// </summary>
    public abstract class EquipTestBase
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

        /// <summary>创建假物品定义：holdable = true 挂 HoldableFacet（可持），否则挂 ResourceFacet（不可持）。</summary>
        protected ItemDefinition NewDef(string id, bool holdable)
        {
            // Inventory/Equipment 按资产引用标识物品（ReferenceEquals），id 仅命名参考，无需回写序列化字段
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            _owned.Add(def);

            GetFacetsList(def).Add(holdable ? (IItemFacet)new HoldableFacet() : new ResourceFacet());
            return def;
        }

        protected static Equipment NewBody() => new Equipment(new ISlot[] { new HandSlot() });

        protected static Inventory NewBag() => new Inventory();

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
