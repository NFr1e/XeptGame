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
            _motorInput.MoveInput = Vector2.zero;
            _motorInput.SprintHeld = false;
            _motorInput.CrouchHeld = false;
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
                _motorInput.JumpPressed = true;
            }
        }
        private void OnSprint(InputAction.CallbackContext ctx)
        {
            // 折叠为 Held 语义（Motor 只读 SprintHeld 持续值，无切换保持/边沿时序问题）：
            // PressToSprint=true 长按（按住激活/松开复位）；false 切换（按下翻转）
            if (ctx.performed)
            {
                if (_settings.PressToSprint.Value)
                {
                    _motorInput.SprintHeld = true;
                }
                else
                {
                    _motorInput.SprintHeld = !_motorInput.SprintHeld;
                }
            }
            else if (ctx.canceled && _settings.PressToSprint.Value)
            {
                _motorInput.SprintHeld = false;
            }
        }
        private void OnCrouch(InputAction.CallbackContext ctx)
        {
            // 折叠为 Held 语义（同 OnSprint）：
            // PressToCrouch=true 长按；false 切换（按下翻转）
            if (ctx.performed)
            {
                if (_settings.PressToCrouch.Value)
                {
                    _motorInput.CrouchHeld = true;
                }
                else
                {
                    _motorInput.CrouchHeld = !_motorInput.CrouchHeld;
                }
            }
            else if (ctx.canceled && _settings.PressToCrouch.Value)
            {
                _motorInput.CrouchHeld = false;
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
