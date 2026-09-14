using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Interaction;
using XeptGame.Items;
using XeptGame.World.Interactables;

namespace XeptGame.Tests
{
    /// <summary>
    /// 世界掉落物视图的"清单成员"规则（Interaction_Behaviour_Design.md §2 D5）：
    /// 成员 = 能力面（可持物/容器）× 携带事实（有包/无包）；可用与否不在这里（那是 <c>CanAct</c> 的事）。
    /// </summary>
    public sealed class WorldItemActionsTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void 无包_既不可持也非容器_没有任何动作()
            => Assert.AreEqual(string.Empty, KeysWithBag(false, false, false));

        [Test]
        public void 有包_既不可持也非容器_只有拾取()
            => Assert.AreEqual("interaction.pickup", KeysWithBag(true, false, false));

        [Test]
        public void 无包_可持物_只有装备()
            => Assert.AreEqual("interaction.equip", KeysWithBag(false, true, false));

        [Test]
        public void 有包_可持物_拾取与装备()
            => Assert.AreEqual("interaction.pickup,interaction.equip", KeysWithBag(true, true, false));

        [Test]
        public void 无包_容器_只有背上()
            => Assert.AreEqual("interaction.wear", KeysWithBag(false, false, true));

        [Test]
        public void 有包_容器_拾取与背上()
            => Assert.AreEqual("interaction.pickup,interaction.wear", KeysWithBag(true, false, true));

        [Test]
        public void 携带事实变化_触发重建并通知()
        {
            var item = MakeWorldItem(false, false, false);
            var facts = new FakeCarryFacts();
            item.SetCarryFacts(facts);

            var notified = 0;
            item.ActionsChanged += () => notified++;

            facts.HasBag = true;
            facts.Raise();

            Assert.AreEqual("interaction.pickup", Keys(item), "背上背包后应出现拾取。");
            Assert.AreEqual(1, notified, "成员变化应通知一次。");
        }

        [Test]
        public void 成员未变_不重复通知()
        {
            var item = MakeWorldItem(true, false, false);
            var facts = new FakeCarryFacts { HasBag = true };
            item.SetCarryFacts(facts);

            var notified = 0;
            item.ActionsChanged += () => notified++;

            facts.Raise();

            Assert.AreEqual(0, notified, "成员没变就不该发通知。");
        }

        // ---- 夹具 ----

        /// <summary>按 有包 × 可持物 × 容器 造一个世界掉落物视图，返回其动作键序列（逗号分隔，按名单顺序）。</summary>
        private string KeysWithBag(bool hasBag, bool holdable, bool carrier)
        {
            var item = MakeWorldItem(holdable, carrier, false);
            item.SetCarryFacts(new FakeCarryFacts { HasBag = hasBag });
            return Keys(item);
        }

        private WorldItem MakeWorldItem(bool holdable, bool carrier, bool resource)
        {
            var facets = new List<IItemFacet>();
            if (resource)
            {
                facets.Add(new ResourceFacet());
            }

            if (holdable)
            {
                facets.Add(new HoldableFacet());
            }

            if (carrier)
            {
                facets.Add(new ContainerFacet());
            }

            _go = new GameObject("WorldItemActionsTests");
            _go.SetActive(false); // 先失活：等序列化字段注入后再 Awake（Awake 会按定义建数据源）

            var item = _go.AddComponent<WorldItem>();
            typeof(WorldItem)
                .GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(item, MakeDefinition(facets));
            _go.SetActive(true);

            Assert.IsNotNull(item.Definition, "夹具注入定义失败。");
            return item;
        }

        private static ItemDefinition MakeDefinition(List<IItemFacet> facets)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            typeof(ItemDefinition)
                .GetField("facets", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(definition, facets);
            return definition;
        }

        private static string Keys(WorldItem item)
        {
            var text = string.Empty;
            for (int i = 0; i < item.Actions.Count; i++)
            {
                if (i > 0)
                {
                    text += ",";
                }

                text += item.Actions[i].PromptKey;
            }

            return text;
        }

        /// <summary>假携带事实（EditMode 覆盖"有包/无包"两态，不需要真会话）。</summary>
        private sealed class FakeCarryFacts : ICarryFacts
        {
            public bool HasBag { get; set; }

            public event System.Action Changed;

            public void Raise() => Changed?.Invoke();
        }
    }
}
