using KinematicCharacterController;
using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 电机调试 Gizmos：绘制角色运动信息，便于 Debug。
    /// 绘制内容（Scene 视图，编辑器与运行时均可见）：
    /// - 胶囊底部（小球）；
    /// - 接地对象（KCC GroundingStatus.GroundCollider）：红=不可站立（非 StableGroundLayers）/ 绿=可站立，附表面法线；
    /// - 角色实际速度方向（青色射线）。
    /// 挂载：角色根节点（与 PlayerController 同物体，自动取 Motor）。
    /// 定位：开发期调试工具。
    /// </summary>
    public class MotorDebugGizmos : MonoBehaviour
    {
        private KinematicCharacterMotor Motor
        {
            get
            {
                if (_motor == null)
                {
                    var player = GetComponent<PlayerController>();
                    _motor = player != null ? player.Motor : null;
                }
                return _motor;
            }
        }
        [SerializeField] private KinematicCharacterMotor _motor;

        private void OnDrawGizmos()
        {
            var motor = Motor;
            if (motor == null)
            {
                return;
            }

            // —— 胶囊底部 ——
            Vector3 bottom = motor.TransientPosition + motor.CharacterTransformToCapsuleBottom;
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(bottom, 0.03f);

            // —— 接地对象与可站立判定（互斥语义：非 StableGroundLayers = 不可站立）——
            var ground = motor.GroundingStatus;
            if (ground.GroundCollider != null)
            {
                bool isStableLayer = (motor.StableGroundLayers & (1 << ground.GroundCollider.gameObject.layer)) != 0;
                Gizmos.color = isStableLayer ? Color.green : Color.red;
                Gizmos.DrawSphere(bottom, 0.06f); // 接地：红=不可站立 / 绿=可站立
                Gizmos.DrawLine(bottom, bottom + ground.GroundNormal * 0.3f); // 表面法线
            }

            // —— 辅助：速度方向 ——
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(bottom, motor.Velocity * 0.1f);
        }
    }
}
