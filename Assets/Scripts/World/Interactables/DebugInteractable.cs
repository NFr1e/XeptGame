using UnityEngine;
using XeptGame.Interaction;

namespace XeptGame.World.Interactables
{
    /// <summary>
    /// 调试用可交互对象（验证示例）：覆盖两阶段门控与选中事件的完整验证面——
    /// <list type="bullet">
    /// <item><b>CanSelect 门控（目标性）</b>：<see cref="maxInteractions"/> 耗尽后不可再选（目标性动态失效）；</item>
    /// <item><b>CanInteract 门控（可交互性）</b>：<see cref="cooldown"/> 冷却期不可交互（可交互性动态失效，
    /// 供 Executor 按下重判与提示层灰态翻转验证）；</item>
    /// <item><b>选中事件视觉反馈</b>：<see cref="OnSelected"/>/<see cref="OnDeselected"/> 切换
    /// 渲染器材质高亮（Inspector 指定，可选；留空则仅日志）。</item>
    /// </list>
    /// 挂带 Collider 的对象（或父级）；日志走 XeptKit.Core.Log。首版无 UI，表现以日志 + 高亮为主。
    /// </summary>
    public sealed class DebugInteractable : MonoBehaviour, ISelectable, IInteractable, IInteractionLabel
    {
        [Header("CanSelect 门控（目标性：耗尽后不可选）")]
        [Tooltip("最大交互次数；< 0 = 不限")]
        [SerializeField] private int maxInteractions = -1;

        [Header("CanInteract 门控（可交互性：冷却期不可交互）")]
        [Tooltip("每次交互后的冷却时间（s）；0 = 无冷却")]
        [SerializeField] private float cooldown = 0f;

        [Header("选中事件视觉反馈（可选）")]
        [Tooltip("选中时高亮的渲染器；为空则仅日志")]
        [SerializeField] private Renderer highlightRenderer;

        [Tooltip("选中高亮颜色")]
        [SerializeField] private Color highlightColor = Color.yellow;

        [Header("显示信息（IInteractionLabel，拾取提示用）")]
        [Tooltip("显示名；为空回退 GameObject 名")]
        [SerializeField] private string displayName = "";

        [Tooltip("提示文案；为空回退默认文案")]
        [SerializeField] private string hintText = "";

        private int _interactCount;
        private bool _isSelected;
        private float _cooldownEndTime;
        private Color _defaultColor;
        private bool _hasHighlight;

        /// <summary>已交互次数。</summary>
        public int InteractCount => _interactCount;

        /// <inheritdoc />
        public bool IsSelected => _isSelected;

        /// <summary>当前是否处于交互冷却期。</summary>
        public bool IsCoolingDown => Time.time < _cooldownEndTime;

        /// <inheritdoc />
        public string DisplayName => displayName;

        /// <inheritdoc />
        public string HintText => hintText;

        private void Awake()
        {
            if (highlightRenderer != null)
            {
                _hasHighlight = true;
                _defaultColor = highlightRenderer.sharedMaterial != null
                    ? highlightRenderer.sharedMaterial.color
                    : Color.white;
            }
        }

        private void OnDisable()
        {
            // 退场复位高亮（仅当仍处于选中态；材质此时可能已被实例化）
            if (_hasHighlight && _isSelected)
            {
                highlightRenderer.material.color = _defaultColor;
                _isSelected = false;
            }
        }

        /// <inheritdoc />
        public bool CanSelect(InteractionContext context)
            => maxInteractions < 0 || _interactCount < maxInteractions;

        /// <inheritdoc />
        public bool CanInteract(InteractionContext context)
            => !IsCoolingDown;

        /// <inheritdoc />
        public void Interact(InteractionContext context)
        {
            _interactCount++;
            _cooldownEndTime = Time.time + cooldown;

            var remaining = maxInteractions < 0 ? "∞" : (maxInteractions - _interactCount).ToString();
            //Log.Info($"[DebugInteractable] {name} 第 {_interactCount} 次交互（剩余 {remaining}）");
        }

        /// <inheritdoc />
        public void OnSelected(InteractionContext context)
        {
            _isSelected = true;
            if (_hasHighlight)
            {
                highlightRenderer.material.color = highlightColor;
            }
            //Log.Info($"[DebugInteractable] {name} 被选中（交互者 {context.Interactor.name}）");
        }

        /// <inheritdoc />
        public void OnDeselected()
        {
            _isSelected = false;
            if (_hasHighlight)
            {
                highlightRenderer.material.color = _defaultColor;
            }
            //Log.Info($"[DebugInteractable] {name} 取消选中");
        }
    }
}
