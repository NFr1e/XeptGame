using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using XeptKit.Event;
using XeptKit.Scenes;

namespace XeptKit.Tests
{
    /// <summary>
    /// SwitchMainSceneAsync 并发切换门闩（Scenes.design.md §8「并发切换不支持（评审修订 P1，fail-fast）」）：
    /// 已有切换进行中时再次调用同步抛 InvalidOperationException（不做静默排队）；门闩在 finally 释放
    /// （失败/取消路径同样释放，且先释放再执行过渡退出）；过渡进入失败不执行 ExitAsync（§8/§10 决议 C）。
    /// 测试经"挂起在过渡进入"的 BlockingTransition 占住门闩，全程不触碰真实 SceneManager。
    /// </summary>
    public class SwitchMainSceneConcurrencyTests
    {
        private static readonly SceneReference Ref = new SceneReference("Assets/Fake/Scene.unity");

        /// <summary>EnterAsync 永不完成（由测试控制释放），用于占住切换门闩。</summary>
        private sealed class BlockingTransition : ISceneTransition
        {
            private readonly UniTaskCompletionSource _enterSource = new();

            public float Progress => 0f;

            public UniTask EnterAsync() => _enterSource.Task;

            public UniTask ExitAsync() => UniTask.CompletedTask;

            public void Release() => _enterSource.TrySetResult();
        }

        /// <summary>EnterAsync 同步抛异常；ExitAsync 记录是否被调用（决议 C 断言用）。</summary>
        private sealed class FailingTransition : ISceneTransition
        {
            public bool ExitCalled { get; private set; }

            public float Progress => 0f;

            public UniTask EnterAsync() => throw new InvalidOperationException("EnterAsync failed (test)");

            public UniTask ExitAsync()
            {
                ExitCalled = true;
                return UniTask.CompletedTask;
            }
        }

        /// <summary>EnterAsync 立即完成；ExitAsync 永不完成（由测试控制释放）——占住门闩的过渡退出窗口。</summary>
        private sealed class BlockingExitTransition : ISceneTransition
        {
            private readonly UniTaskCompletionSource _exitSource = new();

            public float Progress => 0f;

            public UniTask EnterAsync() => UniTask.CompletedTask;

            public UniTask ExitAsync() => _exitSource.Task;

            public void ReleaseExit() => _exitSource.TrySetResult();
        }

        /// <summary>EnterAsync 立即完成；ExitAsync 同步抛异常（内层 finally 释放门闩断言用）。</summary>
        private sealed class FailingExitTransition : ISceneTransition
        {
            public float Progress => 0f;

            public UniTask EnterAsync() => UniTask.CompletedTask;

            public UniTask ExitAsync() => throw new InvalidOperationException("ExitAsync failed (test)");
        }

        [Test]
        public void 并发切换_第二调用同步抛InvalidOperationException()
        {
            var manager = new ScenesManager(new EventBus());
            var blocker = new BlockingTransition();

            // 第一次切换挂起在过渡进入——门闩已置位（入口同步代码先于首个 await 执行）
            manager.SwitchMainSceneAsync(Ref, blocker, default);

            // 第二次切换同步 fail-fast，不进入复合流程（不排队、不静默等待）
            Assert.Throws<InvalidOperationException>(() =>
            {
                manager.SwitchMainSceneAsync(Ref, null, default);
            });
        }

        [Test]
        public void 过渡进入失败_不执行退出_且门闩已释放()
        {
            var manager = new ScenesManager(new EventBus());
            var failing = new FailingTransition();

            // 决议 C：EnterAsync 失败 → 编排直接上抛，不执行 ExitAsync
            bool threw = false;
            try
            {
                manager.SwitchMainSceneAsync(Ref, failing, default).AsTask().GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "EnterAsync 失败应上抛。");
            Assert.IsFalse(failing.ExitCalled, "决议 C：EnterAsync 失败不得执行 ExitAsync。");

            // 门闩已在 finally 释放（失败路径）：再次进入不抛门闩异常——挂起在新的过渡进入上即证明
            var blocker = new BlockingTransition();
            bool latchThrew = false;
            try
            {
                manager.SwitchMainSceneAsync(Ref, blocker, default);
            }
            catch (InvalidOperationException)
            {
                latchThrew = true;
            }

            Assert.IsFalse(latchThrew, "失败路径后门闩应已释放，再次切换不应抛门闩异常。");
        }

        [Test]
        public void 过渡退出期间_第二次切换被拒绝_完成后门闩释放()
        {
            var manager = new ScenesManager(new EventBus());
            var blocker = new BlockingExitTransition();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // 第一次切换：过渡进入成功 → 加载阶段因已取消令牌同步抛 OCE → finally 进入 ExitAsync 并阻塞。
            // 预取消令牌使 LoadSceneAsync 在触碰 SceneManager 之前即失败，无需真实场景。
            var first = manager.SwitchMainSceneAsync(Ref, blocker, cts.Token);

            // ExitAsync（淡出）尚未完成——门闩保持（评审修订 P2）：第二次切换同步抛 InvalidOperationException
            Assert.Throws<InvalidOperationException>(() =>
            {
                manager.SwitchMainSceneAsync(Ref, null, default);
            });

            // 释放 ExitAsync → 第一次切换以 OCE 结束 → 门闩释放
            blocker.ReleaseExit();
            bool threw = false;
            try
            {
                first.AsTask().GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                threw = true;
            }
            Assert.IsTrue(threw, "已取消令牌的切换应以 OperationCanceledException 结束。");

            // 门闩已释放：再次进入不抛门闩异常（挂起在新的过渡进入上即证明）
            var blocker2 = new BlockingTransition();
            bool latchThrew = false;
            try
            {
                manager.SwitchMainSceneAsync(Ref, blocker2, default);
            }
            catch (InvalidOperationException)
            {
                latchThrew = true;
            }
            Assert.IsFalse(latchThrew, "ExitAsync 完成后门闩应已释放。");
        }

        [Test]
        public void 过渡退出抛异常_门闩仍释放()
        {
            var manager = new ScenesManager(new EventBus());
            var failingExit = new FailingExitTransition();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // 加载阶段抛 OCE → finally 内 ExitAsync 抛 InvalidOperationException（覆盖原异常）→ 内层 finally 释放门闩
            bool threw = false;
            try
            {
                manager.SwitchMainSceneAsync(Ref, failingExit, cts.Token).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                threw = true;
            }
            Assert.IsTrue(threw, "ExitAsync 抛异常时切换应上抛。");

            // 内层 finally 已释放门闩（不永久锁死）：再次进入不抛门闩异常
            var blocker = new BlockingTransition();
            bool latchThrew = false;
            try
            {
                manager.SwitchMainSceneAsync(Ref, blocker, default);
            }
            catch (InvalidOperationException)
            {
                latchThrew = true;
            }
            Assert.IsFalse(latchThrew, "ExitAsync 抛异常后门闩仍应释放（不永久锁死）。");
        }
    }
}
