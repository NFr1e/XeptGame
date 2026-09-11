using System;
using XeptGame.Container;
using XeptGame.Equip;
using XeptGame.Inv;
using XeptKit.Event;

namespace XeptGame.Items.Operations
{
    /// <summary>
    /// 物品操作协调器（EB-02）：唯一业务转移入口，管理单次操作的路线、时机和终局。
    /// 来源/去向留在本层；装备 FSM 只接收 Draw/Stow 与提交后的占用快照。
    /// 容器写入在同步提交区执行；通知期间拒绝重入，不保留第二条即时命令链路。
    /// 拾取事件在实际提交后聚合发一次，与 Drawing 完成没有依赖。
    /// </summary>
    public sealed class ItemOperationCoordinator : IDisposable
    {
        private enum Step
        {
            Stow,
            Transfer,
            Draw
        }

        private sealed class Operation
        {
            public OperationReceipt Receipt;
            public IItemContainer Source;
            public IItemContainer Destination;
            public IItemContainer Remainder;
            public IWorldItemSource World;
            public ItemDefinition Item;
            public int Count;
            public bool Equip;
            public bool Pickup;
            public bool Published;
            public PickupIntent Intent;
            public Step Step;
        }

        private readonly Equipment _body;
        private readonly EquipController _behavior;
        private readonly Action<ItemAcquiredEvent> _publish;
        private Operation _active;
        private long _nextId;
        private long _version;
        private bool _busy;
        private bool _disposed;
        private bool _faulted;
        private bool _externalChange;
        private readonly Func<ItemDefinition, EquipTiming> _timingResolver;

        /// <summary>
        /// 归位提示（SlotStore_Design.md §6）：装备时"被整格取空"的那个槽，收起时优先放回它。
        /// 只在"收起目标 = 提示容器"时生效、用后即清；外部改动或会话结束即失效。
        /// 属"来源/去向"知识 → 归本层（EB 纪律：装备行为不认识来源与去向）。
        /// </summary>
        private (SlotContainer Store, SlotId Cell)? _returnHint;

        /// <summary>终局通知（SafeEvent 对象事件原语：异常隔离 + 订阅去重——替代 EventBus 的错配用法）。</summary>
        private readonly SafeEvent<OperationReceipt> _operationFinished = new();

        /// <summary>当前活动操作；空表示没有正在编排的转移意图。</summary>
        public OperationReceipt Current => _active?.Receipt;

        /// <summary>最近一次终局，供调试 HUD 和失败反馈读取。</summary>
        public OperationReceipt LastResult { get; private set; }

        /// <summary>每次已接纳操作只通知一次终局；不在此补发获得事件（SafeEvent 发布：异常隔离 + 订阅去重）。</summary>
        public event Action<OperationReceipt> OperationFinished
        {
            add => _operationFinished.Add(value);
            remove => _operationFinished.Remove(value);
        }

        public ItemOperationCoordinator(
            Equipment body, EquipController behavior,
            Action<ItemAcquiredEvent> publish = null,
            Func<ItemDefinition, EquipTiming> timingResolver = null)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));
            _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
            _publish = publish;
            _timingResolver = timingResolver ?? ResolveTimingFromContent;
            Sync();
            _body.SlotChanged += OnSlotChanged;
        }

        /// <summary>世界拾取：解析点按/长按路由，按实际容器提交量播报获得。</summary>
        public OperationReceipt RequestPickup(
            IWorldItemSource source, ItemDefinition item, int count, PickupIntent intent, IItemContainer bag)
        {
            return Begin(source, item, count, bag, bag, true, true, intent);
        }

        /// <summary>从指定容器拿出一单位；旧物去向由调用方指定，不产生获得播报。</summary>
        public OperationReceipt RequestEquip(
            IItemContainer source, ItemDefinition item, IItemContainer displacedDestination)
        {
            return Begin(source, item, 1, displacedDestination, null, true, false, PickupIntent.ForceHold);
        }

        /// <summary>收回完成后转入指定容器；失败保持 Stowed，G 可接管尚未提交的换物意图。</summary>
        public OperationReceipt RequestUnequip(IItemContainer destination)
        {
            if (Blocked)
            {
                return Reject("BusyOrPaused");
            }

            if (destination == null || ReferenceEquals(destination, _body))
            {
                return Reject("InvalidDestination");
            }

            if (_active != null && !_active.Equip && ReferenceEquals(_active.Destination, destination))
            {
                return _active.Receipt;
            }

            if (_body.IsEmpty(BodySlotType.Hand))
            {
                return Reject("NoItem");
            }

            _busy = true;
            try
            {
                // G 接管已存在的收回，或打断拿出；已提交占有不回滚。
                if (_active != null)
                {
                    Finish(OperationStatus.Cancelled, "ReplacedByUnequip");
                }

                _active = new Operation
                {
                    Receipt = NewReceipt(),
                    Destination = destination,
                    Step = Step.Stow
                };
                var receipt = _active.Receipt;
                Advance();
                return receipt;
            }
            finally
            {
                _busy = false;
            }
        }

        private bool Blocked => _disposed || _faulted || _busy || _behavior.Snapshot.Paused;

        private OperationReceipt Begin(
            IItemContainer source, ItemDefinition item, int count,
            IItemContainer destination, IItemContainer remainder,
            bool equip, bool pickup, PickupIntent intent)
        {
            if (Blocked)
            {
                return Reject("BusyOrPaused");
            }

            if (source == null || item == null || count <= 0 || destination == null ||
                ReferenceEquals(source, _body) || ReferenceEquals(destination, _body) ||
                (pickup && ReferenceEquals(source, destination)) ||
                (pickup && intent != PickupIntent.Tap && intent != PickupIntent.ForceHold))
            {
                return Reject("InvalidRequest");
            }

            if (_active != null)
            {
                if (ReferenceEquals(_active.Source, source) && ReferenceEquals(_active.Item, item) &&
                    ReferenceEquals(_active.Destination, destination) && _active.Count == count &&
                    _active.Pickup == pickup && _active.Intent == intent)
                {
                    return _active.Receipt;
                }

                return Reject("Busy");
            }

            if (_behavior.Snapshot.Phase == EquipPhase.Stowing)
            {
                return Reject("Busy");
            }

            var world = source as IWorldItemSource;
            if ((pickup && world == null) || (world != null && !world.Available) || source.CountOf(item) < count)
            {
                return Reject("SourceUnavailable");
            }

            var held = _body.Get(BodySlotType.Hand);
            var accepts = _body.SlotAccepts(BodySlotType.Hand, item);
            if (!pickup && !accepts)
            {
                return Reject("NotHoldable");
            }

            // 不可持物和已持同物的长按直接入包，不触发旧物收回。
            equip = accepts && (!pickup ||
                (intent == PickupIntent.Tap ? held == null : !ReferenceEquals(held, item)));
            _busy = true;
            try
            {
                var receipt = NewReceipt();
                if (world != null && !world.TryAcquire(receipt.Id))
                {
                    return Reject("SourceBusy");
                }

                _active = new Operation
                {
                    Receipt = receipt,
                    Source = source,
                    Destination = destination,
                    Remainder = remainder,
                    Item = item,
                    Count = count,
                    Equip = equip,
                    Pickup = pickup,
                    Intent = intent,
                    World = world,
                    Step = equip && held != null ? Step.Stow : Step.Transfer
                };
                Advance();
                return receipt;
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>同步占用异常、推进当前行为，再尝试执行已满足条件的转移步骤。</summary>
        public void Tick(float delta)
        {
            if (_disposed || _busy)
            {
                return;
            }

            _busy = true;
            try
            {
                if (_externalChange || !ReferenceEquals(_body.Get(BodySlotType.Hand), _behavior.Snapshot.Item))
                {
                    _externalChange = false;
                    _returnHint = null; // 外部改动 → 归位提示失效
                    Sync();
                    if (_active != null)
                    {
                        Finish(OperationStatus.Failed, "ExternalOccupancyChange");
                    }
                }

                if (_active?.World != null && !_active.World.Available && _active.Step != Step.Draw)
                {
                    Finish(OperationStatus.Failed, "SourceUnavailable");
                }

                _behavior.Tick(delta);
                if (!_behavior.Snapshot.Paused)
                {
                    Advance();
                }
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>
        /// 单次操作按 Stow → Transfer → Draw 推进；只有等待行为时才跨帧。
        /// 旧物转出和新物转入是两个提交点，新源失效不会撤销已提交的旧物去向。
        /// </summary>
        private void Advance()
        {
            if (_active == null)
            {
                return;
            }

            var operation = _active;
            try
            {
                if (operation.Step == Step.Stow)
                {
                    _behavior.RequestStow();
                    if (!_behavior.CanTransferOut)
                    {
                        return;
                    }

                    if (!StowHeld(operation))
                    {
                        Finish(OperationStatus.Failed, "DestinationRejected");
                        return;
                    }

                    operation.Receipt.OldItemTransferred = true;
                    if (operation.Source == null)
                    {
                        Finish(OperationStatus.Completed);
                        return;
                    }

                    operation.Step = Step.Transfer;
                }

                if (operation.Step == Step.Transfer)
                {
                    if ((operation.World != null && !operation.World.Available) || operation.Source.CountOf(operation.Item) < operation.Count)
                    {
                        Finish(OperationStatus.Failed, "SourceUnavailable");
                        return;
                    }

                    int remaining = operation.Count;
                    if (operation.Equip)
                    {
                        var hint = FindCellToEmpty(operation.Source, operation.Item, 1);
                        if (!_body.IsEmpty(BodySlotType.Hand) || !Move(operation.Source, _body, operation.Item, 1))
                        {
                            Finish(OperationStatus.Failed, "HandRejected");
                            return;
                        }

                        _returnHint = hint; // 归位提示：仅"整格取出"有意义（同类堆叠不记）
                        if (operation.Pickup)
                        {
                            operation.Receipt.AcquiredCount++;
                        }

                        remaining--;
                    }

                    if (remaining > 0)
                    {
                        if (!Move(operation.Source, operation.Remainder ?? operation.Destination, operation.Item, remaining))
                        {
                            Finish(OperationStatus.Failed, "RemainderRejected");
                            return;
                        }

                        if (operation.Pickup)
                        {
                            operation.Receipt.AcquiredCount += remaining;
                        }
                    }

                    PublishAcquired(operation);
                    ReleaseSource(operation);
                    if (!operation.Equip)
                    {
                        Finish(OperationStatus.Completed);
                        return;
                    }

                    operation.Step = Step.Draw;
                    _behavior.RequestDraw();
                }

                if (operation.Step == Step.Draw && _behavior.Snapshot.Phase == EquipPhase.Ready)
                {
                    Finish(OperationStatus.Completed);
                }
                else if (operation.Step == Step.Draw && _behavior.Snapshot.Phase != EquipPhase.Drawing)
                {
                    Finish(OperationStatus.Cancelled, "DrawInterrupted");
                }
            }
            catch (Exception e)
            {
                // 包括回滚失败：禁止后续业务写入，保留现场供诊断。
                _faulted = true;
                Sync();
                Finish(OperationStatus.Failed, "ConsistencyFault: " + e.Message);
                XeptKit.Core.Log.Error($"[ItemOperation] 转移一致性故障：{e}");
            }
        }

        /// <summary>提交后才同步最终占用，避免移除/添加/回滚的中间通知触发动画。</summary>
        private bool Move(IItemContainer source, IItemContainer destination, ItemDefinition item, int count)
        {
            var result = ContainerTransfer.Move(source, destination, item, count);
            if (result && (ReferenceEquals(source, _body) || ReferenceEquals(destination, _body)))
            {
                Sync();
            }

            return result;
        }

        private void Sync()
        {
            var item = _body.Get(BodySlotType.Hand);
            _behavior.ReconcileOccupancy(item, ++_version, _timingResolver(item));
        }

        /// <summary>
        /// 收起手槽物品：优先"归位"到装备时的原格（尽力而为），失败落回常规分配（SlotStore_Design.md §6）。
        /// </summary>
        private bool StowHeld(Operation operation)
        {
            var held = _body.Get(BodySlotType.Hand);
            var hint = _returnHint;
            _returnHint = null; // 提示一次性：无论成功与否都消费掉

            if (hint is { } target && ReferenceEquals(target.Store, operation.Destination)
                && ContainerTransfer.MoveAt(_body, target.Store, held, 1, target.Cell))
            {
                return true;
            }

            return Move(_body, operation.Destination, held, 1);
        }

        /// <summary>
        /// 归位提示取值：源容器中"恰好一格、且本次取走会清空它"的槽。
        /// 多候选（同类多格）或源不是槽容器（世界堆/行容器）→ 不记提示：同类物品的位置本无意义，并入才是正确行为。
        /// </summary>
        private static (SlotContainer Store, SlotId Cell)? FindCellToEmpty(IItemContainer source, ItemDefinition item, int count)
        {
            var store = source as SlotContainer;
            if (store == null)
            {
                return null;
            }

            SlotId? found = null;
            for (int i = 0; i < store.Slots.Count; i++)
            {
                var slot = store.Slots[i];
                if (!ReferenceEquals(slot.Item, item) || slot.Count != count)
                {
                    continue;
                }

                if (found != null)
                {
                    return null; // 多个候选：位置无意义
                }

                found = slot.Id;
            }

            return found.HasValue ? (store, found.Value) : ((SlotContainer, SlotId)?)null;
        }

        /// <summary>默认时长解析：读物品能力面配置（HoldableFacet → HoldableFacetProfile.ResolvedTiming）；无配置回退默认。测试可注入恒值。</summary>
        private static EquipTiming ResolveTimingFromContent(ItemDefinition item)
            => item?.GetFacet<HoldableFacet>()?.profile?.ResolvedTiming ?? EquipTiming.Default;

        private void OnSlotChanged(SlotChangeArgs change)
        {
            if (!_busy)
            {
                _externalChange = true;
            }
        }

        private OperationReceipt NewReceipt() => new()
        {
            Id = ++_nextId,
            Status = OperationStatus.Pending
        };
        private static OperationReceipt Reject(string reason) => new()
        {
            Status = OperationStatus.Rejected,
            Reason = reason
        };
        /// <summary>唯一获得播报点；先记录已播报，再通知观察者以防重入或终局重复。</summary>
        private void PublishAcquired(Operation operation)
        {
            if (!operation.Pickup || operation.Published || operation.Receipt.AcquiredCount <= 0)
            {
                return;
            }

            operation.Published = true;
            // 获得播报 = 装配接缝直调（Context 转发到会话域总线；回调由装配点保证不抛）。
            _publish?.Invoke(new ItemAcquiredEvent(operation.Item, operation.Receipt.AcquiredCount));
        }

        private static void ReleaseSource(Operation operation)
        {
            if (operation.World == null)
            {
                return;
            }

            var source = operation.World;
            operation.World = null;
            source.Release(operation.Receipt.Id);
            source.RefreshView();
        }

        private void Finish(OperationStatus status, string reason = null)
        {
            var operation = _active;
            if (operation == null)
            {
                return;
            }

            PublishAcquired(operation);
            ReleaseSource(operation);
            operation.Receipt.Status = status;
            operation.Receipt.Reason = reason;
            LastResult = operation.Receipt;
            _active = null;
            _operationFinished.Invoke(operation.Receipt);
        }

        /// <summary>取消后续操作意图，不撤销已提交占用；正在收回的行为继续到 Stowed。</summary>
        public bool CancelOperation(long id)
        {
            if (_busy || _active == null || _active.Receipt.Id != id)
            {
                return false;
            }

            _busy = true;
            try
            {
                Finish(OperationStatus.Cancelled, "Cancelled");
                return true;
            }
            finally
            {
                _busy = false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _busy = true;
            try
            {
                _body.SlotChanged -= OnSlotChanged;
                _returnHint = null;
                Finish(OperationStatus.Cancelled, "SessionEnded");
                _operationFinished.Clear();
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
