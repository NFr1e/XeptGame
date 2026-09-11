using XeptGame.Container;
using XeptGame.Equip;
using XeptGame.Inv;
using XeptGame.Items;
using XeptGame.Items.Operations;
using XeptKit.Core;
using XeptKit.Event;

namespace XeptGame.Game.Flow
{
    /// <summary>
    /// 一轮 GameplaySession 域根对象（GameplaySession_Domain_Design.md §3，R2 裁定）：
    /// 持<b>会话域事件总线</b> + <b>数据</b>（背包行容器 / 身体容器，原纯数据 GameplaySession 并入）+ <b>一轮服务</b>
    /// （装备行为 / 操作编排）。
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
    public sealed class GameplaySessionContext
    {
        /// <summary>当前是否暂停（域根自持，由 <see cref="SetPaused"/> 单一入口维护；默认暂停等待进入游玩）。</summary>
        private bool _paused = true;

        /// <summary>会话域事件总线（一轮生命周期，随一轮清空）——拾取获得等会话内业务事件走本总线。</summary>
        public EventBus EventBus { get; } = new();

        /// <summary>背包行容器（原 GameplaySession 数据并入；可堆叠性的家）。</summary>
        public Inventory Inventory { get; } = new(discardSink: OnOverflowDiscarded);

        /// <summary>身体容器（手槽单位位，原 GameplaySession 数据并入；占有 = 背包与身体分布，无总拥有）。</summary>
        public Equipment Equipment { get; } = new Equipment(new SlotBase[] { new HandSlot() });

        /// <summary>装备行为（手）：五态 FSM，不认识容器来源与去向（EB 决议）。</summary>
        public EquipController EquipBehaviour { get; private set; }

        /// <summary>操作编排（管家）：唯一认识来源/去向/路由与失败处理；广播经本总线（会话域）发出。</summary>
        public ItemOperationCoordinator Operations { get; private set; }

        public GameplaySessionContext()
        {
            EquipBehaviour = new EquipController();
            EquipBehaviour.SetPaused(true); // 默认暂停：域根刚建、未进 Playing 门控
            Operations = new ItemOperationCoordinator(Equipment, EquipBehaviour, PublishAcquired);
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

        /// <summary>
        /// 背包缩容溢出丢弃出口（SlotStore_Design.md §6）：v1 无世界 Drop，"静默消失"只指没有世界表现，
        /// 数据上必须可见——聚合轨已按卸载发事件，这里补一条诊断；后期世界 Drop 在同一接缝落地。
        /// </summary>
        private static void OnOverflowDiscarded(ItemDefinition item, int count)
            => Log.Info($"[Inventory] 缩容溢出丢弃 {item?.Id} × {count}（世界 Drop 未实现，按卸载处理）");

        /// <summary>弃一轮：先终止操作/行为，再清引用（GameplaySession_Domain_Design.md R5）。</summary>
        public void Dispose()
        {
            Operations?.Dispose();
            Operations = null;
            EquipBehaviour?.Dispose();
            EquipBehaviour = null;
        }
    }
}
