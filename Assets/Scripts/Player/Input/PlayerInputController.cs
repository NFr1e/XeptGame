using UnityEngine;
using UnityEngine.InputSystem;
using XeptGame.Core.Input;
using XeptKit.Core;
using XeptKit.Input;

namespace XeptGame.Player
{
    public class PlayerInputController
    {
        private readonly IInputManager _inputManager;
        private readonly GameInput _gameInput;
        private readonly PlayerInputSettings _settings;

        private readonly PlayerMotorInputState _motorInput;
        private readonly PlayerLookInputState _lookInput;

        private readonly CompositeDisposable _disposables = new();

        private bool _isLayerBlocked;

        public PlayerInputController(IInputManager inputManager, GameInput gameInput, PlayerInputSettings settings, PlayerMotorInputState motorInput, PlayerLookInputState lookInput)
        {
            _inputManager = inputManager;
            _gameInput = gameInput;
            _settings = settings;
            _motorInput = motorInput;
            _lookInput = lookInput;
        }

        public void Init()
        {
            _gameInput.Gameplay.Enable();
            Bind();
        }
        public void Update()
        {
            _isLayerBlocked = !_inputManager.IsLayerActive<GameplayInputLayer>();
            if (_isLayerBlocked)
            {
                HandleLayerBlock();
            }
        }
        public void Shutdown()
        {
            _disposables.Dispose();
            _gameInput.Gameplay.Disable();
        }

        #region Internal
        private void Bind()
        {
            _disposables.Add(_inputManager.Bind<GameplayInputLayer>(_gameInput.Gameplay.Move, OnMove));
            _disposables.Add(_inputManager.Bind<GameplayInputLayer>(_gameInput.Gameplay.Jump, OnJump));
            _disposables.Add(_inputManager.Bind<GameplayInputLayer>(_gameInput.Gameplay.Sprint, OnSprint));
            _disposables.Add(_inputManager.Bind<GameplayInputLayer>(_gameInput.Gameplay.Crouch, OnCrouch));
            _disposables.Add(_inputManager.Bind<GameplayInputLayer>(_gameInput.Gameplay.Look, OnLook));
        }
        private void HandleLayerBlock()
        {
            // 输入层被阻断（UI/暂停/失去控制权）→ 意图全部终止（两类模式同样适用）；
            // 决策层因此看到"意图终止"，滑铲/蹲伏/冲刺按各自规则退出
            _motorInput.MoveInput = Vector2.zero;
            _motorInput.SprintIntent = false;
            _motorInput.CrouchIntent = false;
            _motorInput.SlideIntent = false;
            _lookInput.Delta = Vector2.zero;
        }

        private void OnMove(InputAction.CallbackContext ctx)
        {
            _motorInput.MoveInput = ctx.ReadValue<Vector2>();
        }
        private void OnJump(InputAction.CallbackContext ctx)
        {
            if (ctx.performed)
            {
                _motorInput.JumpIntent = true;
            }
        }
        private void OnSprint(InputAction.CallbackContext ctx)
        {
            // 折叠为**意图状态**（决策层只识别"是否想冲刺"，不识别长按/点按输入模式）：
            // PressToSprint=true 长按（按住激活/松开终止）；false 切换（按下翻转）
            if (ctx.performed)
            {
                if (_settings.PressToSprint.Value)
                {
                    _motorInput.SprintIntent = true;
                }
                else
                {
                    _motorInput.SprintIntent = !_motorInput.SprintIntent;
                }
            }
            else if (ctx.canceled && _settings.PressToSprint.Value)
            {
                _motorInput.SprintIntent = false;
            }
        }
        private void OnCrouch(InputAction.CallbackContext ctx)
        {
            // 折叠为**意图状态**（同 OnSprint）：两种模式产出的都是同一个"想蹲伏"意图，
            // 差异只在"谁把意图终止"——长按=松开，点按=下一次按下翻转。
            if (ctx.performed)
            {
                if (_settings.PressToCrouch.Value)
                {
                    _motorInput.CrouchIntent = true;
                }
                else
                {
                    _motorInput.CrouchIntent = !_motorInput.CrouchIntent;
                }

                // 蹲伏意图 0→1 的那一帧同时置**滑铲请求**（瞬时意图，与跳跃边沿同类）：
                // 滑铲进入看"意图到达"而非"意图为真"，否则蹲伏中按奔跑会在到达冲刺速度后
                // 自激成 滑铲↔奔跑 循环（设计决议 §2.5）
                if (_motorInput.CrouchIntent)
                {
                    _motorInput.SlideIntent = true;
                }
            }
            else if (ctx.canceled && _settings.PressToCrouch.Value)
            {
                _motorInput.CrouchIntent = false;
            }
        }

        private void OnLook(InputAction.CallbackContext ctx)
        {
            if (_isLayerBlocked) return;

            var delta = ctx.ReadValue<Vector2>();

            // 手柄摇杆死区（输入预处理；灵敏度/反转/累加归 PlayerLookController）
            var deadzone = _settings.StickDeadzone.Value;
            if (ctx.control.device is Gamepad && deadzone > 0f)
            {
                var mag = delta.magnitude;
                if (mag <= deadzone)
                {
                    delta = Vector2.zero;
                }
                else
                {
                    var t = (mag - deadzone) / (1f - deadzone);
                    delta = delta.normalized * t;
                }
            }

            _lookInput.Delta = delta;
        }
        #endregion
    }
}
