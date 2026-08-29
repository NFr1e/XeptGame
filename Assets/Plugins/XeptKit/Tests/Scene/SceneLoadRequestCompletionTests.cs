using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using XeptKit.Event;
using XeptKit.Scenes;

namespace XeptKit.Tests
{
    /// <summary>
    /// SceneHandle.WaitForCompletionAsync 全终态完成（Scenes.design.md §5 评审修订 P1）：
    /// 句柄加载完成源在成功/失败/取消（含排队取消）全部终态同步完成，持句柄调用方不永久等待。
    /// 本文件覆盖 EditMode 可测的取消路径（不触碰真实 SceneManager）：
    /// 排队取消（SceneLoadRequest.CancelBeforeExecution 覆写）与执行入口已取消；
    /// 失败（op == null）与执行中取消（CleanupOnCancelAsync）依赖真实场景加载，归 playmode 集成测试。
    /// </summary>
    public class SceneLoadRequestCompletionTests
    {
        private static SceneLoadRequest CreateRequest(out SceneHandle handle, out CancellationTokenSource cts)
        {
            cts = new CancellationTokenSource();
            var request = new SceneLoadRequest(
                new SceneReference("Assets/Fake/Scene.unity"),
                SceneLoadOptions.Default,
                new EventBus(),
                sequenceNumber: 1,
                cts.Token);
            handle = request.Handle;
            return request;
        }

        /// <summary>断言任务以 OperationCanceledException（或其派生 TaskCanceledException）结束。</summary>
        private static void AssertCanceled(Task task, string message)
        {
            bool threw = false;
            try
            {
                task.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                threw = true;
            }
            Assert.IsTrue(threw, message);
        }

        [Test]
        public void 排队取消_请求与句柄完成源均以取消结束()
        {
            var request = CreateRequest(out var handle, out var cts);

            cts.Cancel(); // 镜像管理器 TryStartNext 的排队取消前提（Token 已取消）
            request.CancelBeforeExecution();

            // 请求源与句柄源同终态（§5 P1）：await 均以取消结束，不永久等待
            AssertCanceled(request.CompletionTask.AsTask(), "排队取消后请求完成源应以取消结束。");
            AssertCanceled(handle.WaitForCompletionAsync().AsTask(), "排队取消后句柄 WaitForCompletionAsync 应以取消结束。");
            Assert.AreEqual(SceneState.Pending, handle.State, "排队取消未执行，句柄状态应保持 Pending。");

            cts.Dispose();
        }

        [Test]
        public void 执行入口已取消_请求与句柄完成源均以取消结束()
        {
            var request = CreateRequest(out var handle, out var cts);

            cts.Cancel();

            // 入口取消分支无 await，同步完成；ExecuteAsync 自身正常返回（取消经完成源表达，不抛）
            request.ExecuteAsync().AsTask().GetAwaiter().GetResult();

            AssertCanceled(request.CompletionTask.AsTask(), "执行入口已取消后请求完成源应以取消结束。");
            AssertCanceled(handle.WaitForCompletionAsync().AsTask(), "执行入口已取消后句柄 WaitForCompletionAsync 应以取消结束。");
            Assert.AreEqual(SceneState.Pending, handle.State, "入口取消未进入 Loading，句柄状态应保持 Pending。");

            cts.Dispose();
        }
    }
}
