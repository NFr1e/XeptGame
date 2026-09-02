using UnityEngine;
using XeptGame.Interaction;

namespace XeptGame.UI
{
    /// <summary>
    /// 拾取提示打开参数（UI_WorldBillboard_Design.md §3.1）：跨场景（UI 场景 Form ↔ 游戏场景选中者/执行者/相机）
    /// 显式注入，不做魔法查找。由 PickupPromptHudLoader（游戏流程装配器）构造，经 UIManager.OpenAsync args 传入，
    /// PickupPromptLogic.OnOpenAsync 消费。
    /// 契约模型 v2：Prompt 面向接口——选中事件源（<see cref="ISelector"/>）+ 可交互者状态
    /// （<see cref="IInteractionExecutor"/>）+ 交互者（构造 InteractionContext 用）。
    /// </summary>
    public sealed class PickupPromptArgs
    {
        /// <summary>选中者（选中事件源；InteractionDetector 实现）。</summary>
        public ISelector Selector;

        /// <summary>交互执行者（可交互者状态：内容/灰态数据源）。</summary>
        public IInteractionExecutor Executor;

        /// <summary>交互者（玩家对象 Transform，构造 <see cref="InteractionContext"/> 用——CanInteract 查询）。</summary>
        public Transform Interactor;

        /// <summary>投影相机（CameraRig 合成的 FirstPersonCamera 对象 Transform）。</summary>
        public Transform CameraTransform;
    }
}
