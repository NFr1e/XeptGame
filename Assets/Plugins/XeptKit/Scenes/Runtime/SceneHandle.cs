using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace XeptKit.Scenes
{
    /// <summary>
    /// 场景生命周期句柄——<see cref="IScenesManager.LoadSceneAsync"/> 的返回值。
    /// 场景加载完成后句柄持续有效，直到场景被卸载。
    /// 业务只读：状态（<see cref="State"/> / <see cref="Progress"/> / <see cref="Scene"/>）由请求/管理器驱动。
    /// </summary>
    public sealed class SceneHandle
    {
        private readonly UniTaskCompletionSource _loadCompletionSource = new();
        private readonly UniTaskCompletionSource _activationSource = new();

        /// <summary>此句柄对应的 Unity Scene 结构体。场景加载完成前访问将得到无效 Scene。</summary>
        public Scene Scene { get; internal set; }

        /// <summary>场景引用标识。</summary>
        public SceneReference SceneRef { get; }

        /// <summary>当前生命周期状态。初始 <see cref="SceneState.Pending"/>。</summary>
        public SceneState State { get; internal set; } = SceneState.Pending;

        /// <summary>加载/卸载进度，范围 [0, 1]。</summary>
        public float Progress { get; internal set; }

        /// <summary>是否为持久场景（构造期确定，只读；不会被 <see cref="IScenesManager.SwitchMainSceneAsync"/> 自动卸载）。</summary>
        public bool IsPersistent { get; }

        /// <summary>等待场景加载完成的 UniTask（含激活）。</summary>
        public UniTask WaitForCompletionAsync() => _loadCompletionSource.Task;

        /// <summary>
        /// 场景加载至 90%（<see cref="SceneState.Ready"/>）但尚未激活时，调用此方法完成最终激活。
        /// 在 <see cref="SceneLoadOptions.ActivateOnLoad"/> = false 时使用。
        /// </summary>
        /// <exception cref="InvalidOperationException">当前状态不是 <see cref="SceneState.Ready"/> 时抛出（fail-fast）。</exception>
        public UniTask ActivateAsync()
        {
            if (State != SceneState.Ready)
            {
                throw new InvalidOperationException(
                    $"[SceneHandle] Cannot activate scene {SceneRef.SceneName}: " +
                    $"expected state Ready but was {State}.");
            }

            _activationSource.TrySetResult();
            return WaitForCompletionAsync();
        }

        /// <summary>将此场景设为 Unity 活跃场景（影响光照设置、导航网格等依赖 ActiveScene 的系统）。Scene 未加载时为 no-op。</summary>
        public void SetAsActiveScene()
        {
            if (Scene.isLoaded)
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(Scene);
            }
        }

        internal UniTaskCompletionSource ActivationSource => _activationSource;

        internal UniTaskCompletionSource LoadCompletionSource => _loadCompletionSource;

        internal SceneHandle(SceneReference sceneRef, bool isPersistent)
        {
            SceneRef = sceneRef;
            IsPersistent = isPersistent;
        }

        /// <inheritdoc />
        public override string ToString() =>
            $"SceneHandle({SceneRef.SceneName}, {State}, Progress={Progress:F2})";
    }
}
