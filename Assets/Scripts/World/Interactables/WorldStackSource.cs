using System;
using System.Collections.Generic;
using XeptGame.Container;
using XeptGame.Items;
using XeptKit.Event;

namespace XeptGame.World
{
    /// <summary>
    /// 无状态堆叠物的世界源（Item_Instance_Design.md §6；原 <c>WorldItemContainer</c> 正名并接入
    /// <see cref="IWorldSource"/>）：内容 = "某定义 × N"，拾取是<b>按定义</b>扣减。
    /// <list type="bullet">
    /// <item>状态与忙碌标记分离：占用（<see cref="IsBusy"/>）不改变归属，物品仍属于世界；</item>
    /// <item><see cref="TryAdd"/> 是回滚入口（同定义、不受场景可见性影响的恢复），不是业务写入面；</item>
    /// <item><see cref="RecordId"/> 为 0 = 场景直摆、尚未登记记录（视图不因它而删记录）。</item>
    /// </list>
    /// </summary>
    public sealed class WorldStackSource : IWorldSource
    {
        private readonly ItemDefinition _item;
        private readonly Func<bool> _available;
        private readonly Action _refresh;
        private readonly SafeEvent<ContainerChangeArgs> _changed = new();
        private int _count;
        private long _owner;

        public WorldStackSource(
            ItemDefinition item, int count, Func<bool> available = null, Action refresh = null, long recordId = 0)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            _item = item;
            _count = count;
            _available = available;
            _refresh = refresh;
            RecordId = recordId;
        }

        /// <inheritdoc />
        public long RecordId { get; }

        /// <summary>源被一次拾取操作占用，重复交互暂不可接纳；物品仍属于世界容器。</summary>
        public bool IsBusy => _owner != 0;

        /// <inheritdoc />
        public bool Available => _item != null && (_available?.Invoke() ?? true);

        /// <inheritdoc />
        public bool HasContent => _count > 0;

        /// <inheritdoc />
        public ItemDefinition DisplayDefinition => _item;

        /// <summary>当前世界堆剩余数量，提交批结束后场景壳据此刷新显隐。</summary>
        public int Remaining => _count;

        /// <summary>容器变更（SafeEvent：异常隔离 + 订阅去重）。</summary>
        public event Action<ContainerChangeArgs> Changed
        {
            add => _changed.Add(value);
            remove => _changed.Remove(value);
        }

        /// <inheritdoc />
        public IReadOnlyList<ItemStack> Stacks
        {
            get
            {
                if (_count <= 0 || _item == null)
                {
                    return Array.Empty<ItemStack>();
                }

                return new[] { new ItemStack(_item, _count) };
            }
        }

        /// <inheritdoc />
        public int CountOf(ItemDefinition definition) => ReferenceEquals(definition, _item) ? _count : 0;

        /// <inheritdoc />
        public bool Contains(ItemDefinition definition) => CountOf(definition) > 0;

        /// <inheritdoc />
        public bool TryRemove(ItemDefinition definition, int count)
        {
            if (!Available || count <= 0 || CountOf(definition) < count)
            {
                return false;
            }

            var old = _count;
            _count -= count;
            _changed.Invoke(new ContainerChangeArgs(_item, old, _count));
            return true;
        }

        /// <summary>同定义回滚入口；不依赖场景可见性，目标拒绝后仍可恢复原数量。</summary>
        public bool TryAdd(ItemDefinition definition, int count)
        {
            if (!ReferenceEquals(definition, _item) || count <= 0 || count > int.MaxValue - _count)
            {
                return false;
            }

            var old = _count;
            _count += count;
            _changed.Invoke(new ContainerChangeArgs(_item, old, _count));
            return true;
        }

        /// <inheritdoc />
        public bool TryAcquire(long operationId)
        {
            if (!Available || operationId <= 0 || (_owner != 0 && _owner != operationId))
            {
                return false;
            }

            _owner = operationId;
            return true;
        }

        /// <inheritdoc />
        public void Release(long operationId)
        {
            if (_owner == operationId)
            {
                _owner = 0;
            }
        }

        /// <inheritdoc />
        public void RefreshView() => _refresh?.Invoke();
    }
}
