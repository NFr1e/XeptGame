using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Items;
using XeptGame.World;
using XeptGame.World.Interactables;

namespace XeptGame.Tests
{
    /// <summary>
    /// 视图根语义（W8，WorldItem_Design.md §7）：视图是**显式字段**，不是"组件所在的物体"。
    /// 覆盖作者反馈的嵌套坑：组件挂在父级时，内容取空**不能**连带隐藏父级与兄弟物体。
    /// 注：EditMode 不触发 Unity 生命周期（Awake/OnEnable），故这里用 <c>Initialize</c> 注入数据面后直接驱动。
    /// </summary>
    public sealed class WorldItemViewTests
    {
        private readonly System.Collections.Generic.List<Object> _owned = new();

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
        public void 视图根留空_回退本物体()
        {
            var (parent, item, _) = MakeHierarchy();

            Assert.AreSame(parent, item.View, "未配置视图根 → 回退本物体（既有摆件行为不变）");
        }

        [Test]
        public void 内容取空_只隐藏视图_父级与兄弟不受影响()
        {
            var (parent, item, view) = MakeHierarchy();
            var sibling = new GameObject("Sibling");
            sibling.transform.SetParent(parent.transform, false);
            SetView(item, view);

            item.Initialize(new WorldStackSource(MakeDefinition(), 0)); // 内容为空
            item.RefreshView();

            Assert.IsFalse(view.activeSelf, "视图根被隐藏");
            Assert.IsTrue(parent.activeSelf, "父级（组件所在物体）**不受影响**");
            Assert.IsTrue(sibling.activeSelf, "兄弟物体**不受影响**");
        }

        [Test]
        public void 内容还在_不隐藏()
        {
            var (_, item, view) = MakeHierarchy();
            SetView(item, view);

            item.Initialize(new WorldStackSource(MakeDefinition(), 1)); // 还有内容
            item.RefreshView();

            Assert.IsTrue(view.activeSelf, "还有内容就不该隐藏");
        }

        [Test]
        public void 视图失活_数据面不可用且不可拾取()
        {
            var (parent, item, view) = MakeHierarchy();
            SetView(item, view);

            // 与运行期一致：数据面的可用性判据 = 本组件的视图判据（Awake 里也是这么接的）
            item.Initialize(new WorldStackSource(MakeDefinition(), 1, () => item.IsViewAvailable, () => { }));

            Assert.IsTrue(item.IsViewAvailable, "视图激活时可用");
            Assert.IsTrue(item.CanPickup, "视图激活时可拾取");

            view.SetActive(false);

            Assert.IsFalse(item.IsViewAvailable, "视图失活 → 数据面不可用");
            Assert.IsFalse(item.CanPickup, "视图失活 → 不可拾取");
            Assert.IsTrue(parent.activeSelf, "父级仍激活（只是这件东西不在了）");
        }

        // ---- 夹具 ----

        /// <summary>父物体挂 WorldItem，子物体当视图（带一个碰撞体），返回三者。</summary>
        private (GameObject Parent, WorldItem Item, GameObject View) MakeHierarchy()
        {
            var parent = new GameObject("ViewTestsHost");
            _owned.Add(parent);

            var view = new GameObject("ViewRoot");
            view.transform.SetParent(parent.transform, false);
            view.AddComponent<BoxCollider>();

            var item = parent.AddComponent<WorldItem>();
            return (parent, item, view);
        }

        private static void SetView(WorldItem item, GameObject view)
        {
            typeof(WorldItem)
                .GetField("view", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(item, view);
        }

        private ItemDefinition MakeDefinition()
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            _owned.Add(definition);
            return definition;
        }
    }
}
