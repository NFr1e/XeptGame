using System.Collections.Generic;
using UnityEngine;
using XeptKit.Core;

namespace XeptGame.Player
{
    /// <summary>
    /// 相机状态合成器（3C_CameraFeel_Design.md §2）：**唯一写相机 transform 与 FOV 的组件**。
    /// 业务（Look 输入、效果源）只经输入 API 写入，互不知道对方：
    /// <list type="bullet">
    /// <item><see cref="BaseRotation"/>：主视角基准旋转（PlayerLook 独占写；当前决议 =
    /// Euler(Pitch, Yaw, 0)，yaw 在相机 local）；</item>
    /// <item><see cref="AddPositionOffset"/> / <see cref="AddRotationOffset"/>：修饰偏移
    /// （HeadBob/LandingKick 等效果源叠加，本帧应用后自动清零）；</item>
    /// <item><see cref="Fov"/>：FOV 目标接收层（OverrideValue 多优先级；平滑在本组件执行）。</item>
    /// </list>
    /// LateUpdate 合成应用（在移动/输入之后，避免相机抖动）。
    /// 挂载：眼位挂点（CameraRig GO，body 子级，localPosition = 眼位基准）；相机（含 overlay 栈）为其子级。
    /// </summary>
    public sealed class CameraRig : MonoBehaviour, ICameraEffectTarget
    {
        [Tooltip("观感参数资产；为空时使用内置默认值")]
        [SerializeField] private PlayerCameraFeelProfile viewProfile;

        [Tooltip("相机 Transform（眼位基准所在；为空时使用本组件 Transform）")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("FOV 应用目标相机列表（为空时自动收集子级相机；多相机栈场景如 base+overlay 需全部同步）")]
        [SerializeField] private List<Camera> cameras = new();

        private PlayerCameraFeelProfile _profile;
        private Vector3 _baseLocalPosition;   // Awake 捕获的眼位基准（序列化 localPosition）
        private float _currentFov;

        private Vector3 _positionOffset;
        private Quaternion _rotationOffset = Quaternion.identity;

        /// <summary>FOV 目标接收层：DefaultValue = fovIdle，效果源以优先级注入（状态目标 = Effect）。</summary>
        public OverrideValue<float> Fov { get; } = new();

        /// <summary>主视角基准旋转（本地空间；PlayerLook 独占写）。</summary>
        public Quaternion BaseRotation { get; set; } = Quaternion.identity;

        /// <inheritdoc />
        public Vector3 PositionOffset => _positionOffset;

        /// <inheritdoc />
        public Quaternion RotationOffset => _rotationOffset;

        private void Awake()
        {
            _profile = viewProfile != null ? viewProfile : PlayerCameraFeelProfile.Default;

            if (cameraTransform == null)
            {
                cameraTransform = transform;
            }

            _baseLocalPosition = cameraTransform.localPosition;

            Fov.DefaultValue = _profile.fov.fovIdle;
            _currentFov = _profile.fov.fovIdle;

            if (cameras.Count == 0)
            {
                foreach (var cam in GetComponentsInChildren<Camera>(true))
                {
                    cameras.Add(cam);
                }
            }
        }

        private void LateUpdate()
        {
            if (cameraTransform == null)
            {
                return;
            }

            // 合成：基准旋转 × 修饰旋转；眼位基准 + 修饰位置（本地空间）
            cameraTransform.SetLocalPositionAndRotation(
                _baseLocalPosition + _positionOffset,
                BaseRotation * _rotationOffset);

            // FOV：帧率无关指数平滑到目标（OverrideValue 最高优先级）
            if (cameras.Count > 0)
            {
                var smooth = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(_profile.fov.fovSmoothTime, 0.001f));
                _currentFov = Mathf.Lerp(_currentFov, Fov.Value, smooth);

                foreach (var cam in cameras)
                {
                    if (cam != null)
                    {
                        cam.fieldOfView = _currentFov;
                    }
                }
            }

            // 清空偏移：供下一帧效果源重新累积
            _positionOffset = Vector3.zero;
            _rotationOffset = Quaternion.identity;
        }

        /// <inheritdoc />
        public void AddPositionOffset(Vector3 offset) => _positionOffset += offset;

        /// <inheritdoc />
        public void AddRotationOffset(Quaternion offset) => _rotationOffset *= offset;
    }
}
