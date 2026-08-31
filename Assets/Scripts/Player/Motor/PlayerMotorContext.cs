using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// PlayerMotor FSM 类型化上下文：会话数据的唯一载体。
    /// 依据 XeptKit.FSM 约定：状态实例按类型缓存复用、禁止承载跨进入会话的可变数据——
    /// 一切可变会话数据（跳跃时序、加力通道等）必须经本类读写。
    /// </summary>
    public sealed class PlayerMotorContext
    {
        public PlayerMotorContext(IMotor motor, PlayerMotorInputState input, PlayerMotorProfile profile)
        {
            Motor = motor;
            Input = input;
            Profile = profile;
        }

        /// <summary>电机端口（决策层唯一电机依赖）。</summary>
        public IMotor Motor { get; }

        /// <summary>运动输入意图（PlayerInputController 写入）。</summary>
        public PlayerMotorInputState Input { get; }

        /// <summary>参数配置（速度档/跳跃/重力/胶囊）。</summary>
        public PlayerMotorProfile Profile { get; }

        // ============================================================
        // 会话数据（状态类读写；不得存状态字段）
        // ============================================================

        /// <summary>离地后计时（土狼时间窗口用；Grounded 置 0，Airborne 递增）。</summary>
        public float TimeSinceLastAbleToJump { get; set; }

        /// <summary>本"着陆周期"内跳跃已消耗（落地进入 Grounded 时重置）。</summary>
        public bool JumpConsumed { get; set; }

        /// <summary>待应用起跳初速度（转移时写入，Airborne 首帧 ApplyVelocity 消费）。</summary>
        public Vector3 PendingJumpImpulse { get; set; }

        /// <summary>通用加力通道（受击击退等；任意状态可注入）。</summary>
        public Vector3 AddVelocityAccumulator { get; set; }

        // ============================================================
        // 便捷判定（Sprint/Crouch 已在输入层折叠为 Held 持续值，直接读取）
        // ============================================================

        /// <summary>冲刺激活（长按/切换语义在输入层折叠）。</summary>
        public bool WantSprint => Input.SprintHeld;

        /// <summary>蹲伏激活（长按/切换语义在输入层折叠）。</summary>
        public bool WantCrouch => Input.CrouchHeld;

        // ============================================================
        // 参考系 = body（设计决议 §2.2）
        // ============================================================

        /// <summary>
        /// 世界空间移动意图 = 本地意图（body 局部）经 body 旋转转世界。
        /// 参考系 = body：body 被移动平台旋转带动时，移动方向同步跟随参考系，
        /// 与视角（相机 local 相对 body）天然一致，无需"平台脱离并入基准"。
        /// </summary>
        public Vector3 WorldMoveIntent => Motor.TransformDirection(Input.LocalMoveIntent);

        // ============================================================
        // 接地语义判定
        // ============================================================

        /// <summary>
        /// 是否为"坡面滑动"（接地法线与上方向夹角超过稳定角）。
        /// 用于区分 KCC 的两种"不稳定接地"：陡坡（应 UnstableGround）vs
        /// 悬崖边缘/落差（LedgeDetected，法线≈up，应直接 Fall 下落）。
        /// </summary>
        public bool IsSlopeSlide
            => Vector3.Angle(Motor.Ground.GroundNormal, Motor.CharacterUp) > Motor.MaxStableSlopeAngle;

        /// <summary>
        /// 是否为"可滑面"（UnstableGround 进入条件）：坡面（法线超稳定角）**或**
        /// 不可站立层对象（接地碰撞体不在 StableGroundLayers——互斥语义，见设计决议 §5.4）。
        /// 悬崖边缘（稳定层 + 法线≈up）不满足 → 保持 Fall。
        /// </summary>
        public bool IsUnstableGroundSurface => IsSlopeSlide || IsOnNonStableLayer;

        /// <summary>
        /// 当前接地对象是否"不可站立"：接地碰撞体 Layer **不在** StableGroundLayers。
        /// 依赖 KCC 接地探测填充 GroundCollider（KCC 已改为探测全部碰撞层，见修改点）；
        /// 互斥语义：StableGroundLayers = 可站立（Grounded），非 StableGroundLayers = 不可站立（UnstableGround）。
        /// </summary>
        public bool IsOnNonStableLayer
            => Motor.GroundColliderLayer >= 0
               && (Motor.StableGroundLayers.value & (1 << Motor.GroundColliderLayer)) == 0;
    }
}
