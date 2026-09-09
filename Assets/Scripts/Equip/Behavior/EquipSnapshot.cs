using System;
using XeptGame.Items;

namespace XeptGame.Equip
{
    /// <summary>
    /// 装备行为只读快照：身份（占用版本/动作号）用于拒绝过期数据，阶段与进度用于恢复呈现。
    /// 物品引用只表示当前占用，不赋予使用许可。
    /// </summary>
    public readonly struct EquipSnapshot
    {
        public readonly long OccupancyVersion;
        public readonly long ActionId;
        public readonly ItemDefinition Item;

        /// <summary>相位（由控制器从 Fsm 状态推导填入；消费者只读视图）。</summary>
        public readonly EquipPhase Phase;

        public readonly float Elapsed;
        public readonly float Duration;

        /// <summary>暂停标志（见 EquipContext.Paused）；呈现可据此冻结画面。</summary>
        public readonly bool Paused;

        /// <summary>当前动作归一化进度；零时长表示已经到达目标姿态。</summary>
        public float Progress => Duration <= 0 ? 1 : Math.Min(1, Elapsed / Duration);

        internal EquipSnapshot(EquipContext context, EquipPhase phase)
        {
            OccupancyVersion = context.Version;
            ActionId = context.ActionId;
            Item = context.Item;
            Phase = phase;
            Elapsed = context.Elapsed;
            Duration = context.Duration;
            Paused = context.Paused;
        }
    }
}
