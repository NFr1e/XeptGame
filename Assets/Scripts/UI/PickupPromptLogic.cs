using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using XeptKit.Core;
using XeptKit.UI.Manager;
using XeptGame.Interaction;

namespace XeptGame.UI
{
    /// <summary>
    /// 拾取提示表单逻辑（FormLogicBase 子类，信息显示职责，见 UI_WorldBillboard_Design.md §1.2）：
    /// 订阅 <see cref="ISelector.SelectionChanged"/>（锚点/显隐）→ 驱动 WorldAnchorUI + CanvasGroup 淡入淡出；
    /// 内容/灰态读 <see cref="IInteractionExecutor.CurrentInteractable"/>（IInteractionLabel 契约 + CanInteract）。
    /// 契约模型 v2：**Prompt = 交互物驱动**——选中事件有目标且执行器持有可交互者才显示；
    /// 纯高亮对象（只有 ISelectable）只高亮、不显示提示（UX 边界见 Interaction_Design.md §7.4）。
    /// 常驻打开一次：打开参数 = <see cref="PickupPromptArgs"/>（selector + executor + interactor + camera，
    /// 跨场景显式注入）；选中切换是逻辑内显隐（淡入淡出），不触碰 Form 生命周期。
    /// </summary>
    public sealed class PickupPromptLogic : FormLogicBase
    {
        [Header("投影")]
        [Tooltip("世界锚点投影器（PromptContent 上，纯表现机制）")]
        [SerializeField] private WorldAnchorUI worldAnchorUI;

        [Tooltip("内容根（CanvasGroup 所在：整体显隐与淡入淡出）")]
        [SerializeField] private CanvasGroup contentGroup;

        [Header("内容")]
        [Tooltip("显示名文本（TMP）")]
        [SerializeField] private TextMeshProUGUI displayNameText;

        [Tooltip("提示文案文本（TMP，按键提示）")]
        [SerializeField] private TextMeshProUGUI hintText;

        [Header("配置")]
        [Tooltip("淡入淡出时长（s）")]
        [SerializeField] private float fadeDuration = 0.12f;

        [Tooltip("无 IInteractionLabel 时的默认提示文案")]
        [SerializeField] private string defaultHintText = "按 E 交互";

        [Tooltip("不可交互时的文本颜色（灰态）")]
        [SerializeField] private Color unavailableTextColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        private ISelector _selector;
        private IInteractionExecutor _executor;
        private Transform _interactor;
        private bool _isVisible;
        private float _targetAlpha;
        private bool _wasProjecting;   // 上帧投影状态（防御检查基准，避免选中当帧陈旧值误伤）
        private Color _availableNameColor;
        private Color _availableHintColor;

        /// <inheritdoc />
        public override UniTask OnOpenAsync(FormHandle handle, object args, CancellationToken cancellationToken)
        {
            if (args is PickupPromptArgs promptArgs)
            {
                _selector = promptArgs.Selector;
                _executor = promptArgs.Executor;
                _interactor = promptArgs.Interactor;

                if (worldAnchorUI != null)
                {
                    worldAnchorUI.CameraTransform = promptArgs.CameraTransform;
                }
                else
                {
                    Log.Warning("[PickupPromptLogic] worldAnchorUI 未接线（应为 PromptContent 上的 WorldAnchorUI），提示无法投影。");
                }

                if (_selector == null || _executor == null)
                {
                    Log.Error("[PickupPromptLogic] Selector 或 Executor 未注入（打开参数缺失），拾取提示不可用。");
                }
            }
            else
            {
                Log.Error("[PickupPromptLogic] 打开参数缺失或类型错误（应为 PickupPromptArgs），拾取提示不可用。");
            }

            // 初始隐藏
            _isVisible = false;
            _targetAlpha = 0f;
            if (contentGroup != null)
            {
                contentGroup.alpha = 0f;
                contentGroup.gameObject.SetActive(false);
            }
            else
            {
                Log.Warning("[PickupPromptLogic] contentGroup 未接线（应为 PromptContent 的 CanvasGroup）——" +
                            "内容无法淡入淡出且初始可能直接可见（广告牌表现为保持原始位置）。");
            }

            CacheTextColors();
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public override void OnOpened()
        {
            if (_selector != null)
            {
                _selector.SelectionChanged += OnSelectionChanged;
            }
        }

        /// <inheritdoc />
        public override void OnClosed()
        {
            if (_selector != null)
            {
                _selector.SelectionChanged -= OnSelectionChanged;
            }
        }

        private void Update()
        {
            // 防御：仅当"上帧可投影、本帧失效"时淡出（如锚点意外移出相机背后）。
            // 基准为上帧状态：选中当帧 IsProjecting 尚未更新（投影器 LateUpdate 才计算），
            // 若直读当帧值会在选中帧误判失效、把刚淡入的提示立刻淡出。
            // 常规路径：目标丢失经 SelectionChanged 权威淡出，本检查是兜底。
            if (_isVisible && _wasProjecting && (worldAnchorUI == null || !worldAnchorUI.IsProjecting))
            {
                FadeOut();
            }
            _wasProjecting = worldAnchorUI != null && worldAnchorUI.IsProjecting;

            // 淡入淡出驱动（固定时长线性过渡，帧率无关）
            if (contentGroup != null && Mathf.Abs(contentGroup.alpha - _targetAlpha) > 0.001f)
            {
                var step = fadeDuration > 0f ? Time.deltaTime / fadeDuration : 1f;
                contentGroup.alpha = Mathf.MoveTowards(contentGroup.alpha, _targetAlpha, step);
                if (contentGroup.alpha <= 0.001f && _targetAlpha <= 0f)
                {
                    contentGroup.gameObject.SetActive(false);
                }
            }

            // 态切换：可交互亮 / 不可交互灰（每帧查执行器持有的交互者）
            if (_isVisible)
            {
                ApplyAvailability(IsInteractionAvailable);
            }
        }

        /// <summary>当前是否可执行交互：执行器持有交互者且 CanInteract 通过（交互者经打开参数注入）。</summary>
        private bool IsInteractionAvailable
        {
            get
            {
                var interactable = _executor != null ? _executor.CurrentInteractable : null;
                return interactable != null && _interactor != null
                    && interactable.CanInteract(new InteractionContext(_interactor));
            }
        }

        private void OnSelectionChanged(SelectionChangeArgs args)
        {
            // v2：Prompt = 交互物驱动——推送先于事件（Detector 单处理器内），此处读 Executor 即一致状态
            var interactable = _executor != null ? _executor.CurrentInteractable : null;

            if (args.HasTarget && interactable != null)
            {
                if (worldAnchorUI != null)
                {
                    // 锚点 = 选中对象（契约可挂父级，取 selectable 组件所在对象）；放置方向为投影器配置
                    worldAnchorUI.SetAnchor(((Component)args.To).transform);
                }
                ApplyLabel(interactable);
                FadeIn();
            }
            else
            {
                if (worldAnchorUI != null)
                {
                    worldAnchorUI.ClearAnchor();
                }
                FadeOut();
            }
        }

        /// <summary>内容组装：IInteractionLabel 定制，未实现回退交互物名 + 默认文案（数据与显示分离）。</summary>
        private void ApplyLabel(IInteractable interactable)
        {
            var displayName = interactable is Component component ? component.name : interactable.GetType().Name;
            var hint = defaultHintText;

            if (interactable is IInteractionLabel label)
            {
                if (!string.IsNullOrEmpty(label.DisplayName))
                {
                    displayName = label.DisplayName;
                }
                if (!string.IsNullOrEmpty(label.HintText))
                {
                    hint = label.HintText;
                }
            }

            if (displayNameText != null)
            {
                displayNameText.text = displayName;
            }
            if (hintText != null)
            {
                hintText.text = hint;
            }
        }

        private void ApplyAvailability(bool available)
        {
            if (displayNameText != null)
            {
                displayNameText.color = available ? _availableNameColor : unavailableTextColor;
            }
            if (hintText != null)
            {
                hintText.color = available ? _availableHintColor : unavailableTextColor;
            }
        }

        private void CacheTextColors()
        {
            _availableNameColor = displayNameText != null ? displayNameText.color : Color.white;
            _availableHintColor = hintText != null ? hintText.color : Color.white;
        }

        private void FadeIn()
        {
            _isVisible = true;
            _targetAlpha = 1f;
            if (contentGroup != null)
            {
                contentGroup.gameObject.SetActive(true);
            }
        }

        private void FadeOut()
        {
            _isVisible = false;
            _targetAlpha = 0f;
        }
    }
}
