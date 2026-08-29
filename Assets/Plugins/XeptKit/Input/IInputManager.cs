using System;
using UnityEngine.InputSystem;

namespace XeptKit.Input
{
    /// <summary>
    /// 输入分发器抽象：将 <see cref="InputAction"/> 的回调按输入层（<see cref="IInputLayer"/>）优先级
    /// 与阻断语义分发到业务回调。主线程 only、无锁；全同步、无异步 API。
    /// 业务语义事件（如"跳跃键按下"）由业务自行经 Event 模块发布，本模块不代劳。
    /// </summary>
    public interface IInputManager
    {
        // ---- 绑定 / 解绑 ----

        /// <summary>
        /// 绑定输入回调到指定 action 与输入层。返回反注册句柄，Dispose 即解绑（幂等）。
        /// 同一 (action, callback, layerType) 重复绑定仅保留首次。
        /// </summary>
        IDisposable Bind<TLayer>(InputAction action, Action<InputAction.CallbackContext> callback) where TLayer : IInputLayer, new();

        /// <summary>按 (action, callback, layerType) 三元组精确退订。未命中返回 false（no-op）。</summary>
        bool Unbind<TLayer>(InputAction action, Action<InputAction.CallbackContext> callback) where TLayer : IInputLayer, new();

        // ---- 层 ----

        /// <summary>
        /// 返回缓存层实例（每类型唯一，懒创建），供自省/调试。
        /// 注意：struct 层类型经接口装箱缓存，此处返回拆箱拷贝（副本）；class 层类型返回缓存引用。
        /// </summary>
        TLayer GetInputLayer<TLayer>() where TLayer : IInputLayer, new();

        /// <summary>该层是否处于激活状态（默认激活）。纯查询，无副作用。</summary>
        bool IsLayerActive<TLayer>() where TLayer : IInputLayer, new();

        /// <summary>
        /// 设置层激活状态：false 使该层全部已绑定任务失效（分发时跳过，不阻断更低层），
        /// true 恢复。由业务显式驱动（如"打开暂停菜单时暂停 Gameplay 输入"）。
        /// 不要求该层已有任务（先配置后绑定合法）。
        /// </summary>
        void SetLayerActive<TLayer>(bool active) where TLayer : IInputLayer, new();

        // ---- 生命周期清理 ----

        /// <summary>
        /// 幂等。解绑全部已绑定 action 的三事件（started/performed/canceled），
        /// 清空任务映射与层缓存，恢复全部层为激活（"出厂"状态）。
        /// </summary>
        void Clear();

        // ---- 异常上报 ----

        /// <summary>可选异常上报钩子：输入回调抛出的业务异常（每异常触发一次），InputAction 定位动作。</summary>
        event Action<InputAction, Exception> HandlerException;
    }
}
