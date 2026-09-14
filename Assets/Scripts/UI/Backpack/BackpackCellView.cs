using System;
using UnityEngine;
using UnityEngine.UI;
using XeptKit.Core;

namespace XeptGame.UI.Backpack
{
    /// <summary>
    /// 一个背包格视图（Backpack_UI_Design.md B1）：<b>格视图 = 槽位</b>，一格一个实例，
    /// 只负责"把一份快照画出来"与"报告自己被点了"，不认识容器、不认识编排器。
    /// <list type="bullet">
    /// <item><b>图标双通道</b>：物品的 <c>IconKind</c> 决定画 Sprite 还是 Texture，另一路关闭
    /// （与 <c>PromptIconView</c> 同一政策）；无图标 = 两路都关（空格与"无图标物品"视觉一致，
    /// 由角标/数量区分）；</item>
    /// <item><b>数量角标只在 &gt; 1 时出现</b>：网格惯例——"1" 是噪声；实例行数量恒 1，
    /// 改用另一枚角标区分（<c>HasInstance</c>）；</item>
    /// <item><b>不缓存物品清单</b>：每次 <see cref="Bind"/> 都按传入快照整体重画（幂等）。</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackpackCellView : MonoBehaviour
    {
        [Tooltip("Sprite 通道图标（IconKind.Sprite）")]
        [SerializeField] private Image iconImage;

        [Tooltip("Texture 通道图标（IconKind.Texture）")]
        [SerializeField] private RawImage iconRaw;

        [Tooltip("数量角标（Label；数量 ≤ 1 时自动隐藏）")]
        [SerializeField] private Label countLabel;

        [Tooltip("实例行角标（有状态载荷：数量恒 1、不参与合并）")]
        [SerializeField] private GameObject instanceBadge;

        [Tooltip("选中高亮（选中时激活）")]
        [SerializeField] private GameObject selectionHighlight;

        [Tooltip("点击按钮（点自己 → 报告格号）")]
        [SerializeField] private Button button;

        /// <summary>本视图对应的格号（<see cref="Bind"/> 时给定；-1 = 未绑定）。</summary>
        public int Cell { get; private set; } = -1;

        /// <summary>被点击（参数 = 格号）。</summary>
        public event Action<int> Clicked;

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(OnClick);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
            }

            Clicked = null;
        }

        /// <summary>按快照整体重画（幂等；空格也走这里，视图不隐藏——网格要看得见空格）。</summary>
        public void Bind(int cell, in BackpackCellSnapshot snapshot)
        {
            Cell = cell;
            SetSelected(false);

            ApplyIcon(snapshot.Item);

            var count = snapshot.Count;
            if (countLabel != null)
            {
                if (count > 1)
                {
                    countLabel.gameObject.SetActive(true);
                    countLabel.SetText(LocalizedText.Resolve(BackpackTextKeys.CellCount, count));
                }
                else
                {
                    countLabel.gameObject.SetActive(false);
                }
            }

            if (instanceBadge != null)
            {
                instanceBadge.SetActive(snapshot.HasInstance);
            }
        }

        /// <summary>选中高亮（选中态由界面逻辑统一裁决，视图不自己记）。</summary>
        public void SetSelected(bool selected)
        {
            if (selectionHighlight != null)
            {
                selectionHighlight.SetActive(selected);
            }
        }

        private void ApplyIcon(XeptGame.Items.ItemDefinition item)
        {
            var sprite = item != null ? item.IconSprite : null;
            var texture = item != null ? item.IconTexture : null;

            if (iconImage != null)
            {
                iconImage.enabled = sprite != null;
                iconImage.sprite = sprite;
            }

            if (iconRaw != null)
            {
                iconRaw.enabled = texture != null;
                iconRaw.texture = texture;
            }
        }

        private void OnClick()
        {
            if (Cell < 0)
            {
                Log.Warning("[BackpackCellView] 未绑定就收到点击（Cell = -1）——忽略。");
                return;
            }

            Clicked?.Invoke(Cell);
        }
    }
}
