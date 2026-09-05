namespace XeptGame.Player
{
    /// <summary>
    /// 蹲伏能力 Marker（结构性能力，无行为）：当前叶子状态是否允许"进入/保持蹲伏"动作。
    /// 由 <see cref="MotorActionDispatcher"/> 做准入判定：叶子实现 <see cref="ICrouchable"/>
    /// 且 <see cref="PlayerMotorContext.WantCrouch"/> → 在叶子所属机器内请求 CrouchState
    /// （实现类 Idle/Walk/Sprint/Crouch 均位于 Grounded 子机器，与目标 CrouchState 同机器，
    /// 故叶子用自身 <c>Fsm</c> 发起转移即正确；已处于 Crouch 时请求幂等跳过）。
    /// 边界：Marker 只表达结构性能力，动态行为留在状态内——蹲伏的保持/起身（WantCrouch 释放 +
    /// 站立尺寸 overlap 检查）在 <see cref="CrouchState.Update"/>，调度器不重复。
    /// </summary>
    public interface ICrouchable
    {
    }

    /// <summary>
    /// 接地跳跃能力 Marker（结构性能力，无行为）：当前叶子状态是否允许"接地跳"。
    /// 由 <see cref="MotorActionDispatcher"/> 做准入判定：叶子实现 <see cref="IJumpable"/>
    /// 且 <see cref="PlayerMotorInputState.JumpPressed"/> 边沿且 <see cref="PlayerMotorContext.JumpConsumed"/>
    /// 未消耗 → 执行统一"接地跳"（冲量 = up×jumpUpSpeed + 平台垂直、ForceUnground、置 JumpConsumed、
    /// 冒泡根机器转 Airborne）。
    /// 实现：Idle/Walk/Sprint。**Crouch 不实现**（蹲伏禁跳，需先起身）——以此替代原
    /// <c>!(Fsm.CurrentState is CrouchState)</c> 硬编码排除。
    /// 边界：Marker 只表达结构性能力——条件性跳跃不实现本接口、留各自状态内：
    /// Fall 土狼跳（<c>TimeSinceLastAbleToJump</c> 窗口）、UnstableGround 可滑面跳
    /// （<c>allowJumpingWhenUnstableGround</c>，冲量沿表面法线）。
    /// </summary>
    public interface IJumpable
    {
    }
}
