using System;
using XeptGame.Container;
using XeptGame.Equip;
using XeptGame.Inv;
using XeptGame.World;
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
            public IWorldSource World;
            public ItemDefinition Item;
            public int Count;
            public bool Equip;
            public bool Pickup;
            public bool Published;
            public bool DroppedRemainder;
            public PickupIntent Intent;
            public Step Step;
        }

        private readonly Equipment _body;
        private readonly EquipController _behavior;
        private readonly Action<ItemAcquiredEvent> _publish;

        /// <summary>使用播报接缝（Backpack_UI_Design.md B2；与获得播报同层同纪律）。</summary>
        private readonly Action<ItemConsumedEvent> _publishConsumed;
        private Operation _active;
        private long _nextId;
        private long _version;
        private bool _busy;
        private bool _disposed;
        private bool _faulted;
        private bool _externalChange;
        private readonly Func<ItemDefinition, EquipTiming> _timingResolver;

        /// <summary>记录层删除钩子（拾取世界实例成功后调用；装配点接会话的 WorldRecordStore）。</summary>
        private readonly Func<long, bool> _removeRecord;

        /// <summary>旧包去向 / 世界掉落口（换包交接与"收起失败落地"共用；惰性取值以容纳"会话先建、世界层后装配"）。</summary>
        private readonly Func<IWorldDropPort> _worldDrop;

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
            Func<ItemDefinition, EquipTiming> timingResolver = null,
            Func<long, bool> removeRecord = null,
            Func<IWorldDropPort> worldDrop = null,
            Action<ItemConsumedEvent> publishConsumed = null)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));
            _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
            _publish = publish;
            _publishConsumed = publishConsumed;
            _timingResolver = timingResolver ?? ResolveTimingFromContent;
            _removeRecord = removeRecord;
            _worldDrop = worldDrop;
            Sync();
            _body.SlotChanged += OnSlotChanged;
        }

        /// <summary>
        /// 世界拾取：解析点按/长按路由，按实际容器提交量播报获得。
        /// <b>无包态</b>（背槽为空 → 会话的 <c>Inventory</c> 为 null）按 intent 分流：
        /// <list type="bullet">
        /// <item><b>点按（Tap）</b>整批入包 → 无包必失败，拒绝 <c>NoBag</c>；</item>
        /// <item><b>长按（ForceHold）</b>是"先上一手、余量入包"→ 无包时<b>不受影响</b>：一单位到手，
        /// 余量留在世界源里（Hand 槽与背包解耦，Item_Instance_Design.md §3）。</item>
        /// </list>
        /// </summary>
        public OperationReceipt RequestPickup(
            IWorldSource source, ItemDefinition item, int count, PickupIntent intent, IItemContainer bag)
        {
            if (bag == null && intent != PickupIntent.ForceHold)
            {
                return Reject("NoBag");
            }

            return Begin(source, item, count, bag, bag, true, true, intent);
        }

        /// <summary>
        /// 换包（Item_Instance_Design.md §4）：把背槽里的背包实例换成 <paramref name="incoming"/>，
        /// 旧包<b>整包</b>交给 <paramref name="destination"/>（内容不动、不拆不丢）。
        /// <list type="bullet">
        /// <item><b>只换引用</b>：两个实例各自的容器内容都不迁移（"换包 = 换实例"）；</item>
        /// <item><b>目的地必需</b>（DP6）：背槽已有旧包而目的地为 null → 拒绝（WorldDrop 接线前不静默丢包）；</item>
        /// <item><b>顺序 = 先摘旧 → 放新 → 最后交接旧包</b>：交接放最后，失败可完整回滚（摘回新包、放回旧包），
        /// 不会出现"旧包已被接收、新包又没上身"的半换状态；</item>
        /// <item>背槽本来就空 = 直接背上（"无包 → 有包"）；同一实例已在背上 = 无操作成功。</item>
        /// </list>
        /// 同步完成（不参与装备 FSM 的 Draw/Stow 时序）：换包只动背槽一个格，且手槽占用与之无关。
        /// </summary>
        public OperationReceipt RequestSwapCarrier(ContainerInstance incoming, ICarrierDestination destination)
        {
            if (Blocked)
            {
                return Reject("BusyOrPaused");
            }

            if (incoming == null)
            {
                return Reject("NoCarrier");
            }

            var backCell = new SlotId((int)BodySlotType.Back);
            var current = _body.GetInstance(BodySlotType.Back) as ContainerInstance;

            if (current == null)
            {
                _busy = true;
                try
                {
                    return _body.TryPlaceInstanceAt(backCell, incoming)
                        ? Complete("CarrierEquipped")
                        : Reject("CarrierPlacementFailed");
                }
                finally
                {
                    _busy = false;
                }
            }

            if (ReferenceEquals(current, incoming))
            {
                return Complete("AlreadyEquipped");
            }

            if (destination == null)
            {
                return Reject("NoCarrierDestination");
            }

            _busy = true;
            try
            {
                // 1) 先摘旧：此刻背槽为空、旧包在手上——任何后续失败都能放回，不会丢
                //（背槽只有一个实例，取出的就是 current；用 current 是同一对象且保留了容器类型）
                if (!_body.TryTakeInstanceAt(backCell, out _))
                {
                    return Reject("CarrierTakeFailed");
                }

                // 2) 放新
                if (!_body.TryPlaceInstanceAt(backCell, incoming))
                {
                    return RollbackCarrierSwap(backCell, current, "CarrierPlacementFailed");
                }

                // 3) 最后交接旧包（目的地在最后一步，失败即整体回滚）
                if (destination.TryAccept(current, out var reason))
                {
                    return Complete("CarrierSwapped");
                }

                if (!_body.TryTakeInstanceAt(backCell, out _))
                {
                    throw new InvalidOperationException("换包回滚失败：新包无法摘回，必须停止后续操作并检查占用事实。");
                }

                return RollbackCarrierSwap(backCell, current, string.IsNullOrEmpty(reason) ? "DestinationRejected" : reason);
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>换包失败回滚：把旧包放回背槽；放不回即视为占用事实被破坏（抛错停止）。</summary>
        private OperationReceipt RollbackCarrierSwap(SlotId backCell, ItemInstance previous, string reason)
        {
            if (!_body.TryPlaceInstanceAt(backCell, previous))
            {
                throw new InvalidOperationException("换包回滚失败：旧包无法放回背槽，必须停止后续操作并检查占用事实。");
            }

            return Reject(reason);
        }

        /// <summary>
        /// 拾取世界上的<b>背包实例</b>（Item_Instance_Design.md §4/§6）——<b>按 intent 分流</b>：
        /// <list type="bullet">
        /// <item><b>点按</b>：背槽空 → 整体背上；<b>背槽已占 → 装进当前背包</b>（"包中放包"合法，
        /// 背槽只是"同时只戴一个"，不是"只能拥有一个"）；当前背包没空格 → <c>BagFull</c>；</item>
        /// <item><b>长按</b>：这是<b>换包</b>动作 → 需要旧包的去向（DP6），WorldDrop 接线前一律 <c>NoCarrierDestination</c>；</item>
        /// <item>任一路径放置失败都<b>原样放回源</b>（不丢实例，I1/I7）；成功后请求记录层删记录 + 获得播报一次。</item>
        /// </list>
        /// </summary>
        public OperationReceipt RequestPickupCarrier(IWorldCarrierSource source, PickupIntent intent)
        {
            if (Blocked)
            {
                return Reject("BusyOrPaused");
            }

            if (source == null || !source.Available || !source.HasContent)
            {
                return Reject("SourceUnavailable");
            }

            if (intent != PickupIntent.Tap && intent != PickupIntent.ForceHold)
            {
                return Reject("InvalidRequest");
            }

            var backCell = new SlotId((int)BodySlotType.Back);
            var worn = _body.GetInstance(BodySlotType.Back) as ContainerInstance;

            // 长按 + 背槽已占 = 换包：旧包必须有去向（DP6）；去向未接线时明确拒绝（不静默丢包）
            var swapping = worn != null && intent == PickupIntent.ForceHold;
            var destination = swapping ? _worldDrop?.Invoke() : null;
            if (swapping && destination == null)
            {
                return Reject("NoCarrierDestination");
            }

            var receipt = NewReceipt();
            _busy = true;
            try
            {
                if (!source.TryAcquire(receipt.Id))
                {
                    return Reject("SourceBusy");
                }

                try
                {
                    if (!source.TryTakeCarrier(out var carrier) || carrier == null)
                    {
                        return Reject("SourceUnavailable");
                    }

                    if (worn == null)
                    {
                        // 无包 → 背上
                        if (!_body.TryPlaceInstanceAt(backCell, carrier))
                        {
                            return ReturnAndReject(source, carrier, "CarrierPlacementFailed");
                        }
                    }
                    else if (!swapping)
                    {
                        // 点按 + 已有包 → 装进当前背包（多一个背包是合法的）
                        if (!worn.Store.TryPlaceInstance(carrier))
                        {
                            return ReturnAndReject(source, carrier, "BagFull");
                        }
                    }
                    else
                    {
                        // 换包：先摘旧 → 放新 → **最后**交接旧包（失败可完整回滚，不留半换状态）
                        if (!_body.TryTakeInstanceAt(backCell, out _))
                        {
                            return ReturnAndReject(source, carrier, "CarrierTakeFailed");
                        }

                        if (!_body.TryPlaceInstanceAt(backCell, carrier))
                        {
                            if (!_body.TryPlaceInstanceAt(backCell, worn))
                            {
                                throw new InvalidOperationException("换包回滚失败：旧包无法放回背槽，必须停止后续操作并检查占用事实。");
                            }

                            return ReturnAndReject(source, carrier, "CarrierPlacementFailed");
                        }

                        if (!destination.TryAccept(worn, out var reason))
                        {
                            if (!_body.TryTakeInstanceAt(backCell, out _))
                            {
                                throw new InvalidOperationException("换包回滚失败：新包无法摘回，必须停止后续操作并检查占用事实。");
                            }

                            if (!_body.TryPlaceInstanceAt(backCell, worn))
                            {
                                throw new InvalidOperationException("换包回滚失败：旧包无法放回背槽，必须停止后续操作并检查占用事实。");
                            }

                            return ReturnAndReject(source, carrier, string.IsNullOrEmpty(reason) ? "DestinationRejected" : reason);
                        }
                    }

                    if (source.RecordId != 0 && _removeRecord != null && !_removeRecord(source.RecordId))
                    {
                        XeptKit.Core.Log.Warning($"[ItemOperation] 世界背包已收纳，但记录删除失败：record {source.RecordId}（需人工核对记录层）。");
                    }

                    _publish?.Invoke(new ItemAcquiredEvent(carrier.Definition, 1));
                    return Complete(worn == null ? "CarrierPickedUp" : swapping ? "CarrierSwapped" : "CarrierStowedInBag");
                }
                finally
                {
                    source.Release(receipt.Id);
                    source.RefreshView();
                }
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>把实例原样放回世界源，再返回拒绝回执（世界源路径上不得丢实例）。</summary>
        private static OperationReceipt ReturnAndReject(IWorldCarrierSource source, ContainerInstance carrier, string reason)
        {
            if (!source.TryReturnCarrier(carrier))
            {
                throw new InvalidOperationException("世界背包放置失败且无法放回源，必须停止后续操作并检查占用事实。");
            }

            return Reject(reason);
        }

        /// <summary>从指定容器拿出一单位；旧物去向由调用方指定，不产生获得播报。</summary>
        public OperationReceipt RequestEquip(
            IItemContainer source, ItemDefinition item, IItemContainer displacedDestination)
        {
            return Begin(source, item, 1, displacedDestination, null, true, false, PickupIntent.ForceHold);
        }

        /// <summary>
        /// 收回完成后转入指定容器；<paramref name="destination"/> 为 <c>null</c> = <b>没有容器可收</b>
        /// （无包态误拾后按 G）→ 走"放到世界"的落地路径（与"背包满"同一条路：有掉落口就落地，
        /// 没有则失败且东西留在手上，绝不静默丢）。失败保持 Stowed，G 可接管尚未提交的换物意图。
        /// </summary>
        public OperationReceipt RequestUnequip(IItemContainer destination)
        {
            if (Blocked)
            {
                return Reject("BusyOrPaused");
            }

            if (ReferenceEquals(destination, _body))
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

        /// <summary>
        /// 使用（Backpack_UI_Design.md B2）：从 <paramref name="source"/> 扣掉 <paramref name="count"/> 个单位，
        /// 成功后发一次<b>会话域</b>使用播报。<b>效果域后置</b>——v1 只扣数与播报，"吃了回血/喝了解渴"
        /// 由将来的效果域订阅 <see cref="ItemConsumedEvent"/> 实现。
        /// <list type="bullet">
        /// <item>按<b>定义</b>寻址（"用掉一个这东西"本就与格子无关）；未挂 <see cref="ConsumableFacet"/> → <c>NotConsumable</c>；</item>
        /// <item>实例行不可消费（有状态载荷不参与按定义扣减，不变量 I2）→ 一并归入 <c>NotConsumable</c> 之外的 <c>ItemNotFound</c>；</item>
        /// <item><b>同步命令</b>（不占活动操作位，与 <see cref="RequestSwapCarrier"/> 同一形态）：失败零改动，成功即终局。</item>
        /// </list>
        /// 界面主路径用<b>格寻址</b>重载（见下）——按定义扣减会从<b>槽序最靠前</b>的同物格开始扣，
        /// 界面上"选中后格却扣了前格"就是这个差异造成的。
        /// </summary>
        public OperationReceipt RequestConsume(SlotContainer source, ItemDefinition item, int count)
            => ConsumeCore(source, null, item, count);

        /// <summary>
        /// 使用（<b>格寻址</b>；背包界面主路径）：<b>只从选中的那一格扣</b>，且以格内实际内容为准
        /// （不信任调用方给的物品）。空格 / 实例行 → 拒绝（实例行不是"能吃掉的东西"）。
        /// </summary>
        public OperationReceipt RequestConsume(SlotContainer source, SlotId cell, int count)
            => ConsumeCore(source, cell, null, count);

        /// <summary>使用单一实现：两种情况都先解析出"扣什么、扣几个"，再走同一条扣减与播报。</summary>
        private OperationReceipt ConsumeCore(SlotContainer source, SlotId? cell, ItemDefinition item, int count)
        {
            if (Blocked)
            {
                return Reject("BusyOrPaused");
            }

            if (source == null || count <= 0)
            {
                return Reject("InvalidRequest");
            }

            SlotBase slot = null;
            if (cell.HasValue)
            {
                slot = FindSlotById(source, cell.Value);
                if (slot == null || slot.IsEmpty || slot.HasInstance)
                {
                    return Reject("NotConsumable");
                }

                item = slot.Item;
                count = Math.Min(count, slot.Count);
            }

            if (item == null)
            {
                return Reject("InvalidRequest");
            }

            if (!item.HasFacet<ConsumableFacet>())
            {
                return Reject("NotConsumable");
            }

            _busy = true;
            try
            {
                var removed = cell.HasValue
                    ? source.TryRemoveAt(cell.Value, count)
                    : source.TryRemove(item, count);
                if (!removed)
                {
                    return Reject("ItemNotFound");
                }

                _publishConsumed?.Invoke(new ItemConsumedEvent(item, count));
                return Complete("Consumed");
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>
        /// 丢弃（Backpack_UI_Design.md B2；定义寻址）：<b>先从容器移除 → 再交世界掉落口落成记录 → 失败原样放回容器</b>
        /// （沿既有"先移除、再落地、失败回滚"顺序纪律，绝不出现"既在背包又在世界"）。
        /// 界面主路径用<b>格寻址</b>重载（能丢实例行）；本重载供无格上下文的调用方使用。
        /// <para>
        /// <b>为什么源类型是 <see cref="SlotContainer"/> 而不是 <see cref="IItemContainer"/></b>：
        /// 这两个动作都由<b>格界面</b>发起——丢弃必须按格寻址才有"丢哪一个实例行"的语义，
        /// 而 <c>IItemContainer</c> 是"物品面"端口、没有格的概念，承载不了它。
        /// </para>
        /// </summary>
        public OperationReceipt RequestDrop(SlotContainer source, ItemDefinition item, int count)
            => DropCore(source, null, item, count);

        /// <summary>
        /// 丢弃（<b>格寻址</b>；背包界面主路径）：以格内<b>实际内容</b>为准（不信任调用方给的物品），
        /// <b>只从选中的那一格移除</b>，因而<b>能丢实例行</b>（"丢掉这个背包"走整包落地）——
        /// 这两件事定义寻址都做不到（它从槽序最靠前的同物格开始扣）。
        /// </summary>
        public OperationReceipt RequestDrop(SlotContainer source, SlotId cell, int count)
            => DropCore(source, cell, null, count);

        /// <summary>
        /// 丢弃单一实现。两条载荷各走各的落地形态：无状态行 → <c>TryAcceptStack</c>（一堆，一条记录）；
        /// 实例行 → <c>TryAccept</c>（整包，携带同一实例）。两者都是"先取出来、落地失败就原样放回"。
        /// </summary>
        private OperationReceipt DropCore(SlotContainer source, SlotId? cell, ItemDefinition item, int count)
        {
            if (Blocked)
            {
                return Reject("BusyOrPaused");
            }

            if (source == null || count <= 0)
            {
                return Reject("InvalidRequest");
            }

            var port = _worldDrop?.Invoke();
            if (port == null)
            {
                return Reject("NoWorldDrop");
            }

            // 格寻址：以槽为真相（调用方说的"里面是什么"不作数）
            SlotBase slot = null;
            if (cell.HasValue)
            {
                slot = FindSlotById(source, cell.Value);
                if (slot == null || slot.IsEmpty)
                {
                    return Reject("CellEmpty");
                }

                item = slot.Item;
                count = Math.Min(count, slot.Count);
            }

            if (item == null)
            {
                return Reject("InvalidRequest");
            }

            _busy = true;
            try
            {
                // 实例行（有状态载荷，数量恒 1）：整包落地（I1/I7——实例只能有一个位置）
                if (slot is { HasInstance: true })
                {
                    // 世界掉落口的实例形态只认容器实例（ICarrierDestination 的载荷形状）。
                    // 当前唯一会产生实例行的载荷就是容器（Item_Instance_Design.md §2.2）；出现别种实例时在此扩展。
                    if (slot.Instance is not ContainerInstance carrier)
                    {
                        return Reject("UnsupportedInstanceKind");
                    }

                    var store = source;
                    if (!store.TryTakeInstanceAt(cell.Value, out var taken))
                    {
                        return Reject("ItemNotFound");
                    }

                    if (port.TryAccept(carrier, out var carrierReason))
                    {
                        return Complete("Dropped");
                    }

                    if (!store.TryPlaceInstanceAt(cell.Value, taken))
                    {
                        throw new InvalidOperationException("丢弃失败且无法放回原格，必须停止后续操作并检查占用事实。");
                    }

                    return Reject(string.IsNullOrEmpty(carrierReason) ? "DropRejected" : carrierReason);
                }

                if (cell.HasValue)
                {
                    if (!source.TryRemoveAt(cell.Value, count))
                    {
                        return Reject("ItemNotFound");
                    }
                }
                else if (!source.TryRemove(item, count))
                {
                    return Reject("ItemNotFound");
                }

                if (port.TryAcceptStack(item, count, out var reason))
                {
                    return Complete("Dropped");
                }

                // 掉落被拒 → 原样放回（不丢、不复制）。**格寻址必须放回原格**：
                // 用 TryAdd 会把它塞进"最靠前的可用格"，于是"丢后格、失败"会变成"东西跑到前格去了"。
                var restored = cell.HasValue
                    ? source.TryPlaceAt(cell.Value, item, count)
                    : source.TryAdd(item, count);
                if (!restored)
                {
                    throw new InvalidOperationException("丢弃失败且无法放回容器，必须停止后续操作并检查占用事实。");
                }

                return Reject(string.IsNullOrEmpty(reason) ? "DropRejected" : reason);
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>按槽身份查格（容器自带的是 protected，协调器是外部消费者，故本地实现）。</summary>
        private static SlotBase FindSlotById(SlotContainer store, SlotId cell)
        {
            for (int i = 0; i < store.Slots.Count; i++)
            {
                if (store.Slots[i].Id == cell)
                {
                    return store.Slots[i];
                }
            }

            return null;
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

            // destination 允许为 null **仅限**"无包态的长按拿取"（分流点在本方法下方的 NoBag 分支）；
            // 其余调用（含 RequestEquip 的 displacedDestination）仍必须有明确去向。
            var bagless = destination == null;
            if (source == null || item == null || count <= 0 ||
                (bagless && !(pickup && intent == PickupIntent.ForceHold)) ||
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

            var world = source as IWorldSource;
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

            // 无包态没有"直接入包"这回事：手上已持**同一定义**时也必须按"换手"处理
            // （旧物落世界、新物到手）。否则"斧子换斧子"会被误判成无事可做而被拒。
            // 注意：无状态堆叠只按定义判等，所以"同一把"与"另一把同款"在这里无从区分，
            // 一律走换手——两者对玩家是同一件事（手上还是一把斧头，地上多一条记录）。
            if (bagless && pickup && accepts && intent == PickupIntent.ForceHold)
            {
                equip = true;
            }

            // 无包态：只要求"新物能进手"——进不了手就没有任何去处 → 明确拒绝。
            // 手上已有旧物时**不再拒绝**：旧物的去向 = 世界（"换手"语义，与按 G 收起同一条落地路径），
            // 由 Stow 步处理；世界掉落口缺失时该步失败，旧物原样留在手上（不静默丢）。
            if (bagless && !equip)
            {
                return Reject("NoBag");
            }

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
                        // 目的地拒绝（典型：背包满 / 无包）→ "收起 = 把手上的东西放走"：有世界掉落口就落地，
                        // 没有则维持原语义（失败且仍在手上，绝不静默丢）。
                        if (!TryStowHeldToWorld(operation))
                        {
                            Finish(OperationStatus.Failed, "DestinationRejected");
                            return;
                        }

                        operation.Receipt.OldItemTransferred = true;

                        // 纯收起（没有新东西要拿）→ 落地即终局
                        if (operation.Source == null)
                        {
                            Finish(OperationStatus.Completed, "StowedToWorld");
                            return;
                        }
                    }
                    else
                    {
                        operation.Receipt.OldItemTransferred = true;

                        if (operation.Source == null)
                        {
                            Finish(OperationStatus.Completed);
                            return;
                        }
                    }

                    // 还有新东西要拿（无包时的"换手"也走这里）→ 继续
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
                        var remainderTarget = operation.Remainder ?? operation.Destination;
                        if (remainderTarget != null)
                        {
                            if (!Move(operation.Source, remainderTarget, operation.Item, remaining))
                            {
                                // 余量没处放（典型：背包满）→ 落到世界的**一条堆叠记录**（数量记在记录里，
                                // 因此**一个视图**，不是 N 个预制体）。
                                // 顺序不可颠倒：**先从源把余量取出来**，再交给掉落口——否则源里仍留着这些单位，
                                // 世界又多出一份记录（同一批东西出现在两处 = 复制），源视图也就永远不消失。
                                var port = _worldDrop?.Invoke();
                                if (port == null || !operation.Source.TryRemove(operation.Item, remaining))
                                {
                                    Finish(OperationStatus.Failed, "RemainderRejected");
                                    return;
                                }

                                if (!port.TryAcceptStack(operation.Item, remaining, out _))
                                {
                                    // 掉落被拒 → 原样放回源（不丢、不复制）
                                    if (!operation.Source.TryAdd(operation.Item, remaining))
                                    {
                                        throw new InvalidOperationException("余量落地失败且无法放回源，必须停止后续操作并检查占用事实。");
                                    }

                                    Finish(OperationStatus.Failed, "RemainderRejected");
                                    return;
                                }

                                operation.DroppedRemainder = true;
                            }
                            else if (operation.Pickup)
                            {
                                operation.Receipt.AcquiredCount += remaining;
                            }
                        }

                        // remainderTarget == null：无包态的长按拿取 → 余量留在世界源里
                        //（已提交的是"拿到手的那一单位"，不报错、也不计入获得量）
                    }

                    PublishAcquired(operation);
                    ReleaseSource(operation);
                    if (!operation.Equip)
                    {
                        Finish(OperationStatus.Completed, operation.DroppedRemainder ? "RemainderDropped" : null);
                        return;
                    }

                    operation.Step = Step.Draw;
                    _behavior.RequestDraw();
                }

                if (operation.Step == Step.Draw && _behavior.Snapshot.Phase == EquipPhase.Ready)
                {
                    Finish(OperationStatus.Completed, operation.DroppedRemainder ? "RemainderDropped" : null);
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

        /// <summary>
        /// 收起失败时把手上物品落到世界（"背包满 → 丢地上"，Item_Instance_Design.md §5.3）。
        /// 顺序：先摘手（此刻东西在手上，摘下来才不丢）→ 请求掉落口落地 → 失败<b>原样放回手槽</b>。
        /// </summary>
        private bool TryStowHeldToWorld(Operation operation)
        {
            var port = _worldDrop?.Invoke();
            var held = _body.Get(BodySlotType.Hand);
            if (port == null || held == null)
            {
                return false;
            }

            if (!_body.TryRemove(held, 1))
            {
                return false;
            }

            if (port.TryAcceptStack(held, 1, out _))
            {
                Sync(); // 手槽已空 → 行为与呈现收敛到 Empty
                return true;
            }

            if (!_body.TryAdd(held, 1))
            {
                throw new InvalidOperationException("世界掉落失败且无法放回手槽，必须停止后续操作并检查占用事实。");
            }

            return false;
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

            // 没有容器收货（无包态）→ 不放回任何容器，交给调用方的"落地"分支处理
            if (operation.Destination == null)
            {
                return false;
            }

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

        /// <summary>
        /// 同步命令的完成回执（不经活动操作队列，例如换包）：记录终局、发一次终局通知。
        /// 与 <see cref="Finish"/> 的区别是它不消费 <c>_active</c>——同步命令从不占用活动操作位。
        /// </summary>
        private OperationReceipt Complete(string reason)
        {
            var receipt = NewReceipt();
            receipt.Status = OperationStatus.Completed;
            receipt.Reason = reason;
            LastResult = receipt;
            _operationFinished.Invoke(receipt);
            return receipt;
        }
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

        /// <summary>
        /// 释放世界源的占用并刷新视图；<b>源被取空时一并删掉它的记录</b>——
        /// 记录是真相、视图隐藏只是表现（否则记录表会留下 count=0 的僵尸记录）。
        /// </summary>
        private void ReleaseSource(Operation operation)
        {
            if (operation.World == null)
            {
                return;
            }

            var source = operation.World;
            operation.World = null;
            source.Release(operation.Receipt.Id);
            source.RefreshView();

            if (source.RecordId != 0 && !source.HasContent && _removeRecord != null && !_removeRecord(source.RecordId))
            {
                XeptKit.Core.Log.Warning($"[ItemOperation] 世界源已取空，但记录删除失败：record {source.RecordId}（需人工核对记录层）。");
            }
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
