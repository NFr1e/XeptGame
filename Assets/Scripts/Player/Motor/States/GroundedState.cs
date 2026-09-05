using System;
using UnityEngine;
using XeptKit.FSM;

namespace XeptGame.Player
{
    /// <summary>
    /// 稳定接地复合状态（父）：共享接地周期重置、着陆事件与物理转移；子状态管速度档位/姿态参数。
    /// 子机器：{ Idle, Walk, Sprint, Crouch }（互斥同层，设计决议 §2）。
    /// 蹲伏/接地跳跃等姿态级动作已解耦为独立调度器 <see cref="MotorActionDispatcher"/>
    /// （能力 Marker <see cref="ICrouchable"/>/<see cref="IJumpable"/> 准入，设计决议 §2.4）；
    /// 本父状态不再判定输入，离地/不稳定接地 → Airborne 的物理转移仍在此。
    /// </summary>
    public sealed class GroundedState : CompositeStateBase
    {
        private PlayerMotorContext Ctx => Context as PlayerMotorContext;
        protected override Type ResolveInitialSubState() => typeof(IdleState);

        public override void OnEnter()
        {
            // 新着陆周期：重置跳跃消耗与土狼计时（会话数据经 Context）
            Ctx.JumpConsumed = false;
            Ctx.TimeSinceLastAbleToJump = 0f;

            // 确保站立胶囊尺寸按 Profile 配置（场景胶囊可能被调整过；Crouch 子状态切换不触发本钩子），
            // 并同步眼位目标（眼位 = 胶囊顶部，CrouchEye 效果源消费）
            Ctx.ApplyCapsule(false);

            // 着陆事件（观感层订阅，见 3C_CameraFeel_Design.md §3.1）：
            // 仅在"上一根状态为 Airborne"时触发——此刻 Fsm._current 仍是旧状态（RootState 探测），
            // Fall/UnstableGround → Grounded 均覆盖；初始进场（RootState=null）不触发，排除出生伪落地。
            if (RootFsm.RootState is AirborneState)
            {
                Ctx.RaiseLanding(Ctx.Motor.Ground.GroundNormal);
            }
        }

        public override void Update(float deltaTime)
        {
            Ctx.TimeSinceLastAbleToJump = 0f; // 接地中

            var ground = Ctx.Motor.Ground;

            // 物理转移：非稳定接地 或 踩到不可站立层（非 StableGroundLayers）→ Airborne
            // （初始子状态 Fall，由 Fall 判定是否转 UnstableGround）。
            // 注意：非 StableGroundLayers 上 KCC 法线角判定为稳定（IsStableOnGround=true），
            // 但语义上不可站立，需主动转移。
            // 蹲伏/跳跃不在此判定（动作调度已上移 MotorActionDispatcher，于 Fsm.Tick 后驱动——
            // 本转移先收敛，调度器看到的叶子即真实接地分支）。
            if (!ground.IsStableOnGround || Ctx.IsOnNonStableLayer)
            {
                Fsm.RequestChange<AirborneState>();
            }
        }
    }
}
