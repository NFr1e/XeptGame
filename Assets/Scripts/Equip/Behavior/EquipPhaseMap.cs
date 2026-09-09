using System;

namespace XeptGame.Equip
{
    /// <summary>
    /// 相位 ⇄ Fsm 状态类的<b>集中映射单表</b>：EquipController 的散落 switch（RequestState / IsIn）收口于此。
    /// <list type="bullet">
    /// <item>加相位 / 改归属 = 只改 <see cref="Entries"/> 一行 + EquipRequestTable 矩阵一行 + 状态类本身，
    /// 控制器不再维护双向 switch；</item>
    /// <item>相位唯一权威仍为 Fsm 状态类；本表只是"相位名 ↔ 状态类型"的翻译层（enum 为对外稳定视图）；</item>
    /// <item>未知相位/类型 = 编程错误（抛 ArgumentException），不做中性默认。</item>
    /// </list>
    /// </summary>
    public static class EquipPhaseMap
    {
        private static readonly (EquipPhase Phase, Type State)[] Entries =
        {
            (EquipPhase.Empty, typeof(EmptyState)),
            (EquipPhase.Stowed, typeof(StowedState)),
            (EquipPhase.Drawing, typeof(DrawingState)),
            (EquipPhase.Ready, typeof(ReadyState)),
            (EquipPhase.Stowing, typeof(StowingState)),
        };

        /// <summary>相位 → Fsm 状态类型（转移请求用；同态转移幂等由 Fsm 处理）。</summary>
        public static Type ToState(EquipPhase phase)
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].Phase == phase)
                {
                    return Entries[i].State;
                }
            }

            throw new ArgumentException($"未知装备相位：{phase}", nameof(phase));
        }

        /// <summary>Fsm 状态类型 → 相位（快照/规则键推导用）。</summary>
        public static EquipPhase FromState(Type stateType)
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].State == stateType)
                {
                    return Entries[i].Phase;
                }
            }

            throw new ArgumentException($"未知装备状态类型：{stateType?.Name}", nameof(stateType));
        }
    }
}
