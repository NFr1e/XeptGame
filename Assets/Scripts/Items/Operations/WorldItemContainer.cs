using System;
using System.Collections.Generic;
using XeptGame.Inv;
using XeptKit.Event;

namespace XeptGame.Items.Operations
{
    /// <summary>世界堆的容器端点，状态与忙碌标记分离；回滚可恢复原物。</summary>
    public sealed class WorldItemContainer : IWorldItemSource
    {
        private readonly ItemDefinition _item;
        private readonly Func<bool> _available;
        private readonly Action _refresh;
        private int _count;
        private long _owner;

        /// <summary>源被一次拾取操作占用，重复交互暂不可接纳；物品仍属于世界容器。</summary>
        public bool IsBusy => _owner != 0;

        /// <inheritdoc />
        public bool Available => _item != null && (_available?.Invoke() ?? true);

        /// <summary>当前世界堆剩余数量，提交批结束后场景壳据此刷新显隐。</summary>
        public int Remaining => _count;

        private readonly SafeEvent<InventoryChangeArgs> _changed = new();

        /// <summary>容器变更（SafeEvent：异常隔离 + 订阅去重）。</summary>
        public event Action<InventoryChangeArgs> Changed
        {
            add => _changed.Add(value);
            remove => _changed.Remove(value);
        }

        public WorldItemContainer(ItemDefinition item, int count, Func<bool> available = null, Action refresh = null)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            _item = item;
            _count = count;
            _available = available;
            _refresh = refresh;
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
            _changed.Invoke(new InventoryChangeArgs(_item, old, _count));
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
            _changed.Invoke(new InventoryChangeArgs(_item, old, _count));
            return true;
        }

        public bool TryAcquire(long operationId)
        {
            if (!Available || operationId <= 0 || (_owner != 0 && _owner != operationId))
            {
                return false;
            }

            _owner = operationId;
            return true;
        }

        public void Release(long operationId)
        {
            if (_owner == operationId)
            {
                _owner = 0;
            }
        }

        public void RefreshView()
        {
            _refresh?.Invoke();
        }
    }
}
