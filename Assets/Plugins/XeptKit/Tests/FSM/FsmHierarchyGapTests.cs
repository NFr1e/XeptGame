using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using XeptKit.FSM;

namespace XeptKit.Tests
{
    /// <summary>
    /// Fsm 层级（HFSM）缺口测试（XeptKit-Feedback-3C §6 建议补齐的四类路径 + 评审补充）：
    /// 多层嵌套（≥2 级）、取消（OCE）在复合状态上的收敛、根释放与子机器转移并发、
    /// 初始子状态返回 null、祖先复合类型幂等、深叶子跨层级转移与整棵子树退出、RootFsm 回溯。
    /// </summary>
    public class FsmHierarchyGapTests
    {
        private static void Await(UniTask task) => task.GetAwaiter().GetResult();

        private static void AssertCanceled(UniTask task, string message)
        {
            bool threw = false;
            try
            {
                Await(task);
            }
            catch (OperationCanceledException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, message);
        }

        // ---- 测试状态（两层嵌套：Outer → Mid → Leaf）----

        public class GapOuter : CompositeStateBase
        {
            public static int EnterCount, ExitCount, SubChangedCount, UpdateCalls;
            public static readonly List<Type> TickLog = new();
            public static readonly List<Type> ExitLog = new();

            protected override Type ResolveInitialSubState() => typeof(GapMid);

            public override void OnEnter() => EnterCount++;

            public override void OnExit() { ExitCount++; ExitLog.Add(typeof(GapOuter)); }

            public override void Update(float deltaTime) { UpdateCalls++; TickLog.Add(typeof(GapOuter)); }

            protected internal override void OnSubStateChanged(StateBase subState) => SubChangedCount++;
        }

        public class GapMid : CompositeStateBase
        {
            public static int EnterCount, ExitCount, SubChangedCount, UpdateCalls;
            public static readonly List<Type> TickLog = new();
            public static readonly List<Type> ExitLog = new();

            protected override Type ResolveInitialSubState() => typeof(GapLeaf);

            public override void OnEnter() => EnterCount++;

            public override void OnExit() { ExitCount++; ExitLog.Add(typeof(GapMid)); }

            public override void Update(float deltaTime) { UpdateCalls++; TickLog.Add(typeof(GapMid)); }

            protected internal override void OnSubStateChanged(StateBase subState) => SubChangedCount++;
        }

        public class GapLeaf : StateBase
        {
            public static int EnterCount, ExitCount, UpdateCalls;
            public static readonly List<Type> TickLog = new();
            public static readonly List<Type> ExitLog = new();

            public override void OnEnter() => EnterCount++;

            public override void OnExit() { ExitCount++; ExitLog.Add(typeof(GapLeaf)); }

            public override void Update(float deltaTime) { UpdateCalls++; TickLog.Add(typeof(GapLeaf)); }
        }

        public class GapLeaf2 : StateBase
        {
            public static int EnterCount;

            public override void OnEnter() => EnterCount++;
        }

        /// <summary>EnterAsync 永久挂起（忽略令牌）：用于复合状态 Enter 取消收敛路径。</summary>
        public class SuspendEnterComposite : CompositeStateBase
        {
            protected override Type ResolveInitialSubState() => typeof(GapLeaf);

            public override UniTask EnterAsync(CancellationToken cancellationToken = default)
            {
                return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously).Task.AsUniTask();
            }
        }

        /// <summary>初始叶子 InitAsync 挂起（由测试静态门放行）：用于"子机器已创建但初始子状态进入被取消"的收敛补完路径。</summary>
        public class SuspendInitComposite : CompositeStateBase
        {
            protected override Type ResolveInitialSubState() => typeof(SuspendInitLeaf);
        }

        public class SuspendInitLeaf : StateBase
        {
            public static UniTaskCompletionSource Gate;

            public override UniTask InitAsync(CancellationToken cancellationToken = default)
            {
                Gate ??= new UniTaskCompletionSource();
                return Gate.Task;
            }
        }

        /// <summary>EnterAsync 永久挂起：用于子机器转移中根释放的并发路径。</summary>
        public class SuspendLeaf : StateBase
        {
            public override UniTask EnterAsync(CancellationToken cancellationToken = default)
            {
                return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously).Task.AsUniTask();
            }
        }

        /// <summary>初始子状态返回 null：fail-fast。</summary>
        public class NullInitialComposite : CompositeStateBase
        {
            protected override Type ResolveInitialSubState() => null;
        }

        [SetUp]
        public void Reset()
        {
            GapOuter.EnterCount = GapOuter.ExitCount = GapOuter.SubChangedCount = GapOuter.UpdateCalls = 0;
            GapOuter.TickLog.Clear();
            GapOuter.ExitLog.Clear();
            GapMid.EnterCount = GapMid.ExitCount = GapMid.SubChangedCount = GapMid.UpdateCalls = 0;
            GapMid.TickLog.Clear();
            GapMid.ExitLog.Clear();
            GapLeaf.EnterCount = GapLeaf.ExitCount = GapLeaf.UpdateCalls = 0;
            GapLeaf.TickLog.Clear();
            GapLeaf.ExitLog.Clear();
            GapLeaf2.EnterCount = 0;
            SuspendInitLeaf.Gate = null;
        }

        [Test]
        public void 多层嵌套_进入时逐级创建子机器_CurrentState下钻到最深叶子()
        {
            var fsm = new Fsm();
            var entered = new List<Type>();
            fsm.StateChanged += s => entered.Add(s.To.GetType());

            Await(fsm.ChangeStateAsync<GapOuter>());

            Assert.AreEqual(typeof(GapOuter), fsm.RootStateType);
            Assert.AreEqual(typeof(GapLeaf), fsm.CurrentStateType, "CurrentState 应下钻两层到叶子");
            Assert.AreEqual(new List<Type> { typeof(GapLeaf), typeof(GapMid), typeof(GapOuter) }, entered,
                "StateChanged 层级透明，最深层先触发");
            Assert.AreEqual(0, GapOuter.SubChangedCount, "初始进入不回调 OnSubStateChanged");
            Assert.AreEqual(0, GapMid.SubChangedCount, "嵌套初始进入不回调 OnSubStateChanged");
        }

        [Test]
        public void 多层嵌套_Tick逐级下传_父先子后()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<GapOuter>());

            fsm.Tick(0.016f);

            Assert.AreEqual(1, GapOuter.UpdateCalls);
            Assert.AreEqual(1, GapMid.UpdateCalls);
            Assert.AreEqual(1, GapLeaf.UpdateCalls);
            Assert.AreEqual(new List<Type> { typeof(GapOuter) }, GapOuter.TickLog, "父先");
            Assert.AreEqual(new List<Type> { typeof(GapMid) }, GapMid.TickLog, "中次");
            Assert.AreEqual(new List<Type> { typeof(GapLeaf) }, GapLeaf.TickLog, "叶后");
        }

        [Test]
        public void 多层嵌套_子切换_祖先链OnSubStateChanged全回调_StateChanged冒泡到根()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<GapOuter>());
            var entered = new List<Type>();
            Fsm.StateChangeArgs last = default;
            fsm.StateChanged += s => { entered.Add(s.To.GetType()); last = s; };

            fsm.CurrentState.Fsm.RequestChange<GapLeaf2>();

            Assert.AreEqual(typeof(GapLeaf2), fsm.CurrentStateType);
            Assert.Contains(typeof(GapLeaf2), entered, "子切换应冒泡到根 StateChanged");
            Assert.AreEqual(typeof(GapLeaf), last.From.GetType(), "子切换透传 from = 旧叶子");
            Assert.AreEqual(typeof(GapLeaf2), last.To.GetType(), "子切换透传 to = 新叶子");
            Assert.AreEqual(1, GapOuter.SubChangedCount, "外层祖先应收到后代切换回调");
            Assert.AreEqual(1, GapMid.SubChangedCount, "内层祖先应收到后代切换回调");
        }

        [Test]
        public void 多层嵌套_IsInHierarchy匹配任意层级()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<GapOuter>());

            Assert.IsTrue(fsm.IsInHierarchy(typeof(GapOuter)));
            Assert.IsTrue(fsm.IsInHierarchy(typeof(GapMid)), "中间复合类型应匹配");
            Assert.IsTrue(fsm.IsInHierarchy(typeof(GapLeaf)));
            Assert.IsFalse(fsm.IsInHierarchy(typeof(GapLeaf2)));
        }

        [Test]
        public void 祖先复合类型重进请求_幂等跳过()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<GapOuter>());

            Await(fsm.ChangeStateAsync<GapOuter>());

            Assert.AreEqual(typeof(GapOuter), fsm.RootStateType);
            Assert.AreEqual(1, GapOuter.EnterCount, "分支内重进复合类型应幂等跳过");
            Assert.AreEqual(1, GapLeaf.EnterCount);
        }

        [Test]
        public void 多层嵌套_RootFsm回溯到根机器()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<GapOuter>());

            Assert.AreSame(fsm, fsm.CurrentState.RootFsm, "最深层叶子 RootFsm 应回溯到根");
        }

        [Test]
        public void 多层嵌套_深叶子经RootFsm跨层级转移_整棵子树先子后父退出()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<GapOuter>());

            fsm.CurrentState.RootFsm.RequestChange<GapLeaf2>();

            Assert.AreEqual(typeof(GapLeaf2), fsm.RootStateType);
            Assert.AreEqual(typeof(GapLeaf2), fsm.CurrentStateType);
            Assert.AreEqual(1, GapOuter.ExitCount);
            Assert.AreEqual(1, GapMid.ExitCount);
            Assert.AreEqual(1, GapLeaf.ExitCount);
            Assert.AreEqual(new List<Type> { typeof(GapLeaf) }, GapLeaf.ExitLog, "叶子先退出");
            Assert.AreEqual(new List<Type> { typeof(GapMid) }, GapMid.ExitLog, "中间随后");
            Assert.AreEqual(new List<Type> { typeof(GapOuter) }, GapOuter.ExitLog, "顶层最后");
        }

        [Test]
        public void 复合状态_Enter取消_收敛补齐子机器与StateChanged()
        {
            var fsm = new Fsm();
            var cts = new CancellationTokenSource();
            var entered = new List<Type>();
            fsm.StateChanged += s => entered.Add(s.To.GetType());

            var task = fsm.ChangeStateAsync<SuspendEnterComposite>(cts.Token);
            cts.Cancel();
            AssertCanceled(task, "Enter 取消上抛 OCE");

            Assert.AreEqual(typeof(SuspendEnterComposite), fsm.RootStateType);
            Assert.AreEqual(typeof(GapLeaf), fsm.CurrentStateType, "收敛应补齐子机器并进入初始子状态");
            Assert.AreEqual(1, GapLeaf.EnterCount);
            Assert.AreEqual(typeof(GapLeaf), entered[0], "StateChanged 最深层先触发");
            Assert.AreEqual(typeof(SuspendEnterComposite), entered[1]);
        }

        [Test]
        public void 复合状态_初始子状态进入被取消_收敛补完到初始叶子()
        {
            var fsm = new Fsm();
            var cts = new CancellationTokenSource();

            var task = fsm.ChangeStateAsync<SuspendInitComposite>(cts.Token);
            cts.Cancel(); // 初始叶子 InitAsync 挂起 → 取消 → 子机器存在但无当前状态
            SuspendInitLeaf.Gate.TrySetResult(); // 放行收敛补完路径（UniTask 续延同线程内联完成）

            AssertCanceled(task, "取消上抛 OCE");
            Assert.AreEqual(typeof(SuspendInitComposite), fsm.RootStateType);
            Assert.AreEqual(typeof(SuspendInitLeaf), fsm.CurrentStateType, "补完：初始子状态以 None 令牌进入完成");
        }

        [Test]
        public void 子机器转移中_根DisposeAsync_回退同步终止_不挂起不抛异常()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<GapOuter>());
            var subCts = new CancellationTokenSource();

            fsm.CurrentState.Fsm.RequestChange<SuspendLeaf>(subCts.Token); // 子机器（最深）转移挂起

            Assert.DoesNotThrow(() => { Await(fsm.DisposeAsync()); });
            Assert.IsNull(fsm.CurrentState, "释放后应清空当前状态");

            subCts.Cancel(); // 释放挂起的子转移，避免悬挂任务泄漏
        }

        [Test]
        public void 初始子状态返回null_转移failfast抛异常_机器复位()
        {
            var fsm = new Fsm();

            var ex = Assert.Throws<InvalidOperationException>(() => { Await(fsm.ChangeStateAsync<NullInitialComposite>()); });
            StringAssert.Contains("ResolveInitialSubState 返回 null", ex.Message);
            Assert.IsFalse(fsm.IsTransitioning, "异常后转移标志复位");
        }
    }
}
