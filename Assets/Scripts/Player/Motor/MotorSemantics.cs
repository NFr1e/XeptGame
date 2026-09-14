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
    /// 注意：**SlideState 不实现本接口**——滑铲中蹲伏键保持为 true，若实现，调度器的蹲伏动作
    /// 会把滑铲立刻打断（Slide→Crouch 非同状态、非幂等）；滑铲的保持/退出由 SlideState 自行处理。
    /// </summary>
    public interface ICrouchable
    {
    }

    /// <summary>
    /// 接地跳跃能力 Marker（结构性能力，无行为）：当前叶子状态是否允许"接地跳"。
    /// 由 <see cref="MotorActionDispatcher"/> 做准入判定：叶子实现 <see cref="IJumpable"/>
    /// 且 <see cref="PlayerMotorInputState.JumpIntent"/> 边沿且 <see cref="PlayerMotorContext.JumpConsumed"/>
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

    /// <summary>
    /// 滑铲能力 Marker（结构性能力，无行为）：当前叶子状态是否允许"起滑"。
    /// 由 <see cref="MotorActionDispatcher"/> 做准入判定：叶子实现 <see cref="ISlidable"/>
    /// 且动态门槛 <see cref="PlayerMotorContext.CanStartSlide"/> 通过 → 在叶子所属机器内请求 SlideState
    /// （实现类 SprintState 与 SlideState 同处 Grounded 子机器）。门槛不过时**退化为蹲伏**
    /// （调度器继续评估蹲伏动作），避免低速滑铲的怪异手感。
    /// 实现：v1 仅 Sprint（冲刺档）——将来放宽"够快就能滑"只需给 WalkState 也标注。
    /// 边界：Marker 只表达结构性能力；动态门槛（想蹲 + 水平自主速度 ≥ 门槛）在 Context 谓词（§2.5）。
    /// 注意：**SlideState 不实现本接口**（否则滑铲中会重复请求自身）。
    /// </summary>
    public interface ISlidable
    {
    }
}
