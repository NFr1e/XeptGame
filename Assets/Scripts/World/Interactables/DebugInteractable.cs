using System.Collections.Generic;
using UnityEngine;
using XeptGame.Interaction;
using XeptGame.Interaction.Behaviours;
using XeptKit.Core;

namespace XeptGame.World.Interactables
{
    /// <summary>
    /// 调试用交互对照物（v3.1 宿主模型 + v5 目标端口）：同时是**可交互目标（<see cref="IDebugTarget"/>）**与
    /// **宿主（ISelectable + IInteractionActionsHost）**；动作从总名单筛出（当前只有"交互"一件，成员恒定，
    /// 故不实现成员变化通知）。覆盖验证面：CanSelect 门控（maxInteractions 耗尽不可选）/
    /// 动作冷却门控（cooldown 期间灰态、按下重判）/ 选中高亮。
    /// </summary>
    public sealed class DebugInteractable : MonoBehaviour, ISelectable, IInteractionActionsHost, IDebugTarget
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
            for (int i = 0; i < InteractionBehaviourCatalog.All.Count; i++)
            {
                var behaviour = InteractionBehaviourCatalog.All[i];
                if (behaviour.CanBuildOn(this, null))
                {
                    _actions.Add(new BoundAction(behaviour, this));
                }
            }

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

        /// <inheritdoc />
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
    }
}
