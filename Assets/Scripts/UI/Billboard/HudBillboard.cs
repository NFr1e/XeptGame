using System;
using UnityEngine;
using XeptKit.Core;
using XeptGame.Core;

namespace XeptGame.UI.Billboard
{
    /// <summary>
    /// 基于 UGUI 的 HUD 广告牌（效果器实现二——表现层重组决议）：根据世界空间锚点坐标，经投影相机换算
    /// 屏幕坐标，再转换为目标父级本地坐标，对目标 RectTransform 设置 anchoredPosition。
    /// **配置面（最小）**：目标 RectTransform（空 = 自身）+ 钳制 RectTransform（空 = 目标父级）+
    /// 投影相机注入 + 出域策略（可插拔，空 = 默认 <see cref="RectEdgeClamp"/> 矩形保界）。
    /// **Canvas 不做配置**：换算相机运行期自解析——懒取目标所属 Canvas（Overlay 画布 → 换算相机 null；
    /// 相机空间画布 → canvas.worldCamera，缺省回退投影相机）。
    /// **核心职责**：投影 + 前/后判定（委托 <see cref="CameraFrustum"/>）+ 位置写入 + 状态输出——
    /// 出域放置的具体边界几何（矩形保界 / 内切椭圆双态指引）归**出域策略**（<see cref="IHudBillboardClamp"/>，
    /// 策略面可插拔不写死）。相机背后一律不走投影（背后点透视除以负 z，逼近正后方时数值塌缩）——
    /// 方向取锚点在相机本地系的横向 (x: 右, y: 上)，保持真实侧向贴边；正后方（横向近零）落底部中心。
    /// **状态/方向输出（业务消费）**：
    /// <list type="bullet">
    /// <item><see cref="IsProjecting"/> = 有可放置位置；<see cref="IsClamped"/> = 出域（椭圆外/出视口/背后）——
    /// 业务据此切 marker/arrow；</item>
    /// <item><see cref="Direction"/> = 出域时的单位方向（UI 本地坐标，y 上）——业务旋转箭头（椭圆策略下
    /// 位置沿椭圆线连续滑动，方向连续）。</item>
    /// </list>
    /// 不负责任何信息显示/显隐决策——内容与显隐由上层业务模块决定。
    /// 时序：LateUpdate + DefaultExecutionOrder(1000)，晚于相机合成（CameraRig 默认序 0）之后投影。
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class HudBillboard : MonoBehaviour, IBillboard
    {
        [Tooltip("被定位的目标 RectTransform；为空时使用自身")]
        [SerializeField] private RectTransform rectTransform;

        [Tooltip("钳制边界容器（出域策略的边界基准，可为目标所在容器或更上层画布区域）；为空 = 目标父级")]
        [SerializeField] private RectTransform clampRect;

        [Tooltip("出域放置策略（可插拔）：空 = RectEdgeClamp（矩形方向性保界）；EllipseGuidance = 内切椭圆双态指引（任务指引箭头）")]
        [SerializeReference] private IHudBillboardClamp clamp;

        private Camera _camera;
        private Transform _anchor;

        private IHudBillboardClamp _clamp;  // 运行期解析（clamp 为空 → 默认矩形保界）
        private Canvas _canvas;             // 换算相机来源（懒解析目标所属 Canvas；仅相机空间画布需要其 worldCamera）
        private bool _hasAnchor;
        private bool _projecting;
        private bool _clamped;
        private Vector2 _direction;
        private bool _canvasResolved;
        private bool _warnedNoCamera;
        private bool _warnedNoCanvas;
        private bool _warnedNoParent;

        /// <summary>
        /// 当前是否处于可投影状态（有锚点 && 相机有效 && 计算出可放置位置——界内或出域边界）。
        /// LateUpdate 计算；上层业务（显隐决策方）每帧消费。
        /// </summary>
        public bool IsProjecting => _projecting;

        /// <summary>本次放置是否出域（椭圆/矩形界外、出视口或在相机背后——位置在边界而非锚点真实投影）。业务据此切 marker/arrow。</summary>
        public bool IsClamped => _clamped;

        /// <summary>
        /// 出域时的方向（单位向量，目标父级本地坐标，y 上；界内恒为零向量）——
        /// 业务旋转箭头用（约定：箭头贴片默认朝上，旋转角 = Atan2(dx, dy)）。位置与方向在出域时一致指向目标。
        /// </summary>
        public Vector2 Direction => _direction;

        /// <summary>出域策略（运行期可换；置 null 回退默认矩形保界）。</summary>
        public IHudBillboardClamp ClampStrategy
        {
            get => _clamp;
            set => _clamp = value ?? new RectEdgeClamp();
        }

        // ---- IBillboard（锚点语义共同面；位置之外的显隐/状态仍走本类专属 API）----

        /// <inheritdoc />
        public Transform CurrentAnchor => _anchor;

        /// <inheritdoc />
        public event Action<Transform> AnchorChanged;

        /// <inheritdoc />
        public void SetAnchor(Transform worldAnchor)
        {
            _anchor = worldAnchor;
            _hasAnchor = true;
            AnchorChanged?.Invoke(worldAnchor);
        }

        /// <inheritdoc />
        public void ClearAnchor()
        {
            _hasAnchor = false;
            _anchor = null;
            AnchorChanged?.Invoke(null);
        }

        /// <summary>投影相机（相机对象 Transform，内部缓存 Camera 组件；为空则不投影——必填注入）。</summary>
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
            if (rectTransform == null)
            {
                rectTransform = (RectTransform)transform;
            }

            _clamp = clamp ?? new RectEdgeClamp();
        }

        private void LateUpdate()
        {
            if (!_canvasResolved)
            {
                ResolveCanvas();
            }

            // 门控诊断（一次性警告，指出具体缺失项）
            if (_hasAnchor)
            {
                if (_camera == null)
                {
                    WarnNoCamera();
                }

                if (_canvas == null)
                {
                    WarnNoCanvas();
                }

                if (rectTransform == null || rectTransform.parent == null)
                {
                    WarnNoParent();
                }
            }

            _projecting = _hasAnchor && _anchor != null && _camera != null && rectTransform != null && rectTransform.parent != null;

            if (!_projecting)
            {
                _clamped = false;
                _direction = Vector2.zero;
                return;
            }

            var placeParent = (RectTransform)rectTransform.parent;

            // ---- 钳制边界（目标父级本地坐标；元素 inset 后整体保留在钳制容器内）----
            // 元素 rect 与父级同单位（uGUI 常规层级 scale ≈ 1）；钳制容器可为目标父级或任意上层区域
            var container = clampRect != null ? clampRect : placeParent;
            Vector2 cMin;
            Vector2 cMax;
            if (container == placeParent)
            {
                cMin = container.rect.min;
                cMax = container.rect.max;
            }
            else
            {
                // 任意钳制容器：rect 四角 → 世界 → 目标父级本地，取 AABB（旋转容器取外接盒，近似）
                var r = container.rect;
                var p0 = placeParent.InverseTransformPoint(container.TransformPoint(new Vector3(r.xMin, r.yMin, 0f)));
                var p1 = placeParent.InverseTransformPoint(container.TransformPoint(new Vector3(r.xMax, r.yMin, 0f)));
                var p2 = placeParent.InverseTransformPoint(container.TransformPoint(new Vector3(r.xMin, r.yMax, 0f)));
                var p3 = placeParent.InverseTransformPoint(container.TransformPoint(new Vector3(r.xMax, r.yMax, 0f)));
                cMin = Vector2.Min(Vector2.Min(p0, p1), Vector2.Min(p2, p3));
                cMax = Vector2.Max(Vector2.Max(p0, p1), Vector2.Max(p2, p3));
            }

            // 元素 inset（pivot 通用公式：localPoint ∈ [容器.min - 元素.rect.min, 容器.max - 元素.rect.max]）
            var elemMin = rectTransform.rect.min;
            var elemMax = rectTransform.rect.max;
            float minX = cMin.x - elemMin.x;
            float maxX = cMax.x - elemMax.x;
            float minY = cMin.y - elemMin.y;
            float maxY = cMax.y - elemMax.y;

            // 元素比容器大：允许范围失效 → 收敛到容器中心
            if (maxX < minX)
            {
                minX = maxX = (cMin.x + cMax.x) * 0.5f;
            }
            if (maxY < minY)
            {
                minY = maxY = (cMin.y + cMax.y) * 0.5f;
            }

            var area = new HudClampArea(minX, maxX, minY, maxY);
            var center = area.Center;

            // 前/后判定委托 CameraFrustum.IsInFront（视锥判定的单一判据来源，见 XeptGame.World.CameraFrustum）
            bool behind = !CameraFrustum.IsInFront(_camera, _anchor.position);

            Vector2 target;
            bool clamped;
            Vector2 direction = Vector2.zero;

            if (!behind)
            {
                // 面前：正常投影（换算相机：Overlay 画布传 null；相机空间画布传 worldCamera，缺省回退投影相机）
                Camera eventCamera = null;
                if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    eventCamera = _canvas.worldCamera != null ? _canvas.worldCamera : _camera;
                }

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        placeParent, _camera.WorldToScreenPoint(_anchor.position), eventCamera, out var localPoint))
                {
                    _projecting = false;
                    _clamped = false;
                    _direction = Vector2.zero;
                    return;
                }

                if (_clamp.IsInside(localPoint, area))
                {
                    // 界内（策略判定：矩形内 / 椭圆内）→ 真实投影（marker 态）
                    target = localPoint;
                    clamped = false;
                }
                else
                {
                    // 出域 → 沿"容器中心 → 投影点"方向放到边界（策略：矩形边框 / 椭圆线），方向输出供箭头旋转
                    var dir = localPoint - center;
                    if (dir.sqrMagnitude < 1e-6f)
                    {
                        dir = Vector2.down; // 兜底（出界必有方向，理论不发生）
                    }

                    direction = dir.normalized;
                    target = _clamp.ClampToBoundary(center, direction, area);
                    clamped = true;
                }
            }
            else
            {
                // 背后：**不走投影**。方向 = 锚点相机本地横向 (x, y)——保持真实侧向（背后偏左 → 左缘），
                // 沿椭圆/矩形边界连续贴边；正后方（横向近零、方向退化）→ 底部中心（"在身后"提示）。
                // 本地坐标仅此处取方向需要（前/后判定已在上方委托 CameraFrustum）
                var local = _camera.transform.InverseTransformPoint(_anchor.position);
                var dir = new Vector2(local.x, local.y);
                if (dir.sqrMagnitude < 1e-6f)
                {
                    dir = Vector2.down;
                }
                else
                {
                    dir.Normalize();
                }

                direction = dir;
                target = _clamp.ClampToBoundary(center, dir, area);
                clamped = true;
            }

            _clamped = clamped;
            _direction = direction;
            _projecting = true;

            // 父级锚点与 pivot 重合（居中/拉伸锚点，uGUI 惯例）时 anchoredPosition == 父级本地坐标
            rectTransform.anchoredPosition = target;
        }

        private void ResolveCanvas()
        {
            _canvasResolved = true;

            if (rectTransform != null)
            {
                _canvas = rectTransform.GetComponentInParent<Canvas>();
            }
        }

        private void WarnNoCamera()
        {
            if (_warnedNoCamera)
            {
                return;
            }
            _warnedNoCamera = true;
            Log.Warning("[HudBillboard] 投影相机未注入或指向的对象无 Camera 组件——投影不可用（相机为必填注入）。");
        }

        private void WarnNoCanvas()
        {
            if (_warnedNoCanvas)
            {
                return;
            }
            _warnedNoCanvas = true;
            Log.Warning("[HudBillboard] 目标不在任何 Canvas 之下（无法渲染）——换算按 Overlay 处理。");
        }

        private void WarnNoParent()
        {
            if (_warnedNoParent)
            {
                return;
            }
            _warnedNoParent = true;
            Log.Warning("[HudBillboard] 目标无父级 RectTransform，无法定位（应挂在 UI 元素上）。");
        }
    }
}
