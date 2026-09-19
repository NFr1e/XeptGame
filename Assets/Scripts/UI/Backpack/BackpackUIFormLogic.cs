using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using XeptGame.Container;
using XeptGame.Items;
using XeptGame.Items.Operations;
using XeptKit.Core;
using XeptKit.UI.Manager;

namespace XeptGame.UI.Backpack
{
    /// <summary>
    /// 背包界面 Form 逻辑（Backpack_UI_Design.md B1/B2/B4/B5）：<b>格视图 = 槽位</b>，
    /// 全量建视图 + 之后逐格增量。职责：
    /// <list type="bullet">
    /// <item><b>数据源</b>：<see cref="BackpackRuntimeContext.Store"/> 的<b>槽位面</b>（经
    /// <see cref="BackpackUIData"/> 订阅两条事件轨）；聚合面 <c>Stacks</c> 不参与，只在兜底校验里当信号；</item>
    /// <item><b>动作</b>：使用 / 丢弃 只调编排器的两个请求口（<c>RequestConsume</c> / <c>RequestDrop</c>），
    /// <b>不自己组合</b>"移除 + 落地 + 回滚"，也不自己发播报；</item>
    /// <item><b>文案</b>：一律经 <see cref="LocalizedText"/>（键在 <see cref="BackpackTextKeys"/>，句子在 CSV）；</item>
    /// <item><b>计时</b>：本界面不做任何依赖 <c>deltaTime</c> 的事（暂停期间 <c>deltaTime == 0</c>），
    /// 所以 v1 不需要 unscaled 计时；将来加过渡动画时必须走 unscaled
    /// （Time_Authority_Design.md §7.3）。</item>
    /// </list>
    /// </summary>
    public sealed class BackpackUIFormLogic : FormLogicBase
    {
        [Tooltip("标题（Label）")]
        [SerializeField] private Label titleLabel;

        [Tooltip("容量（Label；已用格/总格）")]
        [SerializeField] private Label capacityLabel;

        [Tooltip("网格父级（挂 GridLayoutGroup；格视图由本逻辑生成）")]
        [SerializeField] private RectTransform gridRoot;

        [Tooltip("格视图预制体（BackpackCellView）")]
        [SerializeField] private BackpackCellView cellPrefab;

        [Tooltip("详情：物品名（Label）")]
        [SerializeField] private Label detailNameLabel;

        [Tooltip("详情：数量 / 每格上限（Label）")]
        [SerializeField] private Label detailInfoLabel;

        [Tooltip("动作按钮：使用（Label 为按钮文字）")]
        [SerializeField] private Button useButton;

        [Tooltip("动作按钮：丢弃")]
        [SerializeField] private Button dropButton;

        private readonly List<BackpackCellView> _cells = new();

        private BackpackRuntimeContext _ctx;
        private BackpackUIData _data;
        private int _selected = -1;

        public override UniTask OnOpenAsync(FormHandle handle, object args, CancellationToken cancellationToken)
        {
            if (args is not BackpackRuntimeContext ctx || ctx.Store == null || ctx.Operations == null)
            {
                Log.Error("[BackpackUIFormLogic] 打开参数缺少 BackpackRuntimeContext（store/operations 必填）——背包不可用。");
                return UniTask.CompletedTask;
            }

            if (gridRoot == null || cellPrefab == null)
            {
                Log.Error("[BackpackUIFormLogic] gridRoot / cellPrefab 未接线——格视图无法生成。");
                return UniTask.CompletedTask;
            }

            _ctx = ctx;
            _data = new BackpackUIData(ctx.Store);
            _data.CellChanged += OnCellChanged;
            _data.LayoutChanged += OnLayoutChanged;
            _data.RebuildRequested += OnRebuildRequested;

            ApplyStaticTexts();
            RebuildAll();
            return UniTask.CompletedTask;
        }

        public override UniTask OnCloseAsync()
        {
            if (useButton != null)
            {
                useButton.onClick.RemoveListener(OnUse);
            }

            if (dropButton != null)
            {
                dropButton.onClick.RemoveListener(OnDrop);
            }

            if (_data != null)
            {
                _data.CellChanged -= OnCellChanged;
                _data.LayoutChanged -= OnLayoutChanged;
                _data.RebuildRequested -= OnRebuildRequested;
                _data.Dispose();
                _data = null;
            }

            ClearCells();
            _ctx = null;
            _selected = -1;
            return UniTask.CompletedTask;
        }

        // ---- 建立 / 刷新 ----

        /// <summary>全量建视图（打开时一次；兜底路径也走它）。</summary>
        private void RebuildAll()
        {
            ClearCells();
            for (int i = 0; i < _data.Snapshot.Count; i++)
            {
                AddCell(i);
            }

            RefreshCapacity();
            ApplySelection(-1);
        }

        private void AddCell(int index)
        {
            var view = Instantiate(cellPrefab, gridRoot);
            view.Clicked += OnCellClicked;
            view.Bind(index, _data.Snapshot[index]);
            _cells.Add(view);
        }

        private void ClearCells()
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i] != null)
                {
                    _cells[i].Clicked -= OnCellClicked;
                    Destroy(_cells[i].gameObject);
                }
            }

            _cells.Clear();
        }

        /// <summary>槽位轨：只动那一格。</summary>
        private void OnCellChanged(int cell)
        {
            if (cell < 0 || cell >= _cells.Count)
            {
                return;
            }

            _cells[cell].Bind(cell, _data.Snapshot[cell]);
            _cells[cell].SetSelected(cell == _selected); // Bind 会清高亮，选中态要按界面真值补回

            RefreshCapacity();
            if (cell == _selected)
            {
                RefreshDetail();
                RefreshActions();
            }
        }

        /// <summary>结构轨：只在末尾增删格（不重排已有格——格号即身份）。</summary>
        private void OnLayoutChanged(int capacity)
        {
            while (_cells.Count < capacity && _cells.Count < _data.Snapshot.Count)
            {
                AddCell(_cells.Count);
            }

            while (_cells.Count > capacity)
            {
                RemoveLastCell();
            }

            RefreshCapacity();
            if (_selected >= _cells.Count)
            {
                ApplySelection(-1);
            }
        }

        private void RemoveLastCell()
        {
            var last = _cells[_cells.Count - 1];
            _cells.RemoveAt(_cells.Count - 1);
            if (last != null)
            {
                last.Clicked -= OnCellClicked;
                Destroy(last.gameObject);
            }
        }

        /// <summary>兜底轨：快照与真源不一致 → 整体重建（防御路径，正常不该发生）。</summary>
        private void OnRebuildRequested()
        {
            Log.Warning("[BackpackUIFormLogic] 视图快照与容器真源不一致——已请求全量重建（请检查是否有绕过槽位轨的写入路径）。");
            RebuildAll();
        }

        // ---- 选中与动作 ----

        private void OnCellClicked(int cell) => ApplySelection(cell == _selected ? -1 : cell);

        private void ApplySelection(int cell)
        {
            _selected = cell;
            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i] != null)
                {
                    _cells[i].SetSelected(i == cell);
                }
            }

            RefreshDetail();
            RefreshActions();
        }

        private void RefreshCapacity()
        {
            if (capacityLabel == null || _data == null)
            {
                return;
            }

            var used = 0;
            var snapshot = _data.Snapshot;
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (!snapshot[i].IsEmpty)
                {
                    used++;
                }
            }

            capacityLabel.SetText(LocalizedText.Resolve(BackpackTextKeys.Capacity, used, snapshot.Count));
        }

        private void RefreshDetail()
        {
            if (detailNameLabel == null && detailInfoLabel == null)
            {
                return;
            }

            var snapshot = SelectedSnapshot();
            if (snapshot == null)
            {
                SetText(detailNameLabel, LocalizedText.Resolve(BackpackTextKeys.DetailNone));
                SetText(detailInfoLabel, string.Empty);
                return;
            }

            var cell = snapshot.Value;
            SetText(detailNameLabel, LocalizedText.Resolve(cell.Item.DisplayNameKey));
            SetText(detailInfoLabel, BuildDetailInfo(cell));
        }

        /// <summary>详情第二行：数量 + 每格上限（两句完整句子，各带自己的占位符——不拼接动词）。</summary>
        private static string BuildDetailInfo(BackpackCellSnapshot cell)
        {
            var count = LocalizedText.Resolve(BackpackTextKeys.DetailCount, cell.Count);
            var limit = cell.Item.GetFacet<InventoryItemFacet>()?.profile?.ResolvedMaxStack;
            var stack = limit.HasValue
                ? LocalizedText.Resolve(BackpackTextKeys.DetailStackLimit, limit.Value)
                : LocalizedText.Resolve(BackpackTextKeys.DetailNoStackLimit);

            return count + "\n" + stack;
        }

        private BackpackCellSnapshot? SelectedSnapshot()
        {
            if (_data == null || _selected < 0 || _selected >= _data.Snapshot.Count)
            {
                return null;
            }

            var cell = _data.Snapshot[_selected];
            return cell.IsEmpty ? null : cell;
        }

        /// <summary>动作可用性（B2 的"显示判据"）：使用 = 消耗品且不是实例行；丢弃 = 有内容即可（空格不可丢）。</summary>
        private void RefreshActions()
        {
            var snapshot = SelectedSnapshot();
            var hasItem = snapshot.HasValue;
            var item = hasItem ? snapshot.Value.Item : null;
            var consumable = hasItem && !snapshot.Value.HasInstance && item.HasFacet<ConsumableFacet>() && !item.HasFacet<ContainerFacet>();

            if (useButton != null)
            {
                useButton.interactable = consumable;
            }

            if (dropButton != null)
            {
                dropButton.interactable = hasItem;
            }
        }

        private void OnUse()
        {
            var snapshot = SelectedSnapshot();
            if (snapshot == null)
            {
                return;
            }

            // 格寻址：**选中的那一格**才该动。按定义扣会从槽序最靠前的同物格开始扣，
            // 于是"选中后格却扣了前格"——这与界面的心智模型不符（格视图 = 槽位）。
            var receipt = _ctx.Operations.RequestConsume(_ctx.Store, new SlotId(_selected), 1);
            ReportReceipt("使用", receipt);
        }

        private void OnDrop()
        {
            if (_selected < 0)
            {
                return;
            }

            // 格寻址：以格为真相、只动这一格，且能丢实例行（"丢掉这个背包"）
            var receipt = _ctx.Operations.RequestDrop(_ctx.Store, new SlotId(_selected), 1);
            ReportReceipt("丢弃", receipt);
        }

        /// <summary>
        /// 回执汇报：v1 只落日志（<b>失败必须可见</b>——被拒的理由要能查）。
        /// 界面内的浮字提示是迟到项（需要一条界面级提示通道，不在本阶段）。
        /// </summary>
        private static void ReportReceipt(string action, OperationReceipt receipt)
        {
            if (receipt == null)
            {
                return;
            }

            if (receipt.Status == OperationStatus.Rejected || receipt.Status == OperationStatus.Failed)
            {
                Log.Warning($"[BackpackUI] {action}被拒：{receipt.Reason}");
            }
        }

        private void ApplyStaticTexts()
        {
            SetText(titleLabel, LocalizedText.Resolve(BackpackTextKeys.Title));
            SetText(useButton != null ? useButton.GetComponentInChildren<Label>() : null,
                LocalizedText.Resolve(BackpackTextKeys.ActionUse));
            SetText(dropButton != null ? dropButton.GetComponentInChildren<Label>() : null,
                LocalizedText.Resolve(BackpackTextKeys.ActionDrop));

            if (useButton != null)
            {
                useButton.onClick.RemoveListener(OnUse);
                useButton.onClick.AddListener(OnUse);
            }

            if (dropButton != null)
            {
                dropButton.onClick.RemoveListener(OnDrop);
                dropButton.onClick.AddListener(OnDrop);
            }
        }

        private static void SetText(Label label, string text)
        {
            if (label != null)
            {
                label.SetText(text ?? string.Empty);
            }
        }
    }
}
