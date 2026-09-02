using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XeptKit.Core;
using XeptKit.UI.Manager;
using XeptGame.Interaction;
using XeptGame.UI;

namespace XeptGame.Gameplay
{
    /// <summary>
    /// 游戏流程装配器（GameplayFlow_Design.md §2.3，Gameplay 场景组内持久场景的组合根）：
    /// 持有场景内依赖（Inspector 接线）并完成**场景激活自举**——
    /// 打开 Prompt 表单（构造 <see cref="PickupPromptArgs"/>）——**业务模块自生命周期初始化**
    /// （不进加载门控，失败不阻塞玩法；GameplayFlow_Design.md §4.2）。
    /// 收编原 <c>PickupPromptLauncher</c>（开表单 = 场景自举；上下文注入 = 本装配器持有）。
    /// 注：**不订阅流程事件**（GameplayStateChangedEvent 等）——表单随场景激活开/随场景失活关，
    /// 显隐由表单逻辑内部驱动（ISelector 选中事件），无需 Playing 进入/离开联动。
    /// </summary>
    public sealed class PickupPromptHudLoader : MonoBehaviour
    {
        [Tooltip("交互探测器（实现 ISelector：选中事件源；其 Transform 亦为交互者，与探测器同源）")]
        [SerializeField] private InteractionDetector detector;

        [Tooltip("交互执行器（实现 IInteractionExecutor：可交互者状态数据源）")]
        [SerializeField] private InteractionExecutor executor;

        [Tooltip("投影相机（CameraRig 合成的 FirstPersonCamera 对象）")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("拾取提示 FormEntry（UI 场景装配：HUD 组 + PickupPromptForm Prefab）")]
        [SerializeField] private FormEntry promptFormEntry;

        private CancellationTokenSource _openCts;
        private FormHandle _promptHandle;
        private bool _promptOpened;

        private void OnEnable()
        {
            if (detector == null || executor == null || cameraTransform == null || promptFormEntry == null)
            {
                Log.Error("[PickupPromptHudLoader] detector / executor / cameraTransform / promptFormEntry 未接线，拾取提示不可用。");
            }

            // 场景激活自举：开 Prompt 表单（业务模块自生命周期初始化，不进加载门控）
            _openCts?.Dispose();
            _openCts = CancellationTokenSource.CreateLinkedTokenSource(KitLifecycle.GlobalToken);
            _ = OpenPromptAsync(_openCts.Token);
        }

        private void OnDisable()
        {
            _openCts?.Cancel();
            _openCts?.Dispose();
            _openCts = null;

            if (_promptOpened)
            {
                AppEntry.UIManager.Close(_promptHandle); // 幂等
                _promptOpened = false;
            }
        }

        /// <summary>
        /// 打开 Prompt 表单（异步，业务模块场景副作用）：失败仅记日志——辅助 HUD 不阻塞玩法流程
        /// （模块自生命周期初始化，不进加载门控，GameplayFlow_Design.md §4.2）。
        /// </summary>
        private async UniTaskVoid OpenPromptAsync(CancellationToken token)
        {
            try
            {
                var args = new PickupPromptArgs
                {
                    Selector = detector,          // ISelector 接口注入
                    Executor = executor,          // IInteractionExecutor 接口注入
                    Interactor = detector != null ? detector.transform : null, // 交互者 = 探测器所在 Player
                    CameraTransform = cameraTransform,
                };

                _promptHandle = await AppEntry.UIManager.OpenAsync(promptFormEntry, args, token);
                _promptOpened = true;
            }
            catch (OperationCanceledException)
            {
                // 场景卸载/会话结束取消——正常流程，静默
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
    }
}
