using System;
using XeptGame.Items;
using XeptKit.Core;
using XeptKit.Event;

namespace XeptGame.Container
{
    /// <summary>
    /// 通用槽容器（SlotStore_Design.md §6）：固定格数数组 + 容量扩缩 + 整理压缩 + 溢出丢弃出口。
    /// 背包（<c>XeptGame.Inv.Inventory</c>）是它的第一个使用者；箱子/储物格可复用。
    /// <list type="bullet">
    /// <item><b>格号 = 数组下标 = 槽身份</b>：容量变化<b>只在尾部增删</b>（append/trim），既有格号恒不漂移
    /// ——UI 的选中/拖拽/位移动画因此可依赖格号；</item>
    /// <item><b>扩容</b>：尾部追加空格，内容与格号零搬运，只发一条 <see cref="LayoutChanged"/>；</item>
    /// <item><b>缩容 = 自动压缩 + 溢出丢弃</b>（用户裁定）：保留区稳定缩进 → 待裁区按格号升序搬入保留区
    /// （先并入同类未满格、再占最小空格、允许跨格拆分）→ <b>最终放不下的余量丢弃</b>；</item>
    /// <item><b>溢出丢弃是显式出口</b>：丢弃必须"可见"——发聚合轨（物品总数减少）+ 上报数量 + 调 <c>discardSink</c>
    /// （装配接缝；v1 记诊断，后期世界 Drop 在同一接缝落地）；</item>
    /// <item><b>整理（<see cref="TryCompact"/>）永不丢弃</b>：无溢出源，只做保留区内缩进；</item>
    /// <item><b>容量下限 0</b>（= 没有背包）：<c>TryAdd</c> 恒失败 → <c>ContainerTransfer.Move</c> 走目标拒绝并回滚源。</item>
    /// <item><b>两种载荷</b>（Item_Instance_Design.md §2.2）：无状态堆叠行与实例行（数量恒 1、不参与合并）；
    /// 压缩与缩容把两者<b>同规格搬运</b>（实例连同身份走 <c>TryMoveContentTo</c> / <c>PlaceInstanceWithin</c>，
    /// 绝不按定义重放——那会丢身份或撞上"实例行不可合并"）。</item>
    /// </list>
    /// 所有变更走基类同一道提交门（<see cref="SlotContainer.TryBeginMutation"/>）。
    /// 非 sealed：背包域以 <c>XeptGame.Inv.Inventory</c> 特化（固定默认格数与丢弃出口），箱子可直接实例化或另行特化。
    /// </summary>
    public class SlotStore : SlotContainer
    {
        /// <summary>结构性变更轨（整理/扩缩容；界面据此重建视图）。</summary>
        private readonly SafeEvent<LayoutChangeArgs> _layoutChanged = new();

        /// <summary>溢出丢弃出口（可空）：v1 = 诊断/上报，后期 = 世界 Drop。</summary>
        private readonly Action<ItemDefinition, int> _discardSink;

        public SlotStore(int capacity, Action<ItemDefinition, int> discardSink = null) : base(CreateCells(capacity))
            => _discardSink = discardSink;

        /// <summary>结构性变更（批量档）：整理/扩容/缩容后发一条。</summary>
        public event Action<LayoutChangeArgs> LayoutChanged
        {
            add => _layoutChanged.Add(value);
            remove => _layoutChanged.Remove(value);
        }

        /// <summary>当前格数。</summary>
        public int Capacity => SlotList.Count;

        /// <summary>
        /// 应用解析后的格数（容量来源由外部合成；本类只认数字，幂等）：
        /// 扩容 = 尾部追加空格；缩容 = 自动压缩 + 溢出丢弃。
        /// 返回<b>被丢弃的总单位数</b>（0 = 无丢弃；细则经 <c>discardSink</c> 逐物品上报）。
        /// </summary>
        public int ApplyCapacity(int newCapacity)
        {
            Guard.True(newCapacity >= 0, "格数不得为负。");

            if (newCapacity == Capacity || !TryBeginMutation())
            {
                return 0;
            }

            try
            {
                var discarded = 0;
                if (newCapacity > Capacity)
                {
                    for (int i = Capacity; i < newCapacity; i++)
                    {
                        AppendSlot(new StackSlot(i));
                    }
                }
                else
                {
                    discarded = ShrinkTo(newCapacity);
                }

                PublishLayout();
                return discarded;
            }
            finally
            {
                EndMutation();
            }
        }

        /// <summary>整理：保留区内稳定缩进（同物品合并同类格之上不做额外归并）。永不丢弃；返回是否有搬运发生。</summary>
        public bool TryCompact()
        {
            if (!TryBeginMutation())
            {
                return false;
            }

            try
            {
                var moved = CompactWithin(Capacity);
                if (moved)
                {
                    PublishLayout();
                }

                return moved;
            }
            finally
            {
                EndMutation();
            }
        }

        // ---- 内部 ----

        /// <summary>
        /// 缩容到 <paramref name="newCapacity"/>（调用方已在提交门内）：
        /// 先压缩保留区 → 待裁区逐个搬入保留区 → 搬不下的余量丢弃 → 裁掉尾部空格。
        /// </summary>
        private int ShrinkTo(int newCapacity)
        {
            CompactWithin(newCapacity);

            var discarded = 0;
            for (int i = newCapacity; i < Capacity; i++)
            {
                var slot = SlotList[i];
                if (slot.IsEmpty)
                {
                    continue;
                }

                // 实例行（有状态载荷）：优先安置到保留区最靠前的空格；真的放不下才走出口
                if (slot.HasInstance)
                {
                    var instance = slot.Instance;
                    var beforeInstance = TotalCountOfCore(instance.Definition);
                    if (!slot.TryTakeInstance(out _))
                    {
                        throw new InvalidOperationException("缩容失败：待裁槽状态与实际不符（" + slot.Id + "）。");
                    }

                    if (PlaceInstanceWithin(instance, newCapacity))
                    {
                        continue;
                    }

                    // ⏳ T5（WorldDrop 接线）：出口载荷升级为携带实例句柄（整包落地）；今日占位语义只上报定义 + 1
                    PublishChanged(instance.Definition, beforeInstance, beforeInstance - 1);
                    _discardSink?.Invoke(instance.Definition, 1);
                    discarded += 1;
                    continue;
                }

                var item = slot.Item;
                var count = slot.Count;
                var before = TotalCountOfCore(item);

                if (!slot.TryTake(count))
                {
                    throw new InvalidOperationException("缩容失败：待裁槽状态与实际不符（" + slot.Id + "）。");
                }

                var placed = PlaceWithin(item, count, newCapacity);
                var leftover = count - placed;
                if (leftover <= 0)
                {
                    continue;
                }

                // 丢弃必须可见：聚合轨（总数减少）+ 出口上报
                PublishChanged(item, before, before - leftover);
                _discardSink?.Invoke(item, leftover);
                discarded += leftover;
            }

            while (Capacity > newCapacity)
            {
                RemoveTrailingSlot();
            }

            return discarded;
        }

        /// <summary>保留区 [0, limit) 内的稳定缩进（格号升序、保持相对顺序）；返回是否有搬运。</summary>
        private bool CompactWithin(int limit)
        {
            var moved = false;
            var write = 0;

            for (int read = 0; read < limit; read++)
            {
                var slot = SlotList[read];
                if (slot.IsEmpty)
                {
                    continue;
                }

                if (read != write)
                {
                    // 两种载荷同规格：实例连同身份一起搬（TryMoveContentTo），不按定义重放
                    var target = SlotList[write];
                    if (!slot.TryMoveContentTo(target))
                    {
                        throw new InvalidOperationException("压缩失败：目标格无法容纳（" + target.Id + "）。");
                    }

                    moved = true;
                }

                write++;
            }

            return moved;
        }

        private void PublishLayout() => _layoutChanged.Invoke(new LayoutChangeArgs(Capacity));

        private static SlotBase[] CreateCells(int capacity)
        {
            Guard.True(capacity >= 0, "格数不得为负。");

            var cells = new SlotBase[capacity];
            for (int i = 0; i < capacity; i++)
            {
                cells[i] = new StackSlot(i);
            }

            return cells;
        }
    }
}
