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
    ///   输入边沿标记（JumpPressed）挂 FixedUpdate 清除；
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

                motorAdapter.SetCapsuleDimensions(
                    motorProfile.capsuleRadius, motorProfile.standingHeight, motorProfile.standingYOffset);

                // KCC sweep 迭代配置：凸曲面/贴墙下落时 sweep 命中法线持续变化、迭代频繁超限，
                // 默认 MaxMovementIterations=5 + KillVelocityWhenExceedMaxMovementIterations=true
                // 会清零整个速度（含垂直下落速度）导致"坠落速度丢失/滑行停住"。增大迭代上限并保留超限速度
                // （见 3C_CharacterMotor_Design.md §3.1；KCC 1591-1603 行）。
                //motor.MaxMovementIterations = 15;
                //motor.KillVelocityWhenExceedMaxMovementIterations = false;

                motor.transform.rotation = Quaternion.identity;

                MotorContext = new PlayerMotorContext(motorAdapter, _motorInput, motorProfile);

                MotorFsm = new Fsm(MotorContext);
                MotorFsm.RequestChange<GroundedState>(KitLifecycle.GlobalToken);

                motor.CharacterController = new PlayerCharacterController(MotorFsm);
            }
        }

        private void Update()
        {
            InputController?.Update();
            MotorFsm?.Tick(Time.deltaTime);
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

            return new FeelSnapshot(
                horizontalSpeed,
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
