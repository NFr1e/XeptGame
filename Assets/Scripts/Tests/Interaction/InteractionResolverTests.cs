using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Interaction;

namespace XeptGame.Tests
{
    /// <summary>
    /// <see cref="InteractionResolver"/>（静态解析管线）纯逻辑部分测试（契约模型 v2）：
    /// 无效射线早退、配置防御（null profile）、交互者防御（有效射线 + null 交互者）、
    /// 零射程未命中路径（物理查询零距离直接返回，无命中即空结果）。
    /// 命中 → 角度过滤 → 最近解析 → CanSelect 门控的语义依赖物理世界（SphereCast/Raycast 命中），
    /// EditMode 单测成本高且需场景物理——保持由人工集成验证覆盖（Interaction_EntryStage_Design.md §4.5/T4）；
    /// 本测试锁定无物理分支，防回归不依赖场景。
    /// </summary>
    public class InteractionResolverTests
    {
        private readonly List<UnityEngine.Object> _owned = new();
        private GameObject _interactor;

        [SetUp]
        public void SetUp()
        {
            _interactor = new GameObject("InteractionResolverTests_Interactor");
        }

        [TearDown]
        public void TearDown()
        {
            if (_interactor != null)
            {
                UnityEngine.Object.DestroyImmediate(_interactor);
                _interactor = null;
            }

            foreach (var obj in _owned)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }

            _owned.Clear();
        }

        private InteractionProfile NewProfile()
        {
            var profile = ScriptableObject.CreateInstance<InteractionProfile>();
            _owned.Add(profile);
            return profile;
        }

        [Test]
        public void 无效射线_直接返回空结果()
        {
            // default(InteractionProbeRay)：Direction 为零向量 → IsValid = false
            var result = InteractionResolver.Resolve(default, NewProfile(), _interactor.transform);

            Assert.IsNull(result.Target);
            Assert.AreEqual(0f, result.Distance);
        }

        [Test]
        public void null配置_抛ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => InteractionResolver.Resolve(
                new InteractionProbeRay(Vector3.zero, Vector3.forward), null, _interactor.transform));
        }

        [Test]
        public void 有效射线_null交互者_抛ArgumentNullException()
        {
            var profile = NewProfile();
            profile.maxRange = 0f;

            Assert.Throws<ArgumentNullException>(() => InteractionResolver.Resolve(
                new InteractionProbeRay(Vector3.zero, Vector3.forward), profile, null));
        }

        [Test]
        public void 零射程_未命中_返回空目标()
        {
            var profile = NewProfile();
            profile.maxRange = 0f;
            profile.probeRadius = 0f; // 纯射线路径

            var result = InteractionResolver.Resolve(
                new InteractionProbeRay(Vector3.zero, Vector3.forward), profile, _interactor.transform);

            Assert.IsNull(result.Target);
            Assert.AreEqual(0f, result.Distance);
        }
    }
}
