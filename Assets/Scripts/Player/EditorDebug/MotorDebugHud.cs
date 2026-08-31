using UnityEngine;
using UnityEngine.InputSystem;

namespace XeptGame.Player
{
    /// <summary>
    /// 开发者调试 HUD（OnGUI 轻实现）：显示 3C 角色运动信息，便于运动调试。
    /// 数据源：PlayerController 公开的 MotorFsm / MotorContext / LookController。
    /// 挂载：Player 根节点（与 PlayerController 同物体）；F8 开关显示。
    /// 定位：开发期调试工具，随游戏运行（非 Editor-only）。
    /// </summary>
    public class MotorDebugHud : MonoBehaviour
    {
        [SerializeField] private bool show = true;
        [SerializeField] private Key toggleKey = Key.F8;

        [SerializeField]private PlayerController _player;


        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                show = !show;
            }
        }

        private void OnGUI()
        {
            if (!show || _player == null)
            {
                return;
            }

            var fsm = _player.MotorFsm;
            var ctx = _player.MotorContext;

            GUILayout.BeginArea(new Rect(10f, 10f, 420f, 260f), GUI.skin.box);
            GUILayout.Label("[3C Debug]  F8 开关");

            if (ctx == null)
            {
                GUILayout.Label("MotorContext 未装配（场景未配置 KCC 电机？）");
                GUILayout.EndArea();
                return;
            }

            var ground = ctx.Motor.Ground;
            var velocity = ctx.Motor.Velocity;
            float verticalSpeed = Vector3.Dot(velocity, ctx.Motor.CharacterUp);
            float horizontalSpeed = Vector3.ProjectOnPlane(velocity, ctx.Motor.CharacterUp).magnitude;
            float normalAngle = Vector3.Angle(ground.GroundNormal, ctx.Motor.CharacterUp);

            GUILayout.Label($"状态: {fsm?.CurrentStateType?.Name ?? "null"}   顶层: {fsm?.RootStateType?.Name ?? "null"}");
            GUILayout.Label($"速度: {velocity.magnitude:F2}   水平: {horizontalSpeed:F2}   垂直: {verticalSpeed:F2}");
            GUILayout.Label($"接地: Stable={ground.IsStableOnGround}   FoundAny={ground.FoundAnyGround}   法线角: {normalAngle:F1}°");
            GUILayout.Label($"Yaw: {_player.LookController.Yaw:F1}   Pitch: {_player.LookController.Pitch:F1}");
            GUILayout.Label($"土狼计时: {ctx.TimeSinceLastAbleToJump:F3}   JumpConsumed: {ctx.JumpConsumed}");
            GUILayout.Label($"输入: Move=({ctx.Input.MoveInput.x:F2},{ctx.Input.MoveInput.y:F2})   Sprint={ctx.Input.SprintHeld}   Crouch={ctx.Input.CrouchHeld}");
            GUILayout.Label($"LocalIntent: ({ctx.Input.LocalMoveIntent.x:F2},{ctx.Input.LocalMoveIntent.y:F2},{ctx.Input.LocalMoveIntent.z:F2})   World: ({ctx.WorldMoveIntent.x:F2},{ctx.WorldMoveIntent.z:F2})");

            GUILayout.EndArea();
        }
    }
}
