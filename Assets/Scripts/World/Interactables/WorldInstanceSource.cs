using System;
using System.Collections.Generic;
using XeptGame.Container;
using XeptGame.Inv;
using XeptGame.Items;
using XeptKit.Event;

namespace XeptGame.World
{
    /// <summary>
    /// 携带实例的世界源（背包等；Item_Instance_Design.md §6）：内容 = <b>那一个</b> <see cref="ContainerInstance"/>。
    /// <list type="bullet">
    /// <item><b>按定义的写入一律拒绝</b>：实例的身份不可按定义定位/合并/拆分（与容器层"实例行"纪律一致）——
    /// 取放只能整体走 <see cref="TryTakeCarrier"/> / <see cref="TryReturnCarrier"/>；</item>
    /// <item><b>聚合面仍可见</b>：<see cref="CountOf"/> / <see cref="Stacks"/> 按"实例各计 1"报告，
    /// 使提示与调试件能像对待堆叠物一样读它；</item>
    /// <item>取出成功即本源为空（<see cref="HasContent"/> = false），视图据此隐藏。</item>
    /// </list>
    /// </summary>
    public sealed class WorldInstanceSource : IWorldCarrierSource
    {
        private readonly Func<bool> _available;
        private readonly Action _refresh;
        private readonly SafeEvent<ContainerChangeArgs> _changed = new();
        private ContainerInstance _carrier;
        private long _owner;

        public WorldInstanceSource(
            ContainerInstance carrier, long recordId = 0, Func<bool> available = null, Action refresh = null)
        {
            _carrier = carrier;
            RecordId = recordId;
            _available = available;
            _refresh = refresh;
        }

        /// <inheritdoc />
        public long RecordId { get; }

        /// <inheritdoc />
        public ContainerInstance Carrier => _carrier;

        /// <inheritdoc />
        public bool Available => _carrier != null && (_available?.Invoke() ?? true);

        /// <inheritdoc />
        public bool IsBusy => _owner != 0;

        /// <inheritdoc />
        public bool HasContent => _carrier != null;

        /// <inheritdoc />
        public ItemDefinition DisplayDefinition => _carrier?.Definition;

        /// <inheritdoc />
        public IReadOnlyList<ItemStack> Stacks
            => _carrier == null ? Array.Empty<ItemStack>() : new[] { new ItemStack(_carrier.Definition, 1) };

        /// <inheritdoc />
        public int CountOf(ItemDefinition definition)
            => _carrier != null && ReferenceEquals(definition, _carrier.Definition) ? 1 : 0;

        /// <inheritdoc />
        public bool Contains(ItemDefinition definition) => CountOf(definition) > 0;

        /// <summary>
        /// 按定义加入：<b>恒 false</b>——实例不可被"加进来"（身份不能凭空生成/复制），
        /// 唯一入口是构造时给定或 <see cref="TryReturnCarrier"/> 回滚。
        /// </summary>
        public bool TryAdd(ItemDefinition definition, int count) => false;

        /// <summary>
        /// 按定义移除：<b>恒 false</b>——实例的身份不可按定义定位（容器层同一纪律），
        /// 取出请走 <see cref="TryTakeCarrier"/>。
        /// </summary>
        public bool TryRemove(ItemDefinition definition, int count) => false;

        /// <inheritdoc />
        public event Action<ContainerChangeArgs> Changed
        {
            add => _changed.Add(value);
            remove => _changed.Remove(value);
        }

        /// <inheritdoc />
        public bool TryTakeCarrier(out ContainerInstance carrier)
        {
            carrier = _carrier;
            if (carrier == null)
            {
                return false;
            }

            _carrier = null;
            _changed.Invoke(new ContainerChangeArgs(carrier.Definition, 1, 0));
            return true;
        }

        /// <inheritdoc />
        public bool TryReturnCarrier(ContainerInstance carrier)
        {
            if (carrier == null || _carrier != null)
            {
                return false;
            }

            _carrier = carrier;
            _changed.Invoke(new ContainerChangeArgs(carrier.Definition, 0, 1));
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
