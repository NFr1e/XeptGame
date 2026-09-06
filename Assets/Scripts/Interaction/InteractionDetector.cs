using System;
using UnityEngine;
using XeptKit.Core;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互目标探测器（选中系统，引擎壳 MonoBehaviour，实现 <see cref="ISelector"/>，契约模型 v3）：
    /// 每帧经 <see cref="IInteractionProbeSource"/>（默认相机探测源）取得探测射线，
    /// 由 <see cref="InteractionResolver"/> 统一解析最近 ISelectable（单一门控者），
    /// 经 <see cref="InteractionSelection"/>（纯逻辑状态机）维护选中推送。
    /// 目标变化时（单处理器内同步）：
    /// <list type="bullet">
    /// <item>门控者 <c>OnDeselected(旧) → OnSelected(新)</c>（先退场后登场，避免重叠高亮）；</item>
    /// <item>将宿主（新选中，含 null）**直接推给 Executor**（<c>ApplyHost</c>，单写者纪律）——
    /// 动作集由执行器在宿主链上收集（v3，本组件不感知动作细节）；</item>
    /// <item>发布 <see cref="SelectionChanged"/>（负载 <see cref="SelectionChangeArgs"/>）。</item>
    /// </list>
    /// <list type="bullet">
    /// <item><b>引擎壳分工</b>：解析与选中决策在纯 C#（Resolver / Selection），决策逻辑可单测；</item>
    /// <item><b>装配纪律</b>：序列化引用优先、自动定位兜底（相机留空找子物体 Camera）；不做 FindAnyObjectByType；</item>
    /// <item>输出：<see cref="CurrentSelected"/>（单一门控者）、<see cref="LastProbe"/>（Gizmos 调试数据源）。</item>
    /// </list>
    /// 探测源（<see cref="ProbeSource"/>）为变化点：默认相机探测源，可在 Awake 前注入替换（AI/过场）。
    /// </summary>
    public sealed class InteractionDetector : MonoBehaviour, ISelector
    {
        [Tooltip("交互探测参数资产；为空时使用内置默认值")]
        [SerializeField] private InteractionProfile profile;

        [Space(10)]
        [Tooltip("探测起点相机（留空自动查找子物体 Camera；仅用于构建默认探测源）")]
        [SerializeField] private Transform cameraTransform;

        [Space(10)]
        [Tooltip("交互执行器（宿主推送的单写者通道，必接；应指向同一 Player 上的 InteractionExecutor）")]
        [SerializeField] private InteractionExecutor executor;

        private InteractionProfile _profile;
        private IInteractionProbeSource _probeSource;
        private readonly InteractionSelection _selection = new();

        private ISelectable _currentSelected;
        private ProbeInfo _lastProbe;

        /// <inheritdoc cref="ISelector.CurrentSelected"/>
        public ISelectable CurrentSelected => _currentSelected;

        /// <summary>最近一次探测快照（调试可视化：画的是实际判定用的那次探测）。</summary>
        public ProbeInfo LastProbe => _lastProbe;

        /// <summary>当前生效的探测配置（调试读取 maxAngle 等）。</summary>
        public InteractionProfile Profile => _profile;

        /// <summary>交互探测源（变化点）：默认 <see cref="CameraInteractionProbeSource"/>；可在 Awake 前注入替换。</summary>
        public IInteractionProbeSource ProbeSource
        {
            get => _probeSource;
            set => _probeSource = value;
        }

        /// <inheritdoc cref="ISelector.SelectionChanged"/>
        public event Action<SelectionChangeArgs> SelectionChanged;

        private void Awake()
        {
            _profile = profile != null ? profile : InteractionProfile.Default;

            if (_probeSource == null)
            {
                if (cameraTransform == null)
                {
                    var camera = GetComponentInChildren<Camera>(true);
                    if (camera != null)
                    {
                        cameraTransform = camera.transform;
                    }
                }

                if (cameraTransform != null)
                {
                    _probeSource = new CameraInteractionProbeSource(cameraTransform);
                }
                else
                {
                    Log.Error("[InteractionDetector] 未找到相机（应在子物体或 Inspector 指定），交互探测不可用。");
                }
            }

            if (executor == null)
            {
                Log.Error("[InteractionDetector] executor 未接线（应指向同一 Player 上的 InteractionExecutor），无法推送宿主。");
            }

            _selection.Changed += OnSelectionChanged;
        }

        private void Update()
        {
            RefreshTarget();
        }

        private void OnDestroy()
        {
            _selection.Changed -= OnSelectionChanged;
            _selection.Clear();
        }

        private void RefreshTarget()
        {
            _currentSelected = null;

            if (_probeSource == null)
            {
                _lastProbe = default;
                _selection.Apply(null, new InteractionContext(transform));
                return;
            }

            var result = InteractionResolver.Resolve(_probeSource.GetProbeRay(), _profile, transform);

            _lastProbe = result.Probe;
            _currentSelected = result.Target;

            _selection.Apply(_currentSelected, new InteractionContext(transform));
        }

        /// <summary>
        /// 选中变化处理器（单处理器内同步）：宿主推给 Executor（含 null，单写者纪律）→ 转发外部
        /// <see cref="SelectionChanged"/>。推送先于事件：订阅方在事件处理器内读 Executor 状态即一致。
        /// </summary>
        private void OnSelectionChanged(SelectionChangeArgs args)
        {
            if (executor != null)
            {
                executor.ApplyHost(args.To);
            }

            SelectionChanged?.Invoke(args);
        }
    }
}
