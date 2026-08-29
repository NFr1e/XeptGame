using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace XeptKit.UI.Manager
{
    /// <summary>
    /// Form 转场动画抽象：入场/出场视觉过渡（与 Scenes 的 ISceneTransition 同家族）。
    /// **实现类为 MonoBehaviour 组件**（挂于表单根节点，Inspector 配置参数如时长）——UIForm 经序列化引用
    /// <c>formTransition</c> 解析；未配置转场时 UIForm 按即时行为处理。
    /// 实现类应无状态（可多次调用），并自行处理起始/终止视觉状态（框架不干预 CanvasGroup）。
    /// token 语义：**取消时立即进入最终状态并返回（不抛）**——
    /// 供「中止开启」截断入场使用；正常流程（打开/关闭）从不取消，动画完整执行（对齐「动画不可取消」立场）。
    /// </summary>
    public interface IFormTransition
    {
        /// <summary>入场；返回 = 完全可见。</summary>
        UniTask EnterAsync(CanvasGroup target, CancellationToken cancellationToken = default);

        /// <summary>出场；返回 = 视觉终结（不自毁——终结由管理器 Terminate 执行）。</summary>
        UniTask ExitAsync(CanvasGroup target, CancellationToken cancellationToken = default);
    }
}
