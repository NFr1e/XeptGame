using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace XeptKit.UI.Manager
{
    /// <summary>
    /// 即时转场（MonoBehaviour 组件，挂载即无动画）：立即进入/离开最终状态。
    /// UIForm 无任何 IFormTransition 组件时行为等同本组件（即时）。
    /// </summary>
    [AddComponentMenu("UI/Form Transitions/InstantFormTransition")]
    public sealed class InstantFormTransition : MonoBehaviour, IFormTransition
    {
        public UniTask EnterAsync(CanvasGroup target, CancellationToken cancellationToken = default)
        {
            target.alpha = 1f;
            return UniTask.CompletedTask;
        }

        public UniTask ExitAsync(CanvasGroup target, CancellationToken cancellationToken = default)
        {
            target.alpha = 0f;
            return UniTask.CompletedTask;
        }
    }
}
