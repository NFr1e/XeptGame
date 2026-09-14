using System;
using System.Collections.Generic;
using XeptGame.Container;
using XeptGame.Items;

namespace XeptGame.UI.Backpack
{
    /// <summary>
    /// 一格的可绘制快照（<b>值语义</b>；Backpack_UI_Design.md B1/B4）：
    /// 格视图绘制只读它，**不直接摸槽**——这样"格号 → 内容"的比对可以在纯 C# 里做完，
    /// 界面层只负责把差异画出来。
    /// </summary>
    public readonly struct BackpackCellSnapshot : IEquatable<BackpackCellSnapshot>
    {
        /// <summary>格号（= 槽身份值；背包格里即数组下标）。</summary>
        public readonly int Cell;

        /// <summary>占用（null = 空格）。</summary>
        public readonly ItemDefinition Item;

        /// <summary>数量（空格 = 0；实例行恒 1）。</summary>
        public readonly int Count;

        /// <summary>是否持<b>实例</b>（有状态载荷：数量恒 1、不参与合并）。</summary>
        public readonly bool HasInstance;

        /// <summary>实例 id（无实例 = 0）；用于"换了另一个背包"这类<b>同定义不同身份</b>的比对。</summary>
        public readonly long InstanceId;

        /// <summary>是否为空。</summary>
        public bool IsEmpty => Item == null;

        public BackpackCellSnapshot(int cell, ItemDefinition item, int count, bool hasInstance, long instanceId)
        {
            Cell = cell;
            Item = item;
            Count = count;
            HasInstance = hasInstance;
            InstanceId = instanceId;
        }

        /// <summary>从槽读出快照（<b>唯一</b>把槽翻译成可绘制值的点）。</summary>
        public static BackpackCellSnapshot From(SlotBase slot)
            => new(slot.Id.Value, slot.Item, slot.Count, slot.HasInstance, slot.Instance?.Id ?? 0);

        /// <summary>同格内容是否一致（<b>引用</b>比定义——SO 是共享资产，引用即身份）。</summary>
        public bool Equals(BackpackCellSnapshot other)
            => Cell == other.Cell
               && ReferenceEquals(Item, other.Item)
               && Count == other.Count
               && HasInstance == other.HasInstance
               && InstanceId == other.InstanceId;

        public override bool Equals(object obj) => obj is BackpackCellSnapshot other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(Cell, Item, Count, HasInstance, InstanceId);
    }

    /// <summary>
    /// 背包界面视图模型（Backpack_UI_Design.md B1 + B4；<b>纯 C#，EditMode 可全覆盖</b>）：
    /// 把容器两条事件轨翻译成界面能直接消费的三条通知，并维护一份"当前画的是什么"的快照。
    /// <list type="bullet">
    /// <item><b>数据源只有槽位面</b>（<c>Slots</c>）；聚合面 <c>Stacks</c> 不参与格视图——它只在兜底校验里
    /// 作为"是否需要全量重建"的信号源；</item>
    /// <item><b>槽位轨 → 逐格增量</b>：<c>SlotChanged</c> 只更新那一格并发 <see cref="CellChanged"/>（格号）；</item>
    /// <item><b>结构轨 → 增删格</b>：<c>LayoutChanged</c> 调整快照长度并发 <see cref="LayoutChanged"/>（新格数）；</item>
    /// <item><b>聚合轨 → 兜底</b>：与快照比对，不一致才发 <see cref="RebuildRequested"/>（防御，不常态走）；</item>
    /// <item><b>不缓存第二份物品清单</b>：<see cref="Snapshot"/> 是"当前画出来的样子"，不是真源；
    /// 真源永远是容器的槽。</item>
    /// </list>
    /// 依赖 <see cref="SlotStore"/> 而非 <c>Inventory</c>：界面只认"有格的容器"，不关心它是背包还是箱子。
    /// </summary>
    public sealed class BackpackUIData : IDisposable
    {
        private readonly SlotStore _store;
        private readonly List<BackpackCellSnapshot> _snapshot = new();
        private bool _disposed;

        /// <summary>单格内容变化（参数 = 格号）：界面只更新该格视图。</summary>
        public event Action<int> CellChanged;

        /// <summary>格数变化（参数 = 新格数）：界面在末尾增/删格视图，不重排已有格。</summary>
        public event Action<int> LayoutChanged;

        /// <summary>兜底：与视图快照不一致，需要全量重建（防御路径，正常不该发生）。</summary>
        public event Action RebuildRequested;

        public BackpackUIData(SlotStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Rebuild();

            _store.SlotChanged += OnSlotChanged;
            _store.LayoutChanged += OnLayoutChanged;
            _store.Changed += OnAggregateChanged;
        }

        /// <summary>当前快照（只读；"画出来的样子"，不是真源）。</summary>
        public IReadOnlyList<BackpackCellSnapshot> Snapshot => _snapshot;

        /// <summary>当前格数。</summary>
        public int Capacity => _snapshot.Count;

        /// <summary>全量重建快照（打开界面时调用一次；兜底路径也走它）。</summary>
        public void Rebuild()
        {
            _snapshot.Clear();
            var slots = _store.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                _snapshot.Add(BackpackCellSnapshot.From(slots[i]));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _store.SlotChanged -= OnSlotChanged;
            _store.LayoutChanged -= OnLayoutChanged;
            _store.Changed -= OnAggregateChanged;
            CellChanged = null;
            LayoutChanged = null;
            RebuildRequested = null;
        }

        /// <summary>槽位轨：只动那一格（真源重读 + 与快照比对，避免把重复通知画成两次刷新）。</summary>
        private void OnSlotChanged(SlotChangeArgs args)
        {
            var index = IndexOf(args.Slot);
            if (index < 0)
            {
                // 格号找不到 = 结构正在变（缩容尾部移除与内容搬运交错）。交给 LayoutChanged / 兜底收拾。
                return;
            }

            var current = BackpackCellSnapshot.From(_store.Slots[index]);
            if (current.Equals(_snapshot[index]))
            {
                return;
            }

            _snapshot[index] = current;
            CellChanged?.Invoke(index);
        }

        /// <summary>结构轨：调整快照长度（扩容追加空格；缩容裁尾），快照内容已在逐格轨里更新过。</summary>
        private void OnLayoutChanged(LayoutChangeArgs args)
        {
            var capacity = args.Capacity;
            while (_snapshot.Count < capacity)
            {
                _snapshot.Add(BackpackCellSnapshot.From(_store.Slots[_snapshot.Count]));
            }

            while (_snapshot.Count > capacity)
            {
                _snapshot.RemoveAt(_snapshot.Count - 1);
            }

            LayoutChanged?.Invoke(capacity);
        }

        /// <summary>
        /// 聚合轨（兜底）：按定义算出的总量与快照对不上 ⇒ 有我们没跟上的变化（例如某条路径绕过了槽事件）。
        /// 这里只做"发现并请求全量重建"，不尝试局部修补——修补逻辑本身就是漂移来源。
        /// </summary>
        private void OnAggregateChanged(ContainerChangeArgs _)
        {
            if (IsConsistentWithSource())
            {
                return;
            }

            Rebuild();
            RebuildRequested?.Invoke();
        }

        /// <summary>
        /// 快照与真源是否一致（逐格比对；格数不同即不一致）。
        /// <b>公开给运行期探针/测试</b>：这是"界面从不漂移"这条不变量的可断言形式。
        /// </summary>
        public bool IsConsistentWithSource()
        {
            var slots = _store.Slots;
            if (slots.Count != _snapshot.Count)
            {
                return false;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (!BackpackCellSnapshot.From(slots[i]).Equals(_snapshot[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>格号 → 快照下标（<see cref="SlotStore"/> 保证 <c>Id.Value == 下标</c>，此处仍按身份查找以防漂移）。</summary>
        private int IndexOf(SlotId cell)
        {
            var slots = _store.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Id == cell)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
