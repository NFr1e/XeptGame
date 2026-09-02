using System;
using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 观感层每帧数据快照（3C_CameraFeel_Design.md §3.2）：
    /// PlayerController 提供（<see cref="PlayerController.GetFeelSnapshot"/>），效果源 Update 拉取；
    /// 不直接触碰 FSM/MotorContext 内部（契约隔离）。连续量拉取（速度/状态），
    /// 离散事件（着陆）经 <see cref="PlayerMotorContext.Landing"/> 推送。
    /// </summary>
    public readonly struct FeelSnapshot
    {
        /// <summary>
        /// 水平**自主运动**速度（ProjectOnPlane(Motor.OwnVelocity, up).magnitude；不含贴附移动平台贡献——
        /// 平台被动携带不应驱动 HeadBob；原始值，效果源自平滑）。
        /// </summary>
        public readonly float HorizontalSpeed;

        /// <summary>是否稳定接地（Grounded 层级内）。</summary>
        public readonly bool IsGrounded;

        /// <summary>当前叶子状态类型（档位判定：Idle/Walk/Sprint/Crouch）。</summary>
        public readonly Type LeafState;

        /// <summary>是否蹲伏中。</summary>
        public readonly bool IsCrouching;

        /// <summary>是否冲刺中。</summary>
        public readonly bool IsSprinting;

        /// <summary>
        /// 垂直**自主**速度（dot(Motor.OwnVelocity, up)，正 = 向上；不含平台被动运动）。
        /// JumpInertia 用：起跳 v_y&gt;0 → 相机下偏（惯性滞后），下落 v_y&lt;0 → 上偏。
        /// </summary>
        public readonly float VerticalVelocity;

        /// <summary>眼位目标高度（相对角色 transform；眼位 = 胶囊顶部，电机域发布，CrouchEye 用）。</summary>
        public readonly float TargetEyeHeight;

        /// <summary>站立眼位高度（相对角色 transform；Profile 常量，CrouchEye 偏移基准）。</summary>
        public readonly float StandingEyeHeight;

        public FeelSnapshot(float horizontalSpeed, float verticalVelocity,
            float targetEyeHeight, float standingEyeHeight,
            bool isGrounded, Type leafState, bool isCrouching, bool isSprinting)
        {
            HorizontalSpeed = horizontalSpeed;
            VerticalVelocity = verticalVelocity;
            TargetEyeHeight = targetEyeHeight;
            StandingEyeHeight = standingEyeHeight;
            IsGrounded = isGrounded;
            LeafState = leafState;
            IsCrouching = isCrouching;
            IsSprinting = isSprinting;
        }
    }
}
