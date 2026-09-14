using NUnit.Framework;
using UnityEngine;
using XeptGame.Interaction;
using XeptGame.World.Interactables.Demo;

namespace XeptGame.Tests
{
    /// <summary>
    /// 火堆的"清单成员"与"可用性"两层分工（Interaction_Behaviour_Design.md §2 D5）：
    /// 成员随状态迁移（灭/燃）增删；燃料满只让添柴变灰，成员不动。
    /// </summary>
    public sealed class CampfireActionsTests
    {
        private GameObject _go;
        private DemoCampfireProp _fire;

        [SetUp]
        public void SetUp()
        {
            // 先失活再激活：让 Awake（初始状态 → 首次攒清单）按运行期时序发生（EditMode 下 AddComponent 不保证触发 Awake）
            _go = new GameObject("CampfireActionsTests");
            _go.SetActive(false);
            _fire = _go.AddComponent<DemoCampfireProp>();
            _go.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        // 注：本文件不测"初始直摆时的首建清单"——EditMode 测试不触发 Unity 生命周期（Awake/OnEnable），
        // 那一段由运行期 smoke 覆盖；这里全部经由公开端口方法驱动状态迁移来断言成员规则。

        [Test]
        public void 燃_熄灭与添柴()
        {
            Assert.IsTrue(_fire.TryIgnite());
            Assert.AreEqual("interaction.extinguish,interaction.addfuel", Keys());
        }

        [Test]
        public void 燃料满_添柴仍在清单但已不可用()
        {
            _fire.TryIgnite();
            _fire.TryAddFuel();
            _fire.TryAddFuel(); // 燃料 1 → 3（上限）

            Assert.AreEqual(DemoCampfireProp.MaxFuel, _fire.Fuel);
            Assert.AreEqual("interaction.extinguish,interaction.addfuel", Keys(), "成员不因燃料满而改变。");
            Assert.IsFalse(Find("interaction.addfuel").CanInteract(Context()), "燃料满时添柴应为灰态。");
        }

        [Test]
        public void 熄灭_回到只有点燃()
        {
            _fire.TryIgnite();
            Assert.IsTrue(_fire.TryExtinguish());
            Assert.AreEqual("interaction.ignite", Keys());
        }

        [Test]
        public void 对象名走本地化键_且有直显回退()
        {
            Assert.AreEqual(DemoCampfireProp.NameKey, _fire.DisplayNameKey);
            Assert.AreEqual("campfire.name", DemoCampfireProp.NameKey);
        }

        private string Keys()
        {
            var text = string.Empty;
            for (int i = 0; i < _fire.Actions.Count; i++)
            {
                if (i > 0)
                {
                    text += ",";
                }

                text += _fire.Actions[i].PromptKey;
            }

            return text;
        }

        private IInteractionAction Find(string promptKey)
        {
            for (int i = 0; i < _fire.Actions.Count; i++)
            {
                if (_fire.Actions[i].PromptKey == promptKey)
                {
                    return _fire.Actions[i];
                }
            }

            Assert.Fail("清单里没有 " + promptKey);
            return null;
        }

        private InteractionContext Context() => new InteractionContext(_go.transform);
    }
}
