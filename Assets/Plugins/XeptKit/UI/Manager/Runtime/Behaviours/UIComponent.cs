using UnityEngine;
using XeptKit.Core;

namespace XeptKit.UI.Manager
{
    /// <summary>
    /// 场景桥接组件（原始架构「桥接组件」§5.3 的落地，XeptFramework UIBridge 借鉴）：
    /// 声明 UI 根节点与 UI 相机，并在场景就绪时**主动推送**给 <see cref="Target"/>（推模式，纯显式接线）。
    /// 挂载于 UI 根节点时 <see cref="UIRoot"/> 自动取自身 Transform；<see cref="UICamera"/> 未指定时按 Tag "UICamera" 自动查找。
    /// </summary>
    /// <remarks>
    /// **推模式（评审决议）**：框架不假设管理器访问方式（本库不设模块门面）——桥的推送目标由
    /// 「持有管理器的一方」（组合根装配）显式指路（<see cref="Target"/>）；为空时不推送（桥仅作声明）。
    /// 管理器侧不做任何场景查找（无 FindObjectOfType/懒解析）。组合时点（场景加载前）不存在场景对象，任何「拉」都无法成立。
    /// <see cref="UIRoot"/> 为 Transform（非 RectTransform）——UI 根是**组织容器**而非布局节点：
    /// 「表单自带 Canvas」模型下容器链纯惰性，普通 Transform 即可；「单根 Canvas 嵌套」模型下传 Canvas（RectTransform）同样兼容。
    /// </remarks>
    [DisallowMultipleComponent]
    public class UIComponent : MonoBehaviour
    {
        private const string UiCameraTag = "UICamera";

        /// <summary>UI 根节点——所有组根节点的父级。挂载于 UI 根节点时自动取自身。</summary>
        [Tooltip("UI 根节点——所有组根节点的父级。挂载于 UI 根节点时自动取自身。")]
        public Transform UIRoot;

        /// <summary>UI 专用摄像机（ScreenSpaceCamera 表单 worldCamera 注入用）。未指定时按 Tag \"UICamera\" 自动查找。</summary>
        [Tooltip("UI 专用摄像机（顶层 ScreenSpaceCamera 表单的 worldCamera 注入用）。未指定时按 Tag \"UICamera\" 查找。")]
        public Camera UICamera;

        private IUISceneContext _sceneCtx;

        /// <summary>推送目标（<see cref="IUISceneContext"/> 类型化——纯显式接线，代码指路；为空时不推送）。</summary>
        public IUISceneContext Target
        {
            get
            {
                return _sceneCtx;
            }
            set
            {
                _sceneCtx = value;
                PushContext();
            }
        }

        private void OnEnable()
        {
            TryContextFallback();
            PushContext();
        }

        private void PushContext() => Target?.SetUIContext(UIRoot, UICamera);
        private void TryContextFallback()
        {
            if (UIRoot == null)
            {
                UIRoot = transform;
            }

            if (UICamera == null)
            {
                var go = GameObject.FindGameObjectWithTag(UiCameraTag);
                if (go != null)
                {
                    UICamera = go.GetComponent<Camera>();
                }
                else
                {
                    Log.Warning(
                        $"[XeptKit.UI.Manager] UIComponent '{name}': 未找到 Tag \"{UiCameraTag}\" 的相机——" +
                        "UICamera 保持为空（相机注入将被跳过，请为相机打 Tag 或在 Inspector 显式赋值）。");
                }
            }
        }
    }
}
