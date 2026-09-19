using UnityEngine;
using XeptKit.FSM;

namespace XeptGame.Player
{
    /// <summary>
    /// 电机离散动作调度器（独立类：不寄生状态类/父状态；设计决议 §2.4）。
    /// 把"动作级/姿态级"输入（滑铲、蹲伏、接地跳跃）从父状态（GroundedState）解耦为**集中准入 + 转移请求**：
    /// 每帧对当前叶子状态按能力 Marker 判定——叶子实现 <see cref="ISlidable"/> 且动态门槛
    /// <see cref="PlayerMotorContext.CanStartSlide"/> 通过 → 请求滑铲；叶子实现 <see cref="ICrouchable"/>
    /// → 请求蹲伏；叶子实现 <see cref="IJumpable"/> → 执行统一接地跳。判定不依赖具体状态类型
    /// （消除 <c>is CrouchState</c> 硬编码），新增能力只须在状态上实现 Marker。
    ///
    /// 能力边界（与 Marker 设计一致，见 <see cref="ICrouchable"/>/<see cref="IJumpable"/>）：
    /// - Marker 表达**结构性能力**（本状态类别允许该动作）——准入在调度器集中判定；
    /// - 动作的**动态/执行细节留在状态/域侧**：蹲伏保持/起身在 CrouchState 内部；
    ///   Fall 土狼跳、UnstableGround 可滑面跳为条件性跳跃，留各自状态内，不进调度器。
    ///
    /// 时序（PlayerController.Update，于 <see cref="Fsm.Tick"/> **之后**驱动）：
    /// 须在 Tick 后判定——Tick 内父状态先执行物理转移（Grounded→Airborne），转移收敛后
    /// 叶子状态即真实分支；若先于 Tick 判定，已离地但父状态尚未退出的那一帧会把叶子仍当作
    /// 接地态动作（空中按蹲会收缩胶囊、或与父级转移在两级机器上并发竞态）。Tick 后判定保证
    /// "叶子实现 IJumpable/ICrouchable ⇔ 决策层确认接地"，与旧父状态语义逐帧一致。
    ///
    /// **意图优先级：奔跑 > 蹲伏**——奔跑意图（<see cref="PlayerMotorContext.CanSprint"/>）激活时
    /// **不接入蹲伏**（蹲伏意图被暂时压制、并未终止；奔跑结束后按意图重新生效，见 CrouchState）。
    /// 该判断与 CrouchState 的起身触发共用同一 Context 谓词（单一规则来源，防逐帧翻转）。
    /// 动作优先级：**滑铲 > 蹲伏 > 跳跃**——同帧"冲刺+蹲+跳" = 起滑；蹲伏保持中（叶子=CrouchState）
    /// 请求幂等跳过；滑铲保持中（叶子=SlideState，非 ICrouchable）不会被蹲伏动作打断，
    /// 其保持/退出由 SlideState 自行处理。
    /// </summary>
    public sealed class MotorActionDispatcher
    {
        private readonly Fsm _fsm;
        private readonly PlayerMotorContext _ctx;

        public MotorActionDispatcher(Fsm motorFsm, PlayerMotorContext context)
        {
            _fsm = motorFsm;
            _ctx = context;
        }

        /// <summary>帧驱动（PlayerController.Update 调用，见类注释时序说明）。</summary>
        public void Dispatch()
        {
            if (_fsm.CurrentState == null)
            {
                return; // 未进入初始状态 / 已终止
            }

            // 动作优先级：滑铲 > 蹲伏 > 跳跃（任一命中即本帧结束，不再评估后续动作）
            if (TrySlide())
            {
                return; // 起滑
            }

            if (TryCrouch())
            {
                return; // 蹲伏
            }

            TryJump();
        }

        /// <summary>
        /// 滑铲动作（§2.5）：准入 = 叶子实现 <see cref="ISlidable"/> 且
        /// <see cref="PlayerMotorContext.CanStartSlide"/>（**滑铲请求瞬时意图** + 水平自主速度达门槛）
        /// → 在叶子所属机器（Grounded 子机器）内请求 SlideState。
        /// 返回 true = 本帧起滑（不再评估蹲伏/跳跃）。
        /// </summary>
        private bool TrySlide()
        {
            var leaf = _fsm.CurrentState;
            if (leaf is not ISlidable || !_ctx.CanStartSlide)
            {
                return false;
            }

            leaf.Fsm.RequestChange<SlideState>();
            return true;
        }

        /// <summary>
        /// 蹲伏动作：准入 = 叶子实现 <see cref="ICrouchable"/> 且想蹲 → 在叶子所属机器
        /// （Grounded 子机器）内请求 CrouchState。实现类与 CrouchState 同机器，故叶子用
        /// 自身 <c>Fsm</c> 发起转移（等价于旧父状态 <c>SubMachine.RequestChange</c>）。
        /// 返回 true = 本帧蹲伏优先（不再评估跳跃）；已处于 Crouch 时请求幂等跳过（保持由状态内处理）。
        /// </summary>
        private bool TryCrouch()
        {
            var leaf = _fsm.CurrentState;
            if (leaf is not ICrouchable || !_ctx.WantCrouch)
            {
                return false;
            }

            // **意图优先级：奔跑 > 蹲伏**——奔跑意图激活时不接入蹲伏。
            // 必需（非仅语义）：否则 CrouchState 因奔跑意图起身进 Sprint 后，本帧调度器又会把叶子
            // 按回 Crouch → Crouch⇄Sprint 逐帧翻转。与 CrouchState 的起身触发共用 Context.CanSprint。
            if (_ctx.CanSprint)
            {
                return false;
            }

            leaf.Fsm.RequestChange<CrouchState>();
            return true;
        }

        /// <summary>
        /// 接地跳跃动作（由 GroundedState.PerformJump 迁移）：准入 = 叶子实现 <see cref="IJumpable"/>
        /// 且本帧有跳跃边沿且未消耗（JumpConsumed 于落地进入 Grounded 时由 GroundedState.OnEnter 重置）。
        /// 执行统一"接地跳"：
        /// - 平台垂直速度并入起跳冲量——升降平台起跳时 KCC 动量保持会把平台全速度（含垂直）加回
        ///   BaseVelocity，但 FallState 消费冲量时 Project 掉垂直分量；不补回会丢失 Y 轴惯性
        ///   （设计决议 §3.1 移动平台跳跃 Y 轴惯性修复）；
        /// - ForceUnground：跳过起跳后接地探测（防抖，防 Fall→Grounded 链重置 JumpConsumed 导致二段跳）；
        /// - 冒泡根机器转 Airborne（Airborne 是 Grounded 的顶层兄弟）。
        /// </summary>
        private void TryJump()
        {
            var leaf = _fsm.CurrentState;
            if (leaf is not IJumpable || !_ctx.Input.JumpIntent || _ctx.JumpConsumed)
            {
                return;
            }

            var up = _ctx.Motor.CharacterUp;
            var platformVertical = Vector3.Project(_ctx.Motor.AttachedRigidbodyVelocity, up);

            _ctx.PendingJumpImpulse = up * _ctx.Profile.jumpUpSpeed + platformVertical;
            _ctx.Motor.ForceUnground();
            _ctx.JumpConsumed = true;

            leaf.RootFsm.RequestChange<AirborneState>();
        }
    }
}
