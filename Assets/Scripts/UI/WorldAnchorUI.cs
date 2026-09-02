using UnityEngine;
using XeptKit.Core;

namespace XeptGame.UI
{
    /// <summary>
    /// 锚点放置方向：相对锚点的偏移策略（表现机制内配置，仍不负责任何内容）。
    /// Above/Below 沿世界 up/down（贴附物体物理上下，世界 up ≈ 屏幕 up，FPS 无 roll 下屏幕稳定）；
    /// Left/Right 沿相机 right（每帧随相机旋转刷新，屏幕侧稳定——绕物体走一圈提示始终在屏幕同侧）。
    /// </summary>
    public enum AnchorDirection
    {
        /// <summary>上方（世界 up）。</summary>
        Above,

        /// <summary>下方（世界 down）。</summary>
        Below,

        /// <summary>左方（-相机 right；屏幕侧稳定）。</summary>
        Left,

        /// <summary>右方（+相机 right；屏幕侧稳定）。</summary>
        Right,

        /// <summary>自定义（<c>customOffset</c>）。</summary>
        Custom,
    }

    /// <summary>
    /// 世界锚点投影器（纯表现机制，XeptGame.UI）：把世界锚点投影到屏幕坐标并定位自身 RectTransform。
    /// 不负责任何信息显示——内容由宿主 FormLogic 决定（UI_WorldBillboard_Design.md §1.2 职责边界）；
    /// 显隐决策也在逻辑层，本组件只计算"是否可投影"（<see cref="IsProjecting"/>）供逻辑层消费。
    /// 放置方向（<see cref="AnchorDirection"/> + <see cref="anchorDistance"/>）亦属表现机制配置：
    /// 逻辑层只传锚点（<see cref="SetAnchor(Transform)"/>），偏移策略由本组件解析。
    /// 时序：LateUpdate + DefaultExecutionOrder(1000)，晚于 CameraRig（默认序 0）合成相机之后投影。
    /// Canvas 懒解析：Form 由 UIManager 实例化后才挂到组根（此时父链才有 Canvas），故不在 Awake 解析。
    /// 画布依赖最小化：投影只需"相机 + 锚点 + 父级 RectTransform"；Canvas 仅用于相机空间画布的
    /// 换算相机选择——Overlay 画布（或未找到画布）按 Overlay 模式换算，不依赖 Canvas 组件
    /// （ScreenPointToLocalPointInRectangle 的 Overlay 路径基于 rect.lossyScale）。
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class WorldAnchorUI : MonoBehaviour
    {
        [Tooltip("所属 Canvas（相机空间画布换算用）；为空时自动向上查找（懒解析），未找到按 Overlay 模式换算")]
        [SerializeField] private Canvas canvas;

        [Tooltip("被定位的 RectTransform；为空时使用自身")]
        [SerializeField] private RectTransform selfRect;

        [Header("放置")]
        [Tooltip("锚点放置方向；Custom 使用自定义偏移")]
        [SerializeField] private AnchorDirection anchorDirection = AnchorDirection.Above;

        [Tooltip("方向偏移距离（m）：Above/Below 沿世界 up/down，Left/Right 沿相机 right（屏幕侧稳定）")]
        [SerializeField] private float anchorDistance = 1.2f;

        [Tooltip("Custom 模式的自定义世界偏移")]
        [SerializeField] private Vector3 customOffset = new Vector3(0f, 1.2f, 0f);

        private Camera _camera;
        private Transform _anchor;
        private bool _hasAnchor;
        private bool _projecting;
        private bool _canvasResolved;
        private bool _warnedNoCamera;
        private bool _warnedNoCanvas;
        private bool _warnedNoParent;
        private Vector3 _explicitOffset;
        private bool _useExplicitOffset;   // 本次 SetAnchor 是否使用显式偏移（transient，不触碰序列化配置）

        /// <summary>
        /// 当前是否处于可投影状态（有锚点 && 相机有效 && 锚点在相机前方）。
        /// LateUpdate 计算；逻辑层（如 PickupPromptLogic）每帧消费，作为防御性显隐依据。
        /// </summary>
        public bool IsProjecting => _projecting;

        /// <summary>投影相机（相机对象 Transform，内部缓存 Camera 组件；为空则不投影）。</summary>
        public Transform CameraTransform
        {
            get => _camera != null ? _camera.transform : null;
            set
            {
                _camera = value != null ? value.GetComponent<Camera>() : null;

                if (value == null || _camera == null)
                {
                    WarnNoCamera();
                }
            }
        }

        private void Awake()
        {
            if (selfRect == null)
            {
                selfRect = (RectTransform)transform;
            }
        }

        /// <summary>设置锚点（本次调用使用放置方向配置计算偏移）；每帧 LateUpdate 投影。</summary>
        public void SetAnchor(Transform worldAnchor)
        {
            _anchor = worldAnchor;
            _hasAnchor = true;
            _useExplicitOffset = false;
        }

        /// <summary>
        /// 设置锚点并显式指定世界偏移——**本次调用生效**的 transient override：
        /// 不修改序列化配置（<see cref="anchorDirection"/> / <see cref="customOffset"/>），
        /// 下一次 <see cref="SetAnchor(Transform)"/> 即回到放置方向配置。
        /// 偏移来源由最近一次 SetAnchor 调用决定（调用方意图与持久配置分离）。
        /// </summary>
        public void SetAnchor(Transform worldAnchor, Vector3 worldOffset)
        {
            _anchor = worldAnchor;
            _hasAnchor = true;
            _explicitOffset = worldOffset;
            _useExplicitOffset = true;
        }

        /// <summary>清除锚点（无锚点 = 不可投影；显隐由逻辑层决策）。</summary>
        public void ClearAnchor()
        {
            _hasAnchor = false;
            _anchor = null;
            _useExplicitOffset = false;
        }

        private void LateUpdate()
        {
            if (!_canvasResolved)
            {
                ResolveCanvas();
            }

            // 门控诊断（一次性警告，指出具体缺失项——此前为静默失败，广告牌表现为"保持原始位置"）
            if (_hasAnchor)
            {
                if (_camera == null)
                {
                    WarnNoCamera();
                }

                if (canvas == null)
                {
                    WarnNoCanvas();
                }

                if (selfRect == null || selfRect.parent == null)
                {
                    WarnNoParent();
                }
            }

            _projecting = _hasAnchor && _anchor != null && _camera != null && selfRect != null && selfRect.parent != null;

            if (!_projecting)
            {
                return;
            }

            var screenPos = _camera.WorldToScreenPoint(_anchor.position + ResolveOffset());
            if (screenPos.z < 0f)
            {
                // 锚点在相机背后：不可投影（显隐由逻辑层处理，见 IsProjecting 注释）
                _projecting = false;
                return;
            }

            // 换算相机：Overlay 画布（或未找到画布）传 null；相机空间画布传画布 worldCamera（缺省回退投影相机）
            Camera eventCamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCamera = canvas.worldCamera != null ? canvas.worldCamera : _camera;
            }

            var parentRect = (RectTransform)selfRect.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, eventCamera, out var localPoint))
            {
                // 父级锚点与 pivot 重合（居中/拉伸锚点，uGUI 惯例）时 anchoredPosition == 父级本地坐标
                selfRect.anchoredPosition = localPoint;
            }
            else
            {
                _projecting = false;
            }
        }

        /// <summary>
        /// 计算本次投影的偏移：显式偏移（双参 SetAnchor，transient）优先，
        /// 否则按放置方向解析。Left/Right 依赖相机 right（随相机旋转变化），须每帧计算；
        /// Above/Below 沿世界 up/down（贴附物体物理上下）。仅在有相机时调用（门控已保证）。
        /// </summary>
        private Vector3 ResolveOffset()
        {
            if (_useExplicitOffset)
            {
                return _explicitOffset;
            }

            switch (anchorDirection)
            {
                case AnchorDirection.Above: return Vector3.up * anchorDistance;
                case AnchorDirection.Below: return Vector3.down * anchorDistance;
                case AnchorDirection.Left: return -_camera.transform.right * anchorDistance;
                case AnchorDirection.Right: return _camera.transform.right * anchorDistance;
                default: return customOffset;
            }
        }

        private void ResolveCanvas()
        {
            _canvasResolved = true;

            if (canvas != null)
            {
                return;
            }

            canvas = GetComponentInParent<Canvas>();
        }

        private void WarnNoCamera()
        {
            if (_warnedNoCamera)
            {
                return;
            }
            _warnedNoCamera = true;
            Log.Warning("[WorldAnchorUI] 投影相机未注入或指向的对象无 Camera 组件——" +
                        "检查 PickupPromptHudLoader 的 Camera Transform 接线（应指向 FirstPersonCamera），投影不可用。");
        }

        private void WarnNoCanvas()
        {
            if (_warnedNoCanvas)
            {
                return;
            }
            _warnedNoCanvas = true;
            Log.Warning("[WorldAnchorUI] 未找到所属 Canvas——按 Overlay 模式换算（Overlay 画布无需 Canvas 组件）；" +
                        "若 HUD 画布为相机空间（ScreenSpace-Camera），请确保表单层级位于画布之下。");
        }

        private void WarnNoParent()
        {
            if (_warnedNoParent)
            {
                return;
            }
            _warnedNoParent = true;
            Log.Warning("[WorldAnchorUI] 无父级 RectTransform，无法定位（应挂在 UI 元素上）。");
        }
    }
}
