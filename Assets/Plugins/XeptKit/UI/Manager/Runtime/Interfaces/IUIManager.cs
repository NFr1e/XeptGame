using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace XeptKit.UI.Manager
{
    /// <summary>
    /// 界面管理服务抽象。打开失败 fail-fast 抛异常（传播）；关闭幂等、不可取消、必完成（失败隔离，经
    /// <see cref="FormException"/> 上报）。主线程 only、无锁。
    /// 取消令牌统一为 System.Threading.CancellationToken（惯例传 <c>KitLifecycle.GlobalToken</c>）。
    /// </summary>
    public interface IUIManager
    {
        /// <summary>全局焦点顶；无则 default。</summary>
        FormHandle CurrentTop { get; }

        // ---- 打开 ----

        /// <summary>
        /// 打开表单。await 完成 = 表单已完全打开（OnOpenAsync + 入场动画完成、OnOpened 已调用）。
        /// 失败抛异常（fail-fast，传播）；取消抛 OperationCanceledException。
        /// <paramref name="args"/> 为打开参数（object，逻辑内强转，经 FormLogicBase.OnOpenAsync 消费）。
        /// </summary>
        UniTask<FormHandle> OpenAsync(
            FormEntry entry,
            object args = null,
            CancellationToken cancellationToken = default);

        // ---- 关闭（幂等、不可取消、必完成）----

        /// <summary>同步发起关闭：请求时移除节点并恢复焦点；失效/重复句柄静默忽略。</summary>
        void Close(FormHandle handle);

        /// <summary>同步发起关闭该 FormEntry 的最顶实例（多例组语义）；无实例时 no-op。</summary>
        void Close(FormEntry entry);

        /// <summary>同步发起关闭全局焦点顶（返回键原语）；无焦点顶时 no-op。</summary>
        void CloseTop();

        /// <summary>等待该实例完整关闭（含出场动画与终结）。不抛业务异常（失败隔离）；幂等。</summary>
        UniTask CloseAsync(FormHandle handle);

        // ---- 查询 ----

        /// <summary>该 FormEntry 的最顶实例句柄；无则 default（经 <see cref="IsValid"/> 判断）。</summary>
        FormHandle GetOpened(FormEntry entry);

        /// <summary>获取该 FormEntry 最顶实例的用户逻辑组件（未打开返回 null）。句柄隔离反对生命周期操作，不反对读逻辑。</summary>
        T GetFormLogic<T>(FormEntry entry) where T : FormLogicBase;

        /// <summary>句柄对应实例是否存活（O(1)；Close 请求时句柄即失效）。</summary>
        bool IsValid(FormHandle handle);

        // ---- 生命周期清理 ----

        /// <summary>幂等：清队列、清追踪、清休眠池、焦点置空。供组合根优雅关闭调用，业务通常不直接调用。</summary>
        void Clear();

        // ---- 异常上报 ----

        /// <summary>可选异常上报钩子：关闭失败 / 通知钩子（OnOpened/OnFocus/OnLoseFocus）异常的出口。</summary>
        event Action<FormEntry, Exception> FormException;
    }
}
