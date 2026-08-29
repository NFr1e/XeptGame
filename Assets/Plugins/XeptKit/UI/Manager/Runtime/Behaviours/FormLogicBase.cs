using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace XeptKit.UI.Manager
{
    /// <summary>
    /// 表单逻辑插件点（用户继承并挂到面板 Prefab 根节点，与框架自有的 <see cref="UIForm"/> 同物体）。
    /// 视图机制（动画/激活/层级应用/输入封锁/终结）由 UIForm 承担，本类型只承载业务逻辑。
    /// 异步钩子默认已完成，实现者只重写需要的；新增钩子对既有实现零破坏（抽象类 + 异步默认）。
    /// 可选：Prefab 无 FormLogicBase 时允许纯视图表单（框架内部判空跳过逻辑钩子）。
    /// </summary>
    public abstract class FormLogicBase : MonoBehaviour
    {
        /// <summary>本表单的视图（框架注入，与 Manager 同点）。会话身份经 <see cref="UIForm.Handle"/> 触达（身份单源在视图）。</summary>
        public UIForm View { get; internal set; }

        /// <summary>
        /// 管理器引用（框架注入）。关闭意图由业务自实现：<c>Manager.Close(View.Handle)</c>（决策与执行仍在管理器，
        /// 幂等）。框架不提供 RequestClose 便捷方法——视图纯被动（无意图 API），关闭是业务行为。
        /// </summary>
        public IUIManager Manager { get; set; }

        /// <summary>
        /// 逻辑准备（取数据等），在视图入场前调用。<paramref name="args"/> 为打开参数（object，此处强转）。
        /// 取消令牌与中止开启/取消同源：Close 于 Opening 时本方法被取消（OCE 自然展开，清理写在 catch/finally）。
        /// </summary>
        public virtual UniTask OnOpenAsync(FormHandle handle, object args, CancellationToken cancellationToken)
            => UniTask.CompletedTask;

        /// <summary>逻辑收尾（保存状态），在出场动画前调用。不接受取消（关闭不可撤销）。</summary>
        public virtual UniTask OnCloseAsync() => UniTask.CompletedTask;

        /// <summary>视图已完全打开、可交互。</summary>
        public virtual void OnOpened() { }

        /// <summary>关闭流程完成（销毁/休眠前）。</summary>
        public virtual void OnClosed() { }

        /// <summary>获得焦点（仲裁结果通知，与生命周期正交——入场中亦可能获得焦点）。</summary>
        public virtual void OnFocus() { }

        /// <summary>失去焦点（焦点链变化信号；遮挡另由 <see cref="OnCover"/> 表达）。</summary>
        public virtual void OnLoseFocus() { }

        /// <summary>被视觉遮挡（视觉序中有表单在其上）时调用；逻辑可继续运行。</summary>
        public virtual void OnCover() { }

        /// <summary>恢复可见（不再被遮挡）时调用。</summary>
        public virtual void OnReveal() { }

        /// <summary>被遮挡且组配置 <c>PauseWhenCovered</c> 时调用：自停后台逻辑（计时器/轮询/动画）。</summary>
        public virtual void OnPause() { }

        /// <summary>恢复后台逻辑时调用。</summary>
        public virtual void OnResume() { }
    }
}
