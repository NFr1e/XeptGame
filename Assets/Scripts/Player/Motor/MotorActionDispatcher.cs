using UnityEngine;
using XeptKit.FSM;

namespace XeptGame.Player
{
    /// <summary>
    /// 电机离散动作调度器（独立类：不寄生状态类/父状态；设计决议 §2.4）。
    /// 把"动作级/姿态级"输入（蹲伏、接地跳跃）从父状态（GroundedState）解耦为**集中准入 + 转移请求**：
    /// 每帧对当前叶子状态按能力 Marker 判定——叶子实现 <see cref="ICrouchable"/> → 请求蹲伏；
    /// 叶子实现 <see cref="IJumpable"/> → 执行统一接地跳。判定不依赖具体状态类型
    /// （消除 <c>is CrouchState</c> 硬编码），新增"可蹲/可跳"能力只须在状态上实现 Marker。
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
    /// 同帧蹲+跳：蹲伏优先（先评估蹲伏，命中即本帧不再评估跳跃）——与旧父状态
    /// "先蹲后跳、蹲中禁跳"语义一致；蹲伏保持中（叶子=CrouchState）请求幂等跳过。
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

            // 蹲伏优先：同帧蹲+跳 = 先蹲（蹲中禁跳，需先起身；起身由 CrouchState 内部处理）
            if (TryCrouch())
            {
                return;
            }

            TryJump();
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
            if (leaf is not IJumpable || !_ctx.Input.JumpPressed || _ctx.JumpConsumed)
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
