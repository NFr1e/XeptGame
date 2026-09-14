using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XeptGame.Interaction;
using XeptGame.UI.Billboard;
using XeptKit.Core;
using XeptKit.UI.Manager;

namespace XeptGame.UI
{
    /// <summary>
    /// 交互提示 Form 逻辑（常驻 HUD Form；Interaction_Prompt_V3_Design.md §3.1）：
    /// 单实例 = 单选中宿主（v3 边界）。职责：
    /// <list type="bullet">
    /// <item><b>数据源</b>：OnOpenAsync 从 args 收 <see cref="PromptRuntimeContext"/>（executor/相机），
    /// 订阅 <see cref="IInteractionExecutor.HostChanged"/>（宿主变化 → 显隐 + 内容重列）；</item>
    /// <item><b>头部（图标 + 交互物名字）</b>：宿主实现可选 <see cref="IInteractionInfoContext"/> 则展示——
    /// 名字走 <c>DisplayNameKey</c> 本地化解析（TryGet，未注册 → 隐名）或 <c>DisplayName</c> 直显回退；
    /// 图标 Sprite/Texture 双通道（<see cref="PromptIconView"/>）；</item>
    /// <item><b>动作行</b>：<see cref="InputPromptView"/> 按动作集生成/销毁（即时创建，池后置），
    /// 键名经 <see cref="SlotKeyProvider"/>（实际绑定真源）；灰态 Update 每帧按 CanInteract 刷新（DP3）；</item>
    /// <item><b>显隐</b>：无宿主/无动作 → 清锚 + 清头 + 隐藏容器（Form 常驻不关）。</item>
    /// </list>
    /// </summary>
    public sealed class InteractPromptHudFormLogic : FormLogicBase
    {
        [Tooltip("定位容器（挂 HudBillboard 的 RectTransform，被投影到宿主屏幕位置）")]
        [SerializeField] private RectTransform promptContainer;

        [Tooltip("头部：交互物名字（Label；宿主无显示信息/键未注册时隐藏）")]
        [SerializeField] private Label headerNameLabel;

        [Tooltip("头部：交互图标（Sprite/Texture 双通道）")]
        [SerializeField] private PromptIconView headerIcon;

        [Tooltip("动作行父级（VerticalLayoutGroup；行由本逻辑生成）")]
        [SerializeField] private RectTransform rowsRoot;

        [Tooltip("动作行预制体（InputPromptView）")]
        [SerializeField] private InputPromptView rowPrefab;

        [Tooltip("投影效果器（必接：HudBillboard，锚定宿主世界锚点）")]
        [SerializeField] private HudBillboard billboard;

        private readonly List<RowBinding> _rows = new();

        private IInteractionExecutor _executor;

        private sealed class RowBinding
        {
            public IInteractionAction Action;
            public InputPromptView View;
        }

        public override UniTask OnOpenAsync(FormHandle handle, object args, CancellationToken cancellationToken)
        {
            if (args is PromptRuntimeContext ctx && ctx.Executor != null)
            {
                _executor = ctx.Executor;
                _executor.HostChanged += OnHostChanged;

                if (billboard == null)
                {
                    Log.Error("[InteractPromptHudFormLogic] billboard 未接线（HudBillboard）——提示不跟随锚点。");
                }
                else if (ctx.CameraTransform != null)
                {
                    billboard.CameraTransform = ctx.CameraTransform;
                }
                else
                {
                    Log.Error("[InteractPromptHudFormLogic] 相机未注入（PromptRuntimeContext.CameraTransform 为空）——提示不投影。");
                }
            }
            else
            {
                Log.Error("[InteractPromptHudFormLogic] 打开参数缺少 PromptRuntimeContext（executor 必填）——提示不可用。");
            }

            // 打开时若已有选中宿主（少见），立即呈现
            if (_executor != null)
            {
                ApplyHost(_executor.CurrentHost, _executor.CurrentActions);
            }

            return UniTask.CompletedTask;
        }

        public override UniTask OnCloseAsync()
        {
            if (_executor != null)
            {
                _executor.HostChanged -= OnHostChanged;
                _executor = null;
            }

            Hide();
            return UniTask.CompletedTask;
        }

        private void OnHostChanged(InteractionHostChangedArgs args)
            => ApplyHost(args.Host, args.Actions);

        private void ApplyHost(ISelectable host, IReadOnlyList<IInteractionAction> actions)
        {
            ClearRows();

            if (host == null || actions == null || actions.Count == 0)
            {
                Hide();
                return;
            }

            if (host is Component hostComponent)
            {
                // 锚点：宿主自定义锚点（InteractionAnchorPoint）优先，未指定回退宿主 Transform
                billboard?.SetAnchor(InteractionAnchorResolver.Resolve(host));
                ApplyHeader(hostComponent);
            }

            if (promptContainer != null)
            {
                promptContainer.gameObject.SetActive(true);
            }

            if (rowsRoot != null && rowPrefab != null)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    var action = actions[i];
                    var view = Instantiate(rowPrefab, rowsRoot);
                    view.Bind(SlotKeyProvider.ToDisplayText(action.Slot), InteractionPromptText.Resolve(action.PromptKey));
                    _rows.Add(new RowBinding { Action = action, View = view });
                }
            }
        }

        /// <summary>头部内容：名字 = 键解析（未注册隐名）或直显回退；图标 = Sprite/Texture 任一，全空隐藏。</summary>
        private void ApplyHeader(Component hostComponent)
        {
            var info = hostComponent.GetComponentInParent<IInteractionInfoContext>();
            var name = info != null ? ResolveName(info) : string.Empty;

            if (headerNameLabel != null)
            {
                if (string.IsNullOrEmpty(name))
                {
                    headerNameLabel.gameObject.SetActive(false);
                }
                else
                {
                    headerNameLabel.SetText(name);
                    headerNameLabel.gameObject.SetActive(true);
                }
            }

            if (headerIcon == null)
            {
                return;
            }

            if (info != null && info.IconSprite != null)
            {
                headerIcon.ShowSprite(info.IconSprite);
            }
            else if (info != null && info.IconTexture != null)
            {
                headerIcon.ShowTexture(info.IconTexture);
            }
            else
            {
                headerIcon.Clear();
            }
        }

        /// <summary>名字解析：有键 → LocalizationManager.TryGet（未命中 → 隐名，静默）；无键 → 直显文案。</summary>
        private static string ResolveName(IInteractionInfoContext info)
        {
            if (!string.IsNullOrEmpty(info.DisplayNameKey))
            {
                return AppEntry.LocalizationManager != null
                       && AppEntry.LocalizationManager.TryGet(info.DisplayNameKey, out var value)
                    ? value
                    : string.Empty;
            }

            return info.DisplayName ?? string.Empty;
        }

        private void Hide()
        {
            billboard?.ClearAnchor();

            if (headerNameLabel != null)
            {
                headerNameLabel.gameObject.SetActive(false);
            }

            headerIcon?.Clear();

            if (promptContainer != null)
            {
                promptContainer.gameObject.SetActive(false);
            }
        }

        private void ClearRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                var view = _rows[i].View;
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            _rows.Clear();
        }

        private void Update()
        {
            if (_rows.Count == 0 || _executor == null)
            {
                return;
            }

            var ctx = new InteractionContext(transform);
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.View != null)
                {
                    row.View.SetAvailable(row.Action.CanInteract(ctx));
                }
            }
        }
    }
}
