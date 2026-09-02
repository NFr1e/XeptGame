using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace XeptGame
{
    /// <summary>
    /// 相机栈自报器角色：Auto = 从 UniversalAdditionalCameraData.cameraType
    /// </summary>
    public enum CameraNotifierRole
    {
        Auto,
        Base,
        Overlay,
    }

    /// <summary>
    /// 相机栈自报器（URP，GameplayFlow_Design.md §2.2）：挂每个需要参与跨场景栈仲裁的相机
    /// （Base 相机与 Overlay 相机都挂）。OnEnable 向 AppEntry.CameraManager 注册自身、OnDisable 注销——
    /// 管理器据此把全部存活 Overlay 推入每个 Base 的栈（跨场景天然工作，无序列化跨场景引用）。
    /// <see cref="stackPriority"/>：Overlay 栈内排序（越高越后渲染——UI 相机设高值保证绘制在最上层；
    /// 低值如特效相机先渲染）。角色单一事实源见 <see cref="CameraNotifierRole"/>。
    /// 注意：Overlay 相机的 cullingMask 应只渲染其目标层（如 UI 层），否则会重复绘制世界。
    /// 
    /// 规定:
    /// 0 - 10: 第一人称
    /// 50 - 60: UI
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(UniversalAdditionalCameraData))]
    public sealed class CameraNotifier : MonoBehaviour
    {
        [Tooltip("相机角色：Auto = 从 UniversalAdditionalCameraData.cameraType 读（推荐）；显式值覆盖特殊场景")]
        [SerializeField] private CameraNotifierRole role = CameraNotifierRole.Auto;

        [Tooltip("Overlay 栈内排序优先级（越高越后渲染，UI 相机设高值保证最上层；Base 相机忽略）")]
        [SerializeField] private int stackPriority = 0;

        private Camera _camera;

        private void OnEnable()
        {
            _camera = GetComponent<Camera>();
            AppEntry.CameraManager?.Register(ResolveRole(_camera), _camera, stackPriority);
        }

        private void OnDisable()
        {
            if (_camera != null)
            {
                AppEntry.CameraManager?.Unregister(_camera);
            }
        }

        private CameraStackRole ResolveRole(Camera camera)
        {
            return role switch
            {
                CameraNotifierRole.Base => CameraStackRole.Base,
                CameraNotifierRole.Overlay => CameraStackRole.Overlay,
                _ => camera.GetUniversalAdditionalCameraData().renderType == CameraRenderType.Overlay
                    ? CameraStackRole.Overlay
                    : CameraStackRole.Base
            };
        }
    }
}
