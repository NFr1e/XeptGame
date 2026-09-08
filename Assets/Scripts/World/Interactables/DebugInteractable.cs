using System.Collections.Generic;
using UnityEngine;
using XeptGame.Interaction;
using XeptKit.Core;

namespace XeptGame.World.Interactables
{
    /// <summary>
    /// 调试用交互对照物（v3.1 宿主模型）：同时是**宿主（ISelectable + IInteractionActionsHost）**，
    /// 唯一动作 = 内部纯 C# <see cref="DebugAction"/>（Primary"交互"，构造注入宿主）。覆盖验证面：
    /// CanSelect 门控（maxInteractions 耗尽不可选）/ 动作冷却门控（cooldown 期间灰态/按下重判）/ 选中高亮。
    /// </summary>
    public sealed class DebugInteractable : MonoBehaviour, ISelectable, IInteractionActionsHost
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

        private readonly List<IInteractionAction> _actions = new();
        private int _interactCount;
        private bool _isSelected;
        private float _cooldownEndTime;
        private Color _defaultColor;
        private bool _hasHighlight;

        /// <summary>已交互次数。</summary>
        public int InteractCount => _interactCount;

        /// <inheritdoc />
        public bool IsSelected => _isSelected;

        /// <inheritdoc />
        public IReadOnlyList<IInteractionAction> Actions => _actions;

        /// <summary>当前是否处于交互冷却期（动作门控数据源）。</summary>
        public bool IsCoolingDown => Time.time < _cooldownEndTime;

        private void Awake()
        {
            _actions.Add(new DebugAction(this));

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
        public void OnSelected(InteractionContext context)
        {
            _isSelected = true;
            if (_hasHighlight)
            {
                highlightRenderer.material.color = highlightColor;
            }
        }

        /// <inheritdoc />
        public void OnDeselected()
        {
            _isSelected = false;
            if (_hasHighlight)
            {
                highlightRenderer.material.color = _defaultColor;
            }
        }

        /// <summary>执行交互（动作 Interact 收口）。</summary>
        public bool TryInteract()
        {
            if (IsCoolingDown)
            {
                return false;
            }

            _interactCount++;
            _cooldownEndTime = Time.time + cooldown;

            var remaining = maxInteractions < 0 ? "∞" : (maxInteractions - _interactCount).ToString();
            Log.Info($"[DebugInteractable] {name} 第 {_interactCount} 次交互（剩余 {remaining}）");
            return true;
        }

        /// <summary>交互动作（纯 C#，构造注入宿主）。</summary>
        private sealed class DebugAction : IInteractionAction
        {
            private readonly DebugInteractable _host;

            public DebugAction(DebugInteractable host)
            {
                _host = host;
            }

            public InputSlot Slot => InputSlot.Primary;

            public string PromptText => "交互";

            public bool CanInteract(InteractionContext context) => !_host.IsCoolingDown;

            public void Interact(InteractionContext context) => _host.TryInteract();
        }
    }
}
