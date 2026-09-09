namespace XeptGame.Equip
{
    /// <summary>装备行为请求（相位转移表输入之一）。</summary>
    public enum EquipRequest
    {
        Draw = 0,
        Stow = 1,
    }

    /// <summary>请求评估结果：即时答复 +（Started 时）目标相位。</summary>
    public readonly struct EquipRequestOutcome
    {
        public readonly BehaviorRequest Reply;
        public readonly EquipPhase? Target;

        public EquipRequestOutcome(BehaviorRequest reply, EquipPhase? target = null)
        {
            Reply = reply;
            Target = target;
        }
    }

    /// <summary>
    /// 行为请求转移表（纯逻辑，可单测）：行为文档 §3.2 状态转移表的数据化——"当前相位 × 请求 → 答复/目标"。
    /// 规则收口于此，EquipController 只做守卫短路与执行发布；相位 = Fsm 推导的枚举视图（EquipController.CurrentPhase）。
    /// </summary>
    public static class EquipRequestTable
    {
        public static EquipRequestOutcome Evaluate(EquipPhase from, EquipRequest request)
        {
            return request switch
            {
                EquipRequest.Draw => from switch
                {
                    EquipPhase.Stowed => new EquipRequestOutcome(BehaviorRequest.Started, EquipPhase.Drawing),
                    EquipPhase.Drawing => new EquipRequestOutcome(BehaviorRequest.AlreadyInProgress),// 不重置计时
                    EquipPhase.Ready => new EquipRequestOutcome(BehaviorRequest.AlreadySatisfied),// 已拿出
                    _ => new EquipRequestOutcome(BehaviorRequest.Rejected),// Empty（无物）/ Stowing（禁止反向打断）
                },
                EquipRequest.Stow => from switch
                {
                    EquipPhase.Empty or EquipPhase.Stowed => new EquipRequestOutcome(BehaviorRequest.AlreadySatisfied),// 已收回/空手
                    EquipPhase.Drawing => new EquipRequestOutcome(BehaviorRequest.Started, EquipPhase.Stowing),// 允许打断拿出
                    EquipPhase.Ready => new EquipRequestOutcome(BehaviorRequest.Started, EquipPhase.Stowing),
                    _ => new EquipRequestOutcome(BehaviorRequest.AlreadyInProgress),// Stowing 收回中
                },
                _ => default,
            };
        }
    }
}
