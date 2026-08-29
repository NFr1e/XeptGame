using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace XeptKit.Scenes
{
    /// <summary>
    /// 场景管理器——封装 Unity 原生场景加载/卸载流程，
    /// 提供类型安全的场景引用、加载队列（优先级调度、可配置并发度）、显式取消、进度追踪、主场景切换与过渡编排。
    /// </summary>
    /// <remarks>
    /// 默认使用 Additive 模式加载。全部异步操作接受显式取消令牌（BCL <see cref="CancellationToken"/>，
    /// 惯例传 <see cref="XeptKit.Core.KitLifecycle.GlobalToken"/>）。
    /// 场景生命周期事件经构造注入的 <see cref="XeptKit.Event.IEventBus"/> 同步广播。
    /// 同一会话内建议仅一个管理器实例驱动场景操作（多实例并行驱动会各自维护追踪、状态分裂）；
    /// 实例由业务组合根创建并持有（本库不设模块门面，见 docs/DESIGN.md §6）。
    /// </remarks>
    public interface IScenesManager
    {
        /// <summary>异步加载场景（Additive 模式），返回 <see cref="SceneHandle"/> 供追踪与卸载。失败抛 <see cref="SceneOperationException"/>（fail-fast）。</summary>
        UniTask<SceneHandle> LoadSceneAsync(
            SceneReference sceneRef,
            SceneLoadOptions options = default,
            CancellationToken cancellationToken = default);

        /// <summary>卸载指定场景。未加载/已卸载场景的卸载幂等成功。失败抛 <see cref="SceneOperationException"/>。</summary>
        UniTask UnloadSceneAsync(
            SceneHandle handle,
            CancellationToken cancellationToken = default);

        /// <summary>切换主场景——取消非持久加载请求（清场）→ 卸载全部非持久场景 → 加载新主场景并设为活跃。
        /// 支持通过 <paramref name="transition"/> 编排过渡（过渡不可取消，见 <see cref="ISceneTransition"/>）。</summary>
        UniTask<SceneHandle> SwitchMainSceneAsync(
            SceneReference sceneRef,
            ISceneTransition transition = null,
            CancellationToken cancellationToken = default);

        /// <summary>加载场景组。任一条目失败抛 <see cref="SceneOperationException"/>（已加载条目保留，不回滚）。</summary>
        UniTask<SceneGroupHandle> LoadSceneGroupAsync(
            SceneGroup group,
            CancellationToken cancellationToken = default);

        /// <summary>卸载场景组内所有已加载的场景。未加载条目静默跳过（幂等）。</summary>
        UniTask UnloadSceneGroupAsync(
            SceneGroupHandle groupHandle,
            CancellationToken cancellationToken = default);

        /// <summary>当前所有已加载的场景句柄（含排队中）。</summary>
        IReadOnlyList<SceneHandle> LoadedScenes { get; }

        /// <summary>当前主场景句柄。未设置时为 null。</summary>
        SceneHandle CurrentMainScene { get; }

        /// <summary>场景是否在 Build Settings 中注册（运行时单轨，经 SceneUtility 校验）。</summary>
        bool SceneExists(SceneReference sceneRef);

        /// <summary>指定场景是否已加载且处于 <see cref="SceneState.Active"/> 状态。</summary>
        bool IsSceneLoaded(SceneReference sceneRef);

        /// <summary>将指定场景设为 Unity 活跃场景（转发到句柄 <see cref="SceneHandle.SetAsActiveScene"/>）。</summary>
        void SetActiveScene(SceneHandle handle);

        /// <summary>聚合进度——所有进行中加载操作的算术平均进度，范围 [0, 1]；无进行中时返回 1f。</summary>
        float OverallProgress { get; }

        /// <summary>当前排队等待的请求数（不含执行中）。</summary>
        int PendingRequestCount { get; }

        /// <summary>生命周期清理入口：清空排队请求与场景追踪、主场景置空。幂等。
        /// 供组合根优雅关闭调用；业务通常不直接调用。</summary>
        void Clear();
    }
}
