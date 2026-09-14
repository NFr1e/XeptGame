using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using XeptGame.Interaction;

namespace XeptGame.Tests
{
    /// <summary>
    /// 交互行为总名单的"双向覆盖"校验（Interaction_Behaviour_Design.md §5）：
    /// 名单与实现必须互相覆盖——不多（不会有登记错的对象）、不少（不会有写了却没人能用的行为类）。
    /// </summary>
    public sealed class InteractionBehaviourCatalogTests
    {
        [Test]
        public void 名单非空()
        {
            Assert.Greater(InteractionBehaviourCatalog.All.Count, 0, "总名单不能为空。");
        }

        [Test]
        public void 名单里每个行为的键非空且带域前缀()
        {
            for (int i = 0; i < InteractionBehaviourCatalog.All.Count; i++)
            {
                var behaviour = InteractionBehaviourCatalog.All[i];
                Assert.IsFalse(string.IsNullOrWhiteSpace(behaviour.PromptKey),
                    behaviour.GetType().Name + " 的文案键为空。");
                Assert.IsTrue(behaviour.PromptKey.StartsWith("interaction."),
                    behaviour.GetType().Name + " 的文案键缺少 interaction. 前缀：" + behaviour.PromptKey);
                Assert.IsFalse(behaviour.PromptKey.Contains(" "),
                    behaviour.GetType().Name + " 的文案键含空白：" + behaviour.PromptKey);
            }
        }

        [Test]
        public void 文案键唯一()
        {
            var seen = new Dictionary<string, string>();
            for (int i = 0; i < InteractionBehaviourCatalog.All.Count; i++)
            {
                var behaviour = InteractionBehaviourCatalog.All[i];
                Assert.IsFalse(seen.ContainsKey(behaviour.PromptKey),
                    "文案键重复：" + behaviour.PromptKey + "（" + seen.GetValueOrDefault(behaviour.PromptKey)
                    + " 与 " + behaviour.GetType().Name + "）");
                seen[behaviour.PromptKey] = behaviour.GetType().Name;
            }
        }

        [Test]
        public void 每个行为实现类都被登记()
        {
            var registered = new HashSet<System.Type>();
            for (int i = 0; i < InteractionBehaviourCatalog.All.Count; i++)
            {
                registered.Add(InteractionBehaviourCatalog.All[i].GetType());
            }

            var behaviourInterface = typeof(IInteractionBehaviour);
            var types = behaviourInterface.Assembly.GetTypes();
            var missing = new List<string>();
            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type.IsInterface || type.IsAbstract || !behaviourInterface.IsAssignableFrom(type))
                {
                    continue;
                }

                if (!registered.Contains(type))
                {
                    missing.Add(type.Name);
                }
            }

            Assert.IsEmpty(missing,
                "以下行为类没有登记进 InteractionBehaviourCatalog：" + string.Join("、", missing));
        }

        [Test]
        public void 空目标与空携带事实_任何行为都不适配()
        {
            for (int i = 0; i < InteractionBehaviourCatalog.All.Count; i++)
            {
                Assert.IsFalse(InteractionBehaviourCatalog.All[i].CanBuildOn(null, null),
                    InteractionBehaviourCatalog.All[i].GetType().Name + " 在空目标上仍然适配。");
            }
        }
    }
}
