using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Interaction;

namespace XeptGame.Tests
{
    /// <summary>
    /// <see cref="InteractionSelection"/>（选中状态机，纯逻辑）语义测试（Interaction_Design.md §2.2）：
    /// 单一门控者的目标变化检测（同目标 no-op）、先退场后登场顺序、事件负载（To/From 镜像
    /// Fsm.StateChangeArgs）、Clear 补退场、销毁防护（Unity 假 null）。
    /// 假目标为 MonoBehaviour 实现 ISelectable——契约约定实现方为场景对象，<c>IsAlive</c> 依赖
    /// UnityEngine.Object 判空，纯 C# 假对象无法触发推送路径（见 InteractionSelection.IsAlive）。
    /// </summary>
    public class InteractionSelectionTests
    {
        private GameObject _root;
        private List<string> _log;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("InteractionSelectionTests_Root");
            _log = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        private FakeSelectable NewFake(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var fake = go.AddComponent<FakeSelectable>();
            fake.Bind(_log);
            return fake;
        }

        private static InteractionContext Ctx(FakeSelectable fake)
            => new InteractionContext(fake.transform);

        [Test]
        public void 首次应用_登场并推送_null到A()
        {
            var selection = new InteractionSelection();
            var a = NewFake("A");
            var changes = new List<SelectionChangeArgs>();
            selection.Changed += changes.Add;

            selection.Apply(a, Ctx(a));

            Assert.AreEqual(new List<string> { "A.OnSelected" }, _log);
            Assert.AreEqual(1, changes.Count);
            Assert.AreSame(a, changes[0].To);
            Assert.IsNull(changes[0].From);
            Assert.IsTrue(changes[0].HasTarget);
            Assert.IsTrue(changes[0].Changed);
            Assert.IsTrue(a.IsSelected);
            Assert.AreSame(a, selection.Previous);
        }

        [Test]
        public void 相同目标_重复应用为noop()
        {
            var selection = new InteractionSelection();
            var a = NewFake("A");
            var events = 0;
            selection.Changed += _ => events++;

            selection.Apply(a, Ctx(a));
            selection.Apply(a, Ctx(a));
            selection.Apply(a, Ctx(a));

            Assert.AreEqual(new List<string> { "A.OnSelected" }, _log, "重复应用不重复登场");
            Assert.AreEqual(1, events, "重复应用不重复推送");
        }

        [Test]
        public void 目标切换_先退场后登场_A到B()
        {
            var selection = new InteractionSelection();
            var a = NewFake("A");
            var b = NewFake("B");
            var changes = new List<SelectionChangeArgs>();
            selection.Changed += changes.Add;

            selection.Apply(a, Ctx(a));
            selection.Apply(b, Ctx(b));

            Assert.AreEqual(
                new List<string> { "A.OnSelected", "A.OnDeselected", "B.OnSelected" },
                _log,
                "退场（旧）必须先于登场（新），避免重叠高亮");
            Assert.AreEqual(2, changes.Count);
            Assert.AreSame(b, changes[1].To);
            Assert.AreSame(a, changes[1].From);
            Assert.IsFalse(a.IsSelected);
            Assert.IsTrue(b.IsSelected);
            Assert.AreSame(b, selection.Previous);
        }

        [Test]
        public void 目标消失_退场并推送_A到null()
        {
            var selection = new InteractionSelection();
            var a = NewFake("A");
            var changes = new List<SelectionChangeArgs>();
            selection.Changed += changes.Add;

            selection.Apply(a, Ctx(a));
            selection.Apply(null, default); // default(InteractionContext)：无目标时不读取

            Assert.AreEqual(new List<string> { "A.OnSelected", "A.OnDeselected" }, _log);
            Assert.AreEqual(2, changes.Count);
            Assert.IsNull(changes[1].To);
            Assert.AreSame(a, changes[1].From);
            Assert.IsFalse(changes[1].HasTarget);
            Assert.IsFalse(a.IsSelected);

            // 连续无目标为 no-op
            var before = _log.Count;
            selection.Apply(null, default);
            Assert.AreEqual(before, _log.Count);
            Assert.AreEqual(2, changes.Count);
        }

        [Test]
        public void Clear_补退场并推送_目标归空()
        {
            var selection = new InteractionSelection();
            var a = NewFake("A");
            var changes = new List<SelectionChangeArgs>();
            selection.Changed += changes.Add;

            selection.Apply(a, Ctx(a));
            _log.Clear();
            selection.Clear();

            Assert.AreEqual(new List<string> { "A.OnDeselected" }, _log);
            Assert.AreEqual(2, changes.Count);
            Assert.IsNull(changes[1].To);
            Assert.AreSame(a, changes[1].From);
            Assert.IsNull(selection.Previous);

            // 无目标时 Clear 为 no-op
            var before = _log.Count;
            selection.Clear();
            Assert.AreEqual(before, _log.Count);
            Assert.AreEqual(2, changes.Count);
        }

        [Test]
        public void 目标销毁_清除被宣布_但退场回调被跳过()
        {
            var selection = new InteractionSelection();
            var a = NewFake("A");
            selection.Apply(a, Ctx(a)); // previous = A
            var changes = new List<SelectionChangeArgs>();
            selection.Changed += changes.Add;
            _log.Clear();

            Object.DestroyImmediate(a.gameObject);

            // 契约要点：接口类型（ISelectable）的 == 是引用相等，**不感知** Unity 假 null
            // （Resolver.IsAlive 注释同源坑）——销毁的旧目标与 null 比较不等，故 Apply(null)
            // 仍会发布"清除"（To=null）；但 IsAlive 防御保证不会在已销毁对象上调用 OnDeselected。
            // 这正是"目标死亡 → 消费方收到清除通知"的正确传播路径（若 no-op，提示将残留）。
            selection.Apply(null, default);

            Assert.IsEmpty(_log, "已销毁对象上的退场回调必须被跳过");
            Assert.AreEqual(1, changes.Count, "目标死亡必须以 To=null 的清除事件宣布");
            Assert.IsNull(changes[0].To);
            Assert.AreSame(a, changes[0].From); // 已销毁包装仍是同一托管实例（假 null）
            Assert.IsFalse(changes[0].HasTarget);
            Assert.IsNull(selection.Previous);

            // 清除后再次无目标为 no-op
            selection.Apply(null, default);
            Assert.AreEqual(1, changes.Count, "已清除后重复 Apply(null) 不再推送");
        }

        /// <summary>
        /// 假选中目标（MonoBehaviour 实现 ISelectable）：记录选中/退场调用序列（共享日志，
        /// 验证跨目标顺序），维护 IsSelected 状态（镜像真实实现方约定）。
        /// </summary>
        private sealed class FakeSelectable : MonoBehaviour, ISelectable
        {
            private List<string> _log;
            private string _label;

            /// <inheritdoc />
            public bool IsSelected { get; set; }

            public void Bind(List<string> log)
            {
                _log = log;
                _label = gameObject.name;
            }

            /// <inheritdoc />
            public bool CanSelect(InteractionContext context) => true;

            /// <inheritdoc />
            public void OnSelected(InteractionContext context)
            {
                IsSelected = true;
                _log?.Add($"{_label}.OnSelected");
            }

            /// <inheritdoc />
            public void OnDeselected()
            {
                IsSelected = false;
                _log?.Add($"{_label}.OnDeselected");
            }
        }
    }
}
