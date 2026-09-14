using System;
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
        // 眼位目标（观感层消费；眼位约定 = 胶囊顶部 = yOffset + height/2，见 3C_CameraFeel_Design.md §4.5）
        // ============================================================

        /// <summary>
        /// 眼位目标高度（相对角色 transform）。由 <see cref="ApplyCapsule"/> 与胶囊同步设置
        /// （站立 = 站立胶囊顶，蹲伏 = 蹲伏胶囊顶）；CrouchEye 效果源据此做相机降低过渡。
        /// 归属说明：**目标属 3C 正确性（跟胶囊走，防穿模/掩体遮挡），过渡属感受层**（混合归属）。
        /// </summary>
        public float TargetEyeHeight { get; set; }

        /// <summary>
        /// 按 Profile 应用胶囊尺寸并**同步眼位目标**（防两者发散）：站立/蹲伏共用。
        /// 眼位 = 胶囊顶部（yOffset + height/2）；调用点：GroundedState.OnEnter（站立）、
        /// CrouchState.OnEnter/起身（蹲伏/站立）、PlayerController.Awake（初始站立）。
        /// </summary>
        public void ApplyCapsule(bool crouching)
        {
            var p = Profile;
            float height = crouching ? p.crouchedHeight : p.standingHeight;
            float yOffset = crouching ? p.crouchedYOffset : p.standingYOffset;
            Motor.SetCapsuleDimensions(p.capsuleRadius, height, yOffset);
            TargetEyeHeight = yOffset + height * 0.5f; // 眼位 = 胶囊顶部
        }

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

        /// <summary>
        /// 滑铲起滑 boost 剩余时间（秒）：<see cref="SlideState.OnEnter"/> 写入
        /// （= Profile.slideBoostDuration），<see cref="SlideState.ApplyVelocity"/> 每帧线性摊入并递减
        /// （设计决议 §2.5）。会话数据放 Context 而非状态字段——Kit FSM 约定（状态实例复用）。
        /// </summary>
        public float SlideBoostRemaining { get; set; }

        // ============================================================
        // 着陆事件（观感层订阅；设计决议 docs/modules/3C_CameraFeel_Design.md §3.1）
        // ============================================================

        /// <summary>
        /// 着陆事件：<see cref="GroundedState.OnEnter"/> 内探测上一根状态为 Airborne 时触发
        /// （初始进场不触发，排除出生伪落地）。订阅方：LandingKick（落地镜头缓冲）、
        /// HeadBob（落地恢复窗口）等观感层效果源。
        /// </summary>
        public event Action<MotorLandingInfo> Landing;

        /// <summary>
        /// 离地会话内最大垂直下落速度（&gt;0；Fall/UnstableGround 的 ApplyVelocity 顶部经
        /// <see cref="MotorStateBase.CaptureFallSpeed"/> 记录，AirborneState.OnEnter 重置，落地时消费）。
        /// 读取 sweep 投影前的"带入速度"并取会话最大值——KCC 贴墙/贴边下落时会把结算后速度投影归零，
        /// 直接读 Motor.Velocity 会丢失真实冲击（实测：-18.9 m/s 在落地前被投影为 0）。
        /// </summary>
        public float LastAirborneVerticalSpeed { get; set; }

        /// <summary>触发着陆事件（由 GroundedState.OnEnter 在探测到上一状态为 Airborne 时调用）。</summary>
        public void RaiseLanding(Vector3 groundNormal)
        {
            Landing?.Invoke(new MotorLandingInfo(LastAirborneVerticalSpeed, groundNormal));
        }

        // ============================================================
        // 便捷判定（Sprint/Crouch 已在输入层折叠为 Held 持续值，直接读取）
        // ============================================================

        /// <summary>冲刺**意图**激活（长按/点按输入模式的差异已在输入层折叠，本层不识别输入状态）。</summary>
        public bool WantSprint => Input.SprintIntent;

        /// <summary>蹲伏**意图**激活（同上：决策层只识别意图，长按=按住、点按=切换由输入层折叠）。</summary>
        public bool WantCrouch => Input.CrouchIntent;

        /// <summary>
        /// 能否起滑（动态门槛；**结构能力**由 <see cref="ISlidable"/> 表达，调度器两者取与）：
        /// 想蹲 + 水平**自主**速度达到 <see cref="PlayerMotorProfile.slideEntryMinSpeed"/>。
        /// 用自主速度（排除移动平台被动携带——站在平台上被动高速不应能起滑）；
        /// 门槛不过时调度器退化为蹲伏（§2.5）。
        /// </summary>
        public bool CanStartSlide
            => WantCrouch
               && Vector3.ProjectOnPlane(Motor.OwnVelocity, Motor.CharacterUp).magnitude
                  >= Profile.slideEntryMinSpeed;

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

    /// <summary>
    /// 着陆信息（<see cref="PlayerMotorContext.Landing"/> 事件载荷）。
    /// </summary>
    public readonly struct MotorLandingInfo
    {
        /// <summary>最后空中帧捕获的垂直下落速度（&gt;0；未受落地碰撞响应影响）。</summary>
        public readonly float ImpactSpeed;

        /// <summary>接地法线（冲击方向参考）。</summary>
        public readonly Vector3 GroundNormal;

        public MotorLandingInfo(float impactSpeed, Vector3 groundNormal)
        {
            ImpactSpeed = impactSpeed;
            GroundNormal = groundNormal;
        }
    }
}
