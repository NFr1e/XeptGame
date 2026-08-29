using UnityEngine;

namespace XeptKit.UI.Manager
{
    /// <summary>
    /// 场景上下文接线能力（**可选接口**，对齐 Event 模块 `ISubscriptionClearable` 的「能力接口」模式）：
    /// 接受 UI 场景上下文（组根挂载父级 + UI 相机）推送。
    /// 与业务契约 <see cref="IUIManager"/> 分离——业务层全程只感知 IUIManager；接线方（<see cref="UIComponent"/> / 组合根）
    /// 经本接口类型指路，不做具体类型依赖；自定义 IUIManager 实现按需选择实现本接口。
    /// </summary>
    public interface IUISceneContext
    {
        /// <summary>设置 UI 场景上下文（可随时调用，覆盖既有值；null 值 = 清除/降级运行）。</summary>
        void SetUIContext(Transform canvasRoot, Camera uiCamera);
    }
}
