using UnityEngine;
using XeptGame.Interaction;

namespace XeptGame.UI
{
    /// <summary>
    /// Prompt 运行上下文（开放参数注入）：基座宿主（InteractPromptModule）打开 HUD Form 时经
    /// <c>IUIManager.OpenAsync(entry, args)</c> 传入，FormLogic（InteractPromptHudFormLogic）在 OnOpenAsync
    /// 强转消费——执行器/相机来自场景显式装配，无场景查找（显式装配纪律）。
    /// </summary>
    public sealed class PromptRuntimeContext
    {
        /// <summary>交互执行器（宿主/动作集事件源）。</summary>
        public IInteractionExecutor Executor { get; }

        /// <summary>投影相机（HudBillboard 必填注入；世界相机 Transform）。</summary>
        public Transform CameraTransform { get; }

        public PromptRuntimeContext(IInteractionExecutor executor, Transform cameraTransform)
        {
            Executor = executor;
            CameraTransform = cameraTransform;
        }
    }
}
