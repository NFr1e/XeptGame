using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace XeptKit.UI.Manager
{
    /// <summary>
    /// 遮挡视图过渡能力（**可选接口**，扩展 <see cref="IFormTransition"/>；能力接口模式第三次复用——
    /// 对齐 ISubscriptionClearable / IUISceneContext）：被新 Form 遮住/恢复可见时播放的过渡
    /// （如移动端 push 的旧页让位/复位）。
    /// 由**被遮 Form 自己的转场组件**实现——实现则遮挡时动画（push 让位），不实现则视觉不动（模态弹窗盖住背景页）。
    /// 触发：仲裁遮挡翻转时 <see cref="UIForm"/> 探测本接口并调用（fire-and-forget，不联合 await）。
    /// 中断语义：cover/reveal 是状态派生的、必然可翻转——快速翻转（开即关）时取消上一段
    /// （跳至最终状态并返回，不抛），新动画从当前状态继续（转场实现需「从当前状态」语义）。
    /// 与「动画不可取消」立场的关系：enter/exit 仍不可取消；cover/reveal 允许被状态翻转打断（立场精确化）。
    /// </summary>
    public interface IFormCoverTransition : IFormTransition
    {
        /// <summary>被新 Form 遮住时（如左移让位）；返回 = 遮挡位姿完成。</summary>
        UniTask CoverAsync(CanvasGroup target, CancellationToken cancellationToken = default);

        /// <summary>恢复可见时（如右移复位）；返回 = 完全恢复。</summary>
        UniTask RevealAsync(CanvasGroup target, CancellationToken cancellationToken = default);
    }
}
