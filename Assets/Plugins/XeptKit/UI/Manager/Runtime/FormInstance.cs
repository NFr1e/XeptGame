using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace XeptKit.UI.Manager
{
    /// <summary>内部表单实例：视图 + 逻辑（可 null）+ 生命周期状态 + 令牌与计时状态。</summary>
    internal sealed class FormInstance
    {
        /// <summary>表单层生命周期状态（焦点正交，不在此表达）。</summary>
        public enum LifecycleState
        {
            /// <summary>入场中（OnOpenAsync / EnterAsync 进行中）。</summary>
            Opening,

            /// <summary>完全打开、可交互。</summary>
            Opened,

            /// <summary>离场中（视觉残留，不占栈位——Close 请求时已出链）。</summary>
            Closing,

            /// <summary>终结（销毁或休眠）。</summary>
            Closed,
        }

        /// <summary>本会话句柄（每次打开会话独立身份——休眠复用重开时重新分配新 ID，旧句柄永久失效）。</summary>
        public FormHandle Handle { get; internal set; }
        public readonly UIForm View;
        public readonly FormLogicBase Logic;   // 可 null（纯视图表单）
        public readonly UIGroup Group;

        public LifecycleState State = LifecycleState.Opening;

        /// <summary>是否被视觉遮挡（仲裁派生结果缓存，用于通知去重——非权威存储，每次仲裁重算）。</summary>
        public bool Covered;

        /// <summary>链表节点（AddToChain 时设置、RemoveFromChain 置空）——O(1) 移除依据。</summary>
        public LinkedListNode<FormInstance> Node;

        /// <summary>
        /// 打开操作令牌源（链接调用方令牌）。中止开启/清场时经 <see cref="CancelOpen"/> 触发 OCE；
        /// 释放由打开流程的 finally 负责（本类型只取消、不释放）。
        /// </summary>
        public CancellationTokenSource OpenCts;

        /// <summary>本会话打开完成信号（单例 Opening 时后续 OpenAsync join 等待用；成功/失败/取消各路径完成，会话结束清空）。</summary>
        public UniTaskCompletionSource<FormHandle> OpenCompletion;

        /// <summary>本会话关闭完成信号（Closing 期间后续 CloseAsync join 等待用；关闭终结后完成，随后清空）。</summary>
        public UniTaskCompletionSource CloseCompletion;

        /// <summary>自动关闭计时令牌源；释放由自动关闭流程的 finally 负责（本类型只取消、不释放）。</summary>
        public CancellationTokenSource AutoCloseCts;

        public FormInstance(FormHandle handle, UIForm view, FormLogicBase logic, UIGroup group)
        {
            Handle = handle;
            View = view;
            Logic = logic;
            Group = group;
        }

        /// <summary>仅取消打开令牌（不释放——释放归打开流程 finally）。</summary>
        public void CancelOpen() => OpenCts?.Cancel();

        /// <summary>仅取消自动关闭计时（不释放——释放归自动关闭流程 finally）。</summary>
        public void CancelAutoClose() => AutoCloseCts?.Cancel();
    }
}
