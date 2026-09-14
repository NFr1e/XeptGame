using KinematicCharacterController;
using UnityEngine;
using XeptKit.Core;
using XeptKit.FSM;
using XeptKit.Input;

using XeptGame.Core.Input;

namespace XeptGame.Player
{
    /// <summary>
    /// 玩家宿主（组合根/装配点）：创建并装配 3C 输入、视角与电机服务，驱动帧回调。
    /// 职责边界：
    /// - 创建 PlayerInputSettings / PlayerInputController / PlayerLookController /
    ///   KccMotorAdapter / PlayerMotorContext / PlayerMotor FSM，构造注入装配；
    /// - 注入 PlayerLook（相机消费端）；帧驱动：Fsm.Tick 挂 Update（输入零延迟），
    ///   输入边沿标记（JumpIntent）挂 LateUpdate 清除；
    /// - 创建并帧驱动 MotorActionDispatcher（离散动作：滑铲/蹲伏/接地跳跃，能力 Marker 准入，
    ///   优先级 slide &gt; crouch &gt; jump；于 Fsm.Tick 之后驱动——物理转移先收敛，见类注释）；
    /// - 把 PlayerCharacterController（KCC 回调翻译器）挂接到场景中的 KCC 电机；
    /// - 帧驱动：InputController.Update（输入层阻断检测）；
    /// - OnDestroy 释放（退订输入 + 释放 Fsm；GlobalInput 为 AppEntry 全局单例，由组合根管理）。
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerLookProfile lookProfile;
        [SerializeField] private PlayerMotorProfile motorProfile;
        [SerializeField] private KinematicCharacterMotor motor;
        [SerializeField] private PlayerLook playerLook;
        [SerializeField] private CameraRig cameraRig;

        private GameInput _gameInput;
        private IInputManager _inputManager;

        private PlayerMotorInputState _motorInput;
        private PlayerLookInputState _lookInput;

        public Fsm MotorFsm { get; private set; }
        public PlayerMotorContext MotorContext { get; private set; }
        public PlayerInputController InputController { get; private set; }
        public PlayerInputSettings InputSettings { get; private set; }
        public PlayerLookController LookController { get; private set; }
        public KinematicCharacterMotor Motor => motor;

        /// <summary>电机离散动作调度器（蹲伏/接地跳跃：能力 Marker 准入 + 转移请求，见 MotorActionDispatcher）。</summary>
        public MotorActionDispatcher MotorActions { get; private set; }

        private void Awake()
        {
            if(!lookProfile)
            {
                lookProfile = PlayerLookProfile.Default;
            }
            if(!motorProfile)
            {
                motorProfile = PlayerMotorProfile.Default;
            }

            InputSettings = new PlayerInputSettings();
            InputSettings.SetDefaultLook(lookProfile);
            InputSettings.SetDefaultMotor(motorProfile);

            _motorInput = new PlayerMotorInputState();
            _lookInput = new PlayerLookInputState();

            LookController = new PlayerLookController(InputSettings);

            _gameInput = AppEntry.GlobalInput;
            _inputManager = AppEntry.InputManager;

            InputController = new(
                _inputManager, 
                _gameInput, 
                InputSettings, 
                _motorInput, 
                _lookInput);
            InputController.Init();


            if(playerLook)
                playerLook.Initialize(LookController, _motorInput, _lookInput);

            if (motor)
            {
                var motorAdapter = new KccMotorAdapter(motor);

                // KCC sweep 迭代配置：凸曲面/贴墙下落时 sweep 命中法线持续变化、迭代频繁超限，
                // 默认 MaxMovementIterations=5 + KillVelocityWhenExceedMaxMovementIterations=true
                // 会清零整个速度（含垂直下落速度）导致"坠落速度丢失/滑行停住"。增大迭代上限并保留超限速度
                // （见 3C_CharacterMotor_Design.md §3.1；KCC 1591-1603 行）。
                //motor.MaxMovementIterations = 15;
                //motor.KillVelocityWhenExceedMaxMovementIterations = false;

                motor.transform.rotation = Quaternion.identity;

                MotorContext = new PlayerMotorContext(motorAdapter, _motorInput, motorProfile);
                // 初始站立胶囊 + 眼位目标（胶囊唯一配置源 = Profile；KCC ValidateData 会覆盖场景胶囊，
                // 见 3C_CharacterMotor_Design.md §5.3；眼位 = 胶囊顶部，CrouchEye 消费）
                MotorContext.ApplyCapsule(false);

                MotorFsm = new Fsm(MotorContext);
                MotorFsm.RequestChange<GroundedState>(KitLifecycle.GlobalToken);

                MotorActions = new MotorActionDispatcher(MotorFsm, MotorContext);

                motor.CharacterController = new PlayerCharacterController(MotorFsm);
            }
        }

        private void Update()
        {
            InputController?.Update();
            MotorFsm?.Tick(Time.deltaTime);

            // 离散动作调度须在 Tick 之后：Tick 内父状态先做物理转移（Grounded→Airborne），
            // 转移收敛后叶子状态即真实分支——调度器按叶子能力（ICrouchable/IJumpable）准入，
            // 避免"已离地帧仍按接地态动作"（详见 MotorActionDispatcher 类注释）。
            MotorActions?.Dispatch();
        }

        private void LateUpdate()
        {
            MotorContext?.Input.EndFrame();
        }

        /// <summary>
        /// 观感层每帧数据快照（3C_CameraFeel_Design.md §3.2）：效果源 Update 拉取；
        /// 不直接触碰 FSM/MotorContext 内部（契约隔离）。连续量拉取，离散事件（着陆）经
        /// <see cref="PlayerMotorContext.Landing"/> 推送。
        /// </summary>
        public FeelSnapshot GetFeelSnapshot()
        {
            if (MotorFsm == null || MotorContext == null)
            {
                return default;
            }

            var ctx = MotorContext;
            // 自主运动速度（不含平台贡献）：移动平台上被动携带不应驱动 HeadBob（见设计决议 §3.2/§4.1）
            float horizontalSpeed = Vector3.ProjectOnPlane(ctx.Motor.OwnVelocity, ctx.Motor.CharacterUp).magnitude;
            float verticalVelocity = Vector3.Dot(ctx.Motor.OwnVelocity, ctx.Motor.CharacterUp);

            return new FeelSnapshot(
                horizontalSpeed,
                verticalVelocity,
                ctx.TargetEyeHeight,
                ctx.Profile.standingYOffset + ctx.Profile.standingHeight * 0.5f, // 站立眼位（胶囊顶部）
                MotorFsm.IsInHierarchy(typeof(GroundedState)),
                MotorFsm.CurrentStateType,
                MotorFsm.IsInHierarchy(typeof(CrouchState)),
                MotorFsm.IsInHierarchy(typeof(SprintState)));
        }

        private void OnDestroy()
        {
            InputController?.Shutdown();
            MotorFsm?.Dispose();
        }
    }
}
