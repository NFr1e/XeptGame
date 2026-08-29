using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using XeptKit.Core;
using XeptKit.FSM;

namespace XeptKit.Tests
{
    /// <summary>
    /// Fsm 单层核心语义测试基线（HFSM 移植后回归单层契约）：
    /// 钩子时序、转移序列、幂等（含祖先类型）、重入拒绝、RequestChange pending 仅最新、
    /// Tick 语义、双释放、异常隔离、StateChanged、取消三阶段收敛、IsInHierarchy、RootFsm、非泛型入口。
    /// </summary>
    public class FsmCoreSemanticsTests
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

        // ---- 测试状态 ----

        public interface IAirborne { }

        public abstract class AirborneBase : StateBase { }

        public class JumpState : AirborneBase, IAirborne
        {
            public static int EnterCount;

            public override void OnEnter() => EnterCount++;
        }

        public class MidState : StateBase
        {
            public static int EnterCount;

            public override void OnEnter() => EnterCount++;
        }

        public class ConcreteChild : MidState
        {
            public static int ChildEnterCount;

            public override void OnEnter() => ChildEnterCount++;
        }

        public class TraceA : StateBase
        {
            public static int OnInitCount, OnEnterCount, OnExitCount, UpdateCount;
            public static int InitAsyncC, EnterAsyncC, ExitAsyncC;
            public static readonly List<string> Log = new();

            public override void OnInit() { OnInitCount++; Log.Add("A.OnInit"); }

            public override UniTask InitAsync(CancellationToken cancellationToken = default) { InitAsyncC++; Log.Add("A.InitAsync"); return UniTask.CompletedTask; }

            public override void OnEnter() { OnEnterCount++; Log.Add("A.OnEnter"); }

            public override UniTask EnterAsync(CancellationToken cancellationToken = default) { EnterAsyncC++; Log.Add("A.EnterAsync"); return UniTask.CompletedTask; }

            public override void OnExit() { OnExitCount++; Log.Add("A.OnExit"); }

            public override UniTask ExitAsync(CancellationToken cancellationToken = default) { ExitAsyncC++; Log.Add("A.ExitAsync"); return UniTask.CompletedTask; }

            public override void Update(float deltaTime) => UpdateCount++;
        }

        public class TraceB : StateBase
        {
            public static int EnterCount;

            public override void OnEnter() => EnterCount++;
        }

        public class SuspendInitState : StateBase
        {
            public static int InitAsyncC;

            public override UniTask InitAsync(CancellationToken cancellationToken = default)
            {
                InitAsyncC++;
                return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously).Task.AsUniTask();
            }
        }

        public class SuspendExitState : StateBase
        {
            public static int OnExitC, ExitAsyncC;

            public override void OnExit() => OnExitC++;

            public override UniTask ExitAsync(CancellationToken cancellationToken = default)
            {
                ExitAsyncC++;
                return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously).Task.AsUniTask();
            }
        }

        public class SuspendEnterState : StateBase
        {
            public static int OnEnterC;

            public override void OnEnter() => OnEnterC++;

            public override UniTask EnterAsync(CancellationToken cancellationToken = default)
            {
                return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously).Task.AsUniTask();
            }
        }

        public class ThrowOnEnterState : StateBase
        {
            public static int EnterAsyncC;

            public override void OnEnter() => throw new InvalidOperationException("boom");

            public override UniTask EnterAsync(CancellationToken cancellationToken = default) { EnterAsyncC++; return UniTask.CompletedTask; }
        }

        public class RequestFromUpdateState : StateBase
        {
            public override void Update(float deltaTime) => Fsm.RequestChange<TraceB>();
        }

        public class ContextProbeState : StateBase
        {
            public static object SeenContext;

            public override void OnEnter() => SeenContext = Context;
        }

        public class PlainC : StateBase
        {
            public static int EnterCount;

            public override void OnEnter() => EnterCount++;
        }

        public class PlainD : StateBase
        {
            public static int EnterCount;

            public override void OnEnter() => EnterCount++;
        }

        /// <summary>静音日志实现：隔离语义测试内临时替换 Log，避免预期的异常日志刷红 Console。</summary>
        private sealed class SilentLog : ILog
        {
            public void Info(string message) { }

            public void Warning(string message) { }

            public void Error(string message) { }

            public void Exception(Exception exception) { }
        }

        [SetUp]
        public void Reset()
        {
            JumpState.EnterCount = 0;
            MidState.EnterCount = 0;
            ConcreteChild.ChildEnterCount = 0;
            TraceA.OnInitCount = TraceA.OnEnterCount = TraceA.OnExitCount = TraceA.UpdateCount = 0;
            TraceA.InitAsyncC = TraceA.EnterAsyncC = TraceA.ExitAsyncC = 0;
            TraceA.Log.Clear();
            TraceB.EnterCount = 0;
            SuspendInitState.InitAsyncC = 0;
            SuspendExitState.OnExitC = SuspendExitState.ExitAsyncC = 0;
            SuspendEnterState.OnEnterC = 0;
            ThrowOnEnterState.EnterAsyncC = 0;
            ContextProbeState.SeenContext = null;
            PlainC.EnterCount = 0;
            PlainD.EnterCount = 0;
        }

        [Test]
        public void 首进入_钩子顺序为同步先于异步_无Exit()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TraceA>());

            Assert.AreEqual(new List<string> { "A.OnInit", "A.InitAsync", "A.OnEnter", "A.EnterAsync" }, TraceA.Log);
            Assert.AreEqual(typeof(TraceA), fsm.CurrentStateType);
            Assert.AreEqual(0, TraceA.OnExitCount, "首次进入无退出");
        }

        [Test]
        public void 转移_完整序列_Init新_Exit旧_Enter新()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TraceA>());
            TraceA.Log.Clear();

            Await(fsm.ChangeStateAsync<TraceB>());

            Assert.AreEqual(typeof(TraceB), fsm.CurrentStateType);
            Assert.AreEqual(new List<string> { "A.OnExit", "A.ExitAsync" }, TraceA.Log, "旧状态退出序列");
            Assert.AreEqual(1, TraceB.EnterCount);
        }

        [Test]
        public void 幂等_目标即当前_不触发转移()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TraceA>());
            var before = TraceA.Log.Count;

            Await(fsm.ChangeStateAsync<TraceA>());

            Assert.AreEqual(before, TraceA.Log.Count, "重复进入同类型应幂等跳过");
        }

        [Test]
        public void 祖先类型转移请求_幂等跳过()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<ConcreteChild>());

            Await(fsm.ChangeStateAsync<MidState>());

            Assert.AreEqual(typeof(ConcreteChild), fsm.CurrentStateType, "祖先类型请求应被幂等跳过");
            Assert.AreEqual(1, ConcreteChild.ChildEnterCount, "ConcreteChild 自身仅进入一次");
            Assert.AreEqual(0, MidState.EnterCount, "MidState 作为转移目标从未进入");
        }

        [Test]
        public void 重入_转移中ChangeStateAsync抛异常()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TraceA>());
            var cts = new CancellationTokenSource();
            var task = fsm.ChangeStateAsync<SuspendEnterState>(cts.Token);

            Assert.Throws<InvalidOperationException>(() => { Await(fsm.ChangeStateAsync<TraceB>()); });

            cts.Cancel();
            AssertCanceled(task, "挂起转移取消后应上抛 OCE");
            Assert.AreEqual(typeof(SuspendEnterState), fsm.CurrentStateType, "Enter 取消收敛：新状态为当前");
        }

        [Test]
        public void RequestChange_Update内触发_同栈完成转移()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<RequestFromUpdateState>());

            fsm.Tick(0.016f);

            Assert.AreEqual(typeof(TraceB), fsm.CurrentStateType, "Update 内 RequestChange 应立即发起并同栈完成");
        }

        [Test]
        public void RequestChange_目标为当前_幂等跳过()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TraceA>());
            var before = TraceA.Log.Count;

            fsm.RequestChange<TraceA>();

            Assert.AreEqual(before, TraceA.Log.Count);
        }

        [Test]
        public void RequestChange_转移中登记pending_仅最新生效()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TraceA>());
            var cts = new CancellationTokenSource();
            var task = fsm.ChangeStateAsync<SuspendEnterState>(cts.Token);

            fsm.RequestChange<PlainC>();
            fsm.RequestChange<PlainD>(); // 覆盖 pending，仅保留最新
            Assert.AreEqual(0, PlainC.EnterCount);
            Assert.AreEqual(0, PlainD.EnterCount);

            cts.Cancel();
            AssertCanceled(task, "挂起转移取消后上抛 OCE");
            Assert.AreEqual(typeof(PlainD), fsm.CurrentStateType, "pending 仅最新生效");
            Assert.AreEqual(0, PlainC.EnterCount, "被覆盖的 pending 不应执行");
        }

        [Test]
        public void Tick_驱动当前Update_转移中noop()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TraceA>());
            fsm.Tick(0.016f);
            Assert.AreEqual(1, TraceA.UpdateCount);

            var cts = new CancellationTokenSource();
            var task = fsm.ChangeStateAsync<SuspendEnterState>(cts.Token);
            fsm.Tick(0.016f);

            Assert.AreEqual(1, TraceA.UpdateCount, "转移中 Tick 应为 no-op");
            cts.Cancel();
            AssertCanceled(task, "");
        }

        [Test]
        public void Dispose_标记终止_操作failfast_引用保留()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TraceA>());

            fsm.Dispose();

            Assert.Throws<InvalidOperationException>(() => { Await(fsm.ChangeStateAsync<TraceB>()); });
            Assert.Throws<InvalidOperationException>(() => fsm.Tick(0.1f));
            Assert.AreEqual(typeof(TraceA), fsm.CurrentStateType, "同步 Dispose 不清引用");
        }

        [Test]
        public void DisposeAsync_优雅退出_清空引用_幂等()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TraceA>());

            Await(fsm.DisposeAsync());

            Assert.AreEqual(1, TraceA.OnExitCount);
            Assert.IsNull(fsm.CurrentState);
            Await(fsm.DisposeAsync()); // 幂等 no-op
            Assert.Throws<InvalidOperationException>(() => { Await(fsm.ChangeStateAsync<TraceB>()); });
        }

        [Test]
        public void DisposeAsync_转移中抛异常()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TraceA>());
            var cts = new CancellationTokenSource();
            var task = fsm.ChangeStateAsync<SuspendEnterState>(cts.Token);

            Assert.Throws<InvalidOperationException>(() => { Await(fsm.DisposeAsync()); });

            cts.Cancel();
            AssertCanceled(task, "");
        }

        [Test]
        public void DisposeAsync_CancellationToken_取消中断退出仍收敛清空()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<SuspendExitState>());
            var cts = new CancellationTokenSource();

            var disposeTask = fsm.DisposeAsync(cts.Token);
            Assert.AreEqual(1, SuspendExitState.OnExitC, "同步 OnExit 不被取消跳过");

            cts.Cancel();
            Await(disposeTask); // 取消不向调用方传播，机器收敛到终止态

            Assert.IsNull(fsm.CurrentState, "取消后引用仍清空");
            Assert.Throws<InvalidOperationException>(() => { Await(fsm.ChangeStateAsync<TraceB>()); });
        }

        [Test]
        public void 业务异常隔离_钩子抛异常_转移照常推进()
        {
            var fsm = new Fsm();
            var reported = new List<Type>();
            fsm.TransitionException += (t, ex) => reported.Add(t);

            Await(fsm.ChangeStateAsync<TraceA>());

            var originalLog = Log.GetImplementation();
            Log.SetImplementation(new SilentLog()); // 静音预期异常日志（隔离语义验证，不刷红 Console）
            try
            {
                Await(fsm.ChangeStateAsync<ThrowOnEnterState>());
            }
            finally
            {
                Log.SetImplementation(originalLog); // 无论断言成败均还原
            }

            Assert.AreEqual(typeof(ThrowOnEnterState), fsm.CurrentStateType, "异常隔离后转移照常推进");
            Assert.Contains(typeof(ThrowOnEnterState), reported);
            Assert.AreEqual(1, ThrowOnEnterState.EnterAsyncC, "OnEnter 抛异常不影响后续 EnterAsync");
        }

        [Test]
        public void TransitionException_订阅者异常被隔离()
        {
            var fsm = new Fsm();
            fsm.TransitionException += (t, ex) => throw new InvalidOperationException("subscriber boom");

            var originalLog = Log.GetImplementation();
            Log.SetImplementation(new SilentLog()); // 静音预期异常日志（订阅者异常亦被隔离）
            try
            {
                Assert.DoesNotThrow(() => { Await(fsm.ChangeStateAsync<ThrowOnEnterState>()); });
            }
            finally
            {
                Log.SetImplementation(originalLog);
            }

            Assert.AreEqual(typeof(ThrowOnEnterState), fsm.CurrentStateType);
        }

        [Test]
        public void StateChanged_状态完全进入后触发()
        {
            var fsm = new Fsm();
            StateBase entered = null;
            fsm.StateChanged += s => entered = s.To;

            Await(fsm.ChangeStateAsync<TraceA>());

            Assert.IsNotNull(entered);
            Assert.AreSame(fsm.CurrentState, entered, "事件参数应为已进入的状态实例");
        }

        [Test]
        public void StateChanged_携带正确的From与To()
        {
            var fsm = new Fsm();
            Fsm.StateChangeArgs last = default;
            fsm.StateChanged += s => last = s;

            Await(fsm.ChangeStateAsync<TraceA>());
            Assert.IsNull(last.From, "首次进入 From 应为 null");
            Assert.AreEqual(typeof(TraceA), last.To.GetType());

            Await(fsm.ChangeStateAsync<TraceB>());
            Assert.AreEqual(typeof(TraceA), last.From.GetType(), "From 应为转移前的旧状态（非新状态）");
            Assert.AreEqual(typeof(TraceB), last.To.GetType());
        }

        [Test]
        public void Context注入_状态经Context读写共享数据()
        {
            var ctx = new object();
            var fsm = new Fsm(ctx);

            Await(fsm.ChangeStateAsync<ContextProbeState>());

            Assert.AreSame(ctx, ContextProbeState.SeenContext);
        }

        [Test]
        public void 取消_Init阶段取消_旧状态保持当前()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TraceA>());
            var cts = new CancellationTokenSource();

            var task = fsm.ChangeStateAsync<SuspendInitState>(cts.Token);
            cts.Cancel();
            AssertCanceled(task, "Init 取消应上抛 OCE");

            Assert.AreEqual(typeof(TraceA), fsm.CurrentStateType, "Init 取消旧状态未动，保持当前");
            Assert.AreEqual(1, SuspendInitState.InitAsyncC);
            Assert.AreEqual(0, TraceA.OnExitCount, "Init 阶段未开始退出");
        }

        [Test]
        public void 取消_Exit阶段取消_收敛强制进入新状态()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<SuspendExitState>());
            var cts = new CancellationTokenSource();
            var entered = new List<Type>();
            fsm.StateChanged += s => entered.Add(s.To.GetType());

            var task = fsm.ChangeStateAsync<TraceB>(cts.Token);
            cts.Cancel();
            AssertCanceled(task, "Exit 取消上抛 OCE");

            Assert.AreEqual(typeof(TraceB), fsm.CurrentStateType, "Exit 取消收敛：新状态成为当前");
            Assert.AreEqual(1, SuspendExitState.OnExitC, "旧状态 OnExit 仅一次（无二次退出）");
            Assert.Contains(typeof(TraceB), entered, "收敛路径应补发 StateChanged");
        }

        [Test]
        public void 取消_Enter阶段取消_采纳新状态并触发事件()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TraceA>());
            var cts = new CancellationTokenSource();
            var entered = new List<Type>();
            fsm.StateChanged += s => entered.Add(s.To.GetType());

            var task = fsm.ChangeStateAsync<SuspendEnterState>(cts.Token);
            cts.Cancel();
            AssertCanceled(task, "Enter 取消上抛 OCE");

            Assert.AreEqual(typeof(SuspendEnterState), fsm.CurrentStateType, "Enter 取消采纳新状态为当前");
            Assert.Contains(typeof(SuspendEnterState), entered, "Enter 取消应补发 StateChanged");
        }

        [Test]
        public void 取消_fireAndForget路径_OCE静默吞掉()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TraceA>());
            var cts = new CancellationTokenSource();
            cts.Cancel();
            bool reported = false;
            fsm.TransitionException += (t, ex) => reported = true;

            Assert.DoesNotThrow(() => fsm.RequestChange<TraceB>(cts.Token));
            Assert.AreEqual(typeof(TraceA), fsm.CurrentStateType, "预取消令牌：Init 阶段取消保持当前");
            Assert.IsFalse(reported, "取消不应作为业务异常上报");
        }

        [Test]
        public void IsInHierarchy_单层匹配_当前类型基类接口_其他分支否()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<JumpState>());

            Assert.IsTrue(fsm.IsInHierarchy(typeof(JumpState)));
            Assert.IsTrue(fsm.IsInHierarchy(typeof(AirborneBase)), "抽象基类应匹配");
            Assert.IsTrue(fsm.IsInHierarchy(typeof(IAirborne)), "接口应匹配");
            Assert.IsFalse(fsm.IsInHierarchy(typeof(TraceA)));
            Assert.IsFalse(fsm.IsInHierarchy(null), "null 目标应返回 false");
        }

        [Test]
        public void RootFsm_单层返回自身机器()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<JumpState>());

            Assert.AreSame(fsm, fsm.CurrentState.RootFsm);
        }

        [Test]
        public void RequestChange非泛型_动态目标类型()
        {
            var fsm = new Fsm();
            Await(fsm.ChangeStateAsync<TraceA>());

            fsm.RequestChange(typeof(JumpState));

            Assert.AreEqual(typeof(JumpState), fsm.CurrentStateType);
        }
    }
}
