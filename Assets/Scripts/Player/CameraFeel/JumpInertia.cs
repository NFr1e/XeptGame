using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 跳跃/坠落相机惯性俯仰（3C_CameraFeel_Design.md §4.4）：
    /// 起跳身体向上加速 → 头部惯性滞后 → 相机**下偏**；越过最高点开始下落 → 相机**上偏**。
    /// 实现：
    /// - **信号** = 垂直**自主**速度（FeelSnapshot.VerticalVelocity，dot(OwnVelocity, up)——
    ///   移动平台被动升降不触发，与 HeadBob 同一决策）；线性映射 + 钳制 + 死区；
    /// - **门控** = 仅空中生效（IsGrounded 时目标归零，同一平滑淡出——地面爬坡不干扰瞄准）；
    /// - **应用通道** = AddRotationOffset（不动 LookController 角度权威，归零时瞄准不受影响）；
    /// 输出为瞬态旋转偏移，与 LandingKick（落地瞬间 v_y→0 自然回零）可叠加、无冲突。
    /// </summary>
    public sealed class JumpInertia : MonoBehaviour
    {
        [Tooltip("观感参数资产；为空时使用内置默认值")]
        [SerializeField] private PlayerCameraFeelProfile viewProfile;

        [Tooltip("角色控制器（Player 根）；留空时自动从父级查找")]
        [SerializeField] private PlayerController player;

        [Tooltip("相机合成器（本物体）；留空时自动查找")]
        [SerializeField] private CameraRig rig;

        private PlayerCameraFeelProfile _profile;
        private float _currentPitch;    // 平滑后当前俯仰偏移（度）
        private float _smoothVel;

        private void Awake()
        {
            _profile = viewProfile != null ? viewProfile : PlayerCameraFeelProfile.Default;
            player ??= GetComponentInParent<PlayerController>();
            rig ??= GetComponent<CameraRig>();
        }

        private void Update()
        {
            if (player == null || rig == null)
            {
                return;
            }

            var snapshot = player.GetFeelSnapshot();
            var section = _profile.jumpInertia;

            // 仅空中生效：接地时目标归零（平滑淡出）
            float target = 0f;
            if (!snapshot.IsGrounded)
            {
                float v = snapshot.VerticalVelocity;
                if (Mathf.Abs(v) > section.deadZone)
                {
                    target = Mathf.Clamp(v * section.velocityScale, -section.maxPitchOffset, section.maxPitchOffset);
                }
            }

            _currentPitch = Mathf.SmoothDamp(_currentPitch, target, ref _smoothVel, section.smoothingTime);

            if (Mathf.Abs(_currentPitch) > 0.01f)
            {
                // 正 pitch = 低头（起跳下偏）；负 = 抬头（下落后仰）
                rig.AddRotationOffset(Quaternion.Euler(_currentPitch, 0f, 0f));
            }
        }
    }
}
