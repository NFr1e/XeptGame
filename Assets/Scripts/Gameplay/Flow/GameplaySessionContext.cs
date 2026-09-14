using System;
using XeptGame.Container;
using XeptGame.Equip;
using XeptGame.Interaction;
using XeptGame.Inv;
using XeptGame.Items;
using XeptGame.Items.Operations;
using XeptGame.World;
using XeptKit.Core;
using XeptKit.Event;

namespace XeptGame.Game.Flow
{
    /// <summary>
    /// 一轮 GameplaySession 域根对象（GameplaySession_Domain_Design.md §3，R2 裁定）：
    /// 持<b>会话域事件总线</b> + <b>数据</b>（身体容器 = 身体槽占用；<b>当前背包由背槽推导、无包为 null</b>）
    /// + <b>一轮服务</b>（实例工厂 / 装备行为 / 操作编排）。
    /// <list type="bullet">
    /// <item><b>定位</b>：域根对象（会被 Tick 驱动），<b>非 FSM Context</b>——不适用 XeptKit.FSM 的 Context 纪律
    /// （该纪律约束 Game 域 FSM）；</item>
    /// <item><b>生命周期</b>：由 <see cref="GameplaySessionEntry"/> 创建/驱动/弃（一轮生一轮死）；
    /// 场景模块经 <c>GameplaySessionEntry.Instance.Context</c> 读取；</item>
    /// <item><b>暂停门控</b>：<see cref="SetPaused"/> 是唯一入口（Entry 每帧与 App/Game FSM 状态切换事件都调它）；
    /// 暂停 = 冻结装备行为（时钟/请求/转出），编排只做一致性巡检不推进；不清空占用与进度；</item>
    /// <item><b>总线归属</b>：会话内一次性播报（拾取获得等）走 <see cref="EventBus"/>；流程状态与上报仍走 Game 域
    /// （GameContext.EventBus），本对象不触 App 域；</item>
    /// <item><b>门面收口（后置）</b>：EquipBehaviour/Operations 目前为过渡公开成员；EB 质量返修时收口为
    /// <c>Context.Equip</c> 窄口门面。</item>
    /// </list>
    /// </summary>
    public sealed class GameplaySessionContext : ICarryFacts
    {
        /// <summary>当前是否暂停（域根自持，由 <see cref="SetPaused"/> 单一入口维护；默认暂停等待进入游玩）。</summary>
        private bool _paused = true;

        /// <summary>会话域事件总线（一轮生命周期，随一轮清空）——拾取获得等会话内业务事件走本总线。</summary>
        public EventBus EventBus { get; } = new();

        /// <summary>实例 id 签发器（会话内单调；存档层 T6 持久化 <c>NextInstanceId</c> 以续发）。</summary>
        public InstanceIdAllocator InstanceIds { get; } = new();

        /// <summary>实例工厂（<b>唯一创建实例的地方</b>：签发 id + 按 facet 装配容器 + 注入溢出出口）。</summary>
        public ItemInstanceFactory Instances { get; }

        /// <summary>
        /// 世界记录表（<b>记录层是真相</b>；Item_Instance_Design.md §5.1）：按关卡分组 + 条目上限 + 增删改事件。
        /// 会话持有它；场景视图只订阅，不直接增删记录。
        /// </summary>
        public WorldRecordStore WorldRecords { get; }

        /// <summary>世界掉落工厂（把"要落到世界的东西"变成记录；视图由视图生成器订阅记录表产生）。</summary>
        public WorldDropFactory WorldDropFactory { get; }

        /// <summary>
        /// 世界掉落口（换包交接 + 收起失败落地：Item_Instance_Design.md §4/§5.3）：
        /// 由<b>世界层场景壳</b>在会话建立后注入（它才知道玩家位置）；null = 未接线 → 换包拒绝、收起失败保持原语义（DP6）。
        /// </summary>
        public IWorldDropPort WorldDrop { get; set; }

        /// <summary>身体容器（手槽 + 背槽；占有 = 背包与身体分布，无总拥有）。</summary>
        public Equipment Equipment { get; } = new Equipment(new SlotBase[] { new HandSlot(), new BackSlot() });

        /// <summary>
        /// 当前背包实例（背槽里的容器实例；<b>无包 = null</b>，合法状态）。
        /// 由背槽<b>推导</b>、不缓存：换包后天然一致，不存在"第二份当前背包"。
        /// </summary>
        public ContainerInstance Bag => Equipment.GetInstance(BodySlotType.Back) as ContainerInstance;

        /// <summary>
        /// 当前背包的容器（<b>可空</b>）：名字沿用（DP2），语义变为"指向当前背包容器的引用"，
        /// 无包时为 null。占用事实仍在槽与实例上，本属性只是推导——所以不预建、不注入、不缓存。
        /// </summary>
        public Inventory Inventory => Bag?.Store;

        /// <summary>装备行为（手）：五态 FSM，不认识容器来源与去向（EB 决议）。</summary>
        public EquipController EquipBehaviour { get; private set; }

        /// <summary>操作编排（管家）：唯一认识来源/去向/路由与失败处理；广播经本总线（会话域）发出。</summary>
        public ItemOperationCoordinator Operations { get; private set; }

        /// <summary>
        /// 携带事实（v5 D6）：有没有背包 = 背槽里有没有容器实例。
        /// 交互宿主据此决定"拾取"这类需要去处的动作要不要出现在清单里。
        /// </summary>
        public bool HasBag => Bag != null;

        /// <summary>携带事实变化铃：**只在背槽变化时**发一次（手槽/背包内容变化不影响清单成员）。</summary>
        public event Action Changed;

        public GameplaySessionContext()
        {
            Instances = new ItemInstanceFactory(InstanceIds, OnOverflowDiscarded);
            WorldRecords = new WorldRecordStore(InstanceIds, 0, message => Log.Warning(message));
            WorldDropFactory = new WorldDropFactory(WorldRecords);
            EquipBehaviour = new EquipController();
            EquipBehaviour.SetPaused(true); // 默认暂停：域根刚建、未进 Playing 门控
            Operations = new ItemOperationCoordinator(
                Equipment, EquipBehaviour, PublishAcquired,
                removeRecord: WorldRecords.TryRemove,
                worldDrop: () => WorldDrop,
                publishConsumed: PublishConsumed);

            // 背槽变化 → 携带事实铃（换包/背上/摘下都会经槽级轨报出）
            Equipment.SlotChanged += OnBodySlotChanged;
        }

        /// <summary>身体槽变化 → 只把背槽的变化翻译成一次携带事实铃（其余槽与本事实无关）。</summary>
        private void OnBodySlotChanged(SlotChangeArgs args)
        {
            if (args.Slot.Value == (int)BodySlotType.Back)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// 暂停/恢复（唯一入口，幂等）：门控未开（非 Playing+Running）→ 暂停装备行为。
        /// <see cref="GameplaySessionEntry"/> 每帧与 FSM 状态切换事件都调本方法。
        /// </summary>
        public void SetPaused(bool paused)
        {
            if (_paused == paused)
            {
                return;
            }

            _paused = paused;
            EquipBehaviour?.SetPaused(paused);
        }

        /// <summary>帧驱动（由 <see cref="GameplaySessionEntry"/> 每帧转发）：暂停时编排只做一致性巡检（dt=0），不推进。</summary>
        public void Tick(float deltaTime)
            => Operations?.Tick(_paused ? 0f : deltaTime);

        /// <summary>会话域总线广播接缝：仅转发编排已决定的获得播报，不自行判断或补发（EB 单播纪律）。</summary>
        private void PublishAcquired(ItemAcquiredEvent acquired) => EventBus.Publish(acquired);

        /// <summary>会话域总线广播接缝（使用播报；Backpack_UI_Design.md B2）：与获得播报同纪律，只转发不补发。</summary>
        private void PublishConsumed(ItemConsumedEvent consumed) => EventBus.Publish(consumed);

        /// <summary>
        /// 背包实例的缩容溢出丢弃出口（SlotStore_Design.md §6；Item_Instance_Design.md §9.4）：
        /// v1 无世界 Drop，"静默消失"只指没有世界表现，数据上必须可见——聚合轨已按卸载发事件，这里补一条诊断；
        /// 后期世界 Drop 在同一接缝落地（⏳ T5：载荷升级为携带实例句柄，做到整包落地）。
        /// </summary>
        private static void OnOverflowDiscarded(ItemDefinition item, int count)
            => Log.Info($"[Inventory] 缩容溢出丢弃 {item?.Id} × {count}（世界 Drop 未实现，按卸载处理）");

        /// <summary>弃一轮：先终止操作/行为，再清引用（GameplaySession_Domain_Design.md R5）。</summary>
        public void Dispose()
        {
            Equipment.SlotChanged -= OnBodySlotChanged;
            Changed = null;

            Operations?.Dispose();
            Operations = null;
            EquipBehaviour?.Dispose();
            EquipBehaviour = null;
        }
    }
}
