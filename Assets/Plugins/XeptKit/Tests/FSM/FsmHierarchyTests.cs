using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using XeptKit.FSM;

namespace XeptKit.Tests
{
    /// <summary>
    /// Fsm 层级（CompositeStateBase）语义测试：
    /// 父状态进入创建子机器、子切换不重入父、Tick 父先子后、
    /// 层级查询、跨父转移（RootFsm）、StateChanged 层级透明、优雅退出先子后父。
    /// </summary>
    public class FsmHierarchyTests
    {
        private static void Await(UniTask task) => task.GetAwaiter().GetResult();

        // ---- 测试状态 ----

        public class TestParent : CompositeStateBase
        {
            public static int EnterCount;
            public static int ExitCount;
            public static int SubChangedCount;
            public static readonly List<Type> TickLog = new();
            public static readonly List<Type> ExitLog = new();

            protected override Type ResolveInitialSubState() => typeof(TestIdle);

            public override void OnEnter() => EnterCount++;

            public override void OnExit()
            {
                ExitCount++;
                ExitLog.Add(typeof(TestParent));
            }

            public override void Update(float deltaTime) => TickLog.Add(typeof(TestParent));

            protected internal override void OnSubStateChanged(StateBase subState) => SubChangedCount++;
        }

        public class TestIdle : StateBase
        {
            public static readonly List<Type> TickLog = new();
            public static readonly List<Type> ExitLog = new();

            public override void Update(float deltaTime) => TickLog.Add(typeof(TestIdle));

            public override void OnExit() => ExitLog.Add(typeof(TestIdle));
        }

        public class TestWalk : StateBase
        {
        }

        public class TestAir : StateBase
        {
        }

        [SetUp]
        public void ResetCounters()
        {
            TestParent.EnterCount = 0;
            TestParent.ExitCount = 0;
            TestParent.SubChangedCount = 0;
            TestParent.TickLog.Clear();
            TestParent.ExitLog.Clear();
            TestIdle.TickLog.Clear();
            TestIdle.ExitLog.Clear();
        }

        [Test]
        public void EnteringComposite_CreatesSubMachine_EntersInitialSubState()
        {
            var fsm = new Fsm();
            var entered = new List<Type>();
            fsm.StateChanged += s => entered.Add(s.To.GetType());

            Await(fsm.ChangeStateAsync<TestParent>());

            Assert.AreEqual(typeof(TestParent), fsm.RootStateType, "顶层状态应为父状态");
            Assert.AreEqual(typeof(TestIdle), fsm.CurrentStateType, "CurrentState 应下钻到叶子（初始子状态）");
            Assert.AreEqual(1, TestParent.EnterCount, "父状态仅进入一次");
            Assert.Contains(typeof(TestIdle), entered, "StateChanged 应包含初始子状态");
            Assert.Contains(typeof(TestParent), entered, "StateChanged 应包含父状态");
        }

        [Test]
        public void SubStateTransition_DoesNotReenterParent()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TestParent>());
            Assert.AreEqual(1, TestParent.EnterCount);

            // 经子状态实例的 Fsm（子机器）切换叶子状态
            fsm.CurrentState.Fsm.RequestChange<TestWalk>();

            Assert.AreEqual(typeof(TestWalk), fsm.CurrentStateType, "子切换后叶子应为 TestWalk");
            Assert.AreEqual(typeof(TestParent), fsm.RootStateType, "顶层仍为父状态");
            Assert.AreEqual(1, TestParent.EnterCount, "子切换不应重入父状态");
            Assert.AreEqual(0, TestParent.ExitCount, "子切换不应退出父状态");
        }

        [Test]
        public void Tick_RunsParentUpdateThenSubUpdate()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TestParent>());

            fsm.Tick(0.016f);

            Assert.AreEqual(2, TestParent.TickLog.Count + TestIdle.TickLog.Count, "父与子各 Update 一次");
            Assert.AreEqual(typeof(TestParent), TestParent.TickLog[0], "父 Update 先于子");
            Assert.AreEqual(typeof(TestIdle), TestIdle.TickLog[0], "子 Update 随后");
        }

        [Test]
        public void IsInHierarchy_MatchesParentAndLeaf()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TestParent>());

            Assert.IsTrue(fsm.IsInHierarchy(typeof(TestParent)), "分支内应匹配父类型");
            Assert.IsTrue(fsm.IsInHierarchy(typeof(TestIdle)), "分支内应匹配叶子类型");
            Assert.IsFalse(fsm.IsInHierarchy(typeof(TestAir)), "其他分支不应匹配");
        }

        [Test]
        public void SubState_CrossParentTransition_ViaRootFsm()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TestParent>());

            // 当前叶子（TestIdle 或切换后）经 RootFsm 发起顶层转移（模拟跳跃转 Air）
            fsm.CurrentState.RootFsm.RequestChange<TestAir>();

            Assert.AreEqual(typeof(TestAir), fsm.RootStateType, "跨父转移应切到顶层 Air");
            Assert.AreEqual(typeof(TestAir), fsm.CurrentStateType);
            Assert.AreEqual(1, TestParent.ExitCount, "离开父状态应触发父 Exit");
        }

        [Test]
        public void StateChanged_IsLevelTransparent_ForSubTransitions()
        {
            var fsm = new Fsm();
            var entered = new List<Type>();
            fsm.StateChanged += s => entered.Add(s.To.GetType());
            Await(fsm.ChangeStateAsync<TestParent>());
            entered.Clear();

            fsm.CurrentState.Fsm.RequestChange<TestWalk>();

            Assert.Contains(typeof(TestWalk), entered, "子状态切换应经顶层 StateChanged 通知");
            Assert.AreEqual(1, TestParent.SubChangedCount, "父状态 OnSubStateChanged 应回调");
        }

        [Test]
        public void DisposeAsync_ExitsSubMachineBeforeParent()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TestParent>());

            Await(fsm.DisposeAsync());

            Assert.AreEqual(1, TestIdle.ExitLog.Count, "子状态应优雅退出");
            Assert.AreEqual(1, TestParent.ExitLog.Count, "父状态应优雅退出");
            Assert.AreEqual(typeof(TestIdle), TestIdle.ExitLog[0], "子状态先退出");
            Assert.AreEqual(typeof(TestParent), TestParent.ExitLog[0], "父状态后退出");
            Assert.IsNull(fsm.CurrentState, "释放后应清空当前状态");
        }
    }
}
