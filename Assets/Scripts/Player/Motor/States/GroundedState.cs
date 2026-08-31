using System;
using UnityEngine;
using XeptKit.FSM;

namespace XeptGame.Player
{
    /// <summary>
    /// 稳定接地复合状态（父）：共享跳跃检测与物理转移，子状态管速度档位/姿态参数。
    /// 子机器：{ Idle, Walk, Sprint, Crouch }（互斥同层，设计决议 §2）。
    /// 跳跃为接地共享能力（Crouch 禁跳）；离地/不稳定接地 → Airborne。
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

            // 确保站立胶囊尺寸按 Profile 配置（场景胶囊可能被调整过；Crouch 子状态切换不触发本钩子）
            var profile = Ctx.Profile;
            Ctx.Motor.SetCapsuleDimensions(profile.capsuleRadius, profile.standingHeight, profile.standingYOffset);

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
            if (!ground.IsStableOnGround || Ctx.IsOnNonStableLayer)
            {
                Fsm.RequestChange<AirborneState>();
                return;
            }

            // 跳跃（Crouch 禁跳；蹲需先起身）
            if (!(Fsm.CurrentState is CrouchState) && Ctx.Input.JumpPressed && !Ctx.JumpConsumed)
            {
                PerformJump();
            }
        }

        private void PerformJump()
        {
            var up = Ctx.Motor.CharacterUp;

            // 平台垂直速度并入起跳冲量：移动平台（升降）起跳时，KCC 的动量保持会把平台
            // 全速度（含垂直）加回 BaseVelocity，但 Fall 消费冲量时 Project 掉垂直分量——
            // 若不补回，升降平台跳跃会丢失 Y 轴惯性（X/Z 保留、Y 丢失）。
            var platformVertical = Vector3.Project(Ctx.Motor.AttachedRigidbodyVelocity, up);

            Ctx.PendingJumpImpulse = up * Ctx.Profile.jumpUpSpeed + platformVertical;
            Ctx.Motor.ForceUnground();
            Ctx.JumpConsumed = true;

            Fsm.RequestChange<AirborneState>();
        }
    }
}
