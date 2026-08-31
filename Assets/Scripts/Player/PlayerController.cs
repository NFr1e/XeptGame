using KinematicCharacterController;
using UnityEngine;
using XeptGame.Core.Input;
using XeptKit.Core;
using XeptKit.FSM;
using XeptKit.Input;

namespace XeptGame.Player
{
    /// <summary>
    /// 玩家宿主（组合根/装配点）：创建并装配 3C 输入、视角与电机服务，驱动帧回调。
    /// 职责边界：
    /// - 创建 PlayerInputSettings / PlayerInputController / PlayerLookController /
    ///   KccMotorAdapter / PlayerMotorContext / PlayerMotor FSM，构造注入装配；
    /// - 注入 PlayerLook（相机消费端）与 PlayerMotor（电机 FSM 宿主）；
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

        private GameInput _gameInput;
        private IInputManager _inputManager;

        private PlayerMotorInputState _motorInput;
        private PlayerLookInputState _lookInput;

        public PlayerInputController InputController { get; private set; }
        public PlayerInputSettings InputSettings { get; private set; }
        public PlayerLookController LookController { get; private set; }
        public Fsm MotorFsm { get; private set; }
        public PlayerMotorContext MotorContext { get; private set; }

        /// <summary>场景中的 KCC 电机（调试 Gizmos 等读取）。</summary>
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

            // —— Motor 装配（端口-适配器 → Context → FSM → KCC 回调翻译器）——
            if (motor)
            {
                var motorAdapter = new KccMotorAdapter(motor);
                // 胶囊唯一配置源 = PlayerMotorProfile：KCC 电机 ValidateData 内部强制值会覆盖
                // 场景胶囊，此处按 Profile 设置站立胶囊（GroundedState.OnEnter 亦会再设一次）
                motorAdapter.SetCapsuleDimensions(
                    motorProfile.capsuleRadius, motorProfile.standingHeight, motorProfile.standingYOffset);

                // KCC sweep 迭代配置：凸曲面（球面/曲面坡）上滑动时 sweep 命中法线持续变化，
                // 默认 MaxMovementIterations=5 频繁超限，且 KillVelocityWhenExceedMaxMovementIterations=true
                // 会清零速度导致角色"滑行一下停住、卡在坡上"。增大迭代上限并保留超限速度。
                motor.MaxMovementIterations = 15;
                motor.KillVelocityWhenExceedMaxMovementIterations = false;

                // body 初始归零：保证"参考系 = body"的初始基准（Look.Yaw=0 相对 body 前方），
                // 移动平台旋转带动 body 时视角/移动随参考系同步（见设计决议 §2.2）
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

        private void FixedUpdate()
        {
            MotorContext?.Input.EndFrame();
        }

        private void OnDestroy()
        {
            InputController?.Shutdown();
            MotorFsm?.Dispose();
        }
    }
}
