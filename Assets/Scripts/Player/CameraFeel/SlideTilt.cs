using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 滑铲相机倾斜（3C_CameraFeel_Design.md §4.6）：滑铲时相机轻微 roll（**固定方向**，v1 不区分左右），
    /// 经 <see cref="ICameraEffectTarget.AddRotationOffset"/> 叠加——**不动 BaseRotation 角度权威**
    /// （PlayerLook 独占写基准；roll 归零时瞄准完全不受影响）。
    /// 目标 = <see cref="FeelSnapshot.IsSliding"/> ? `tiltAngle` : 0，SmoothDamp 过渡（进/出共用同一 τ）。
    /// 与 JumpInertia 无冲突：两者都走 `AddRotationOffset`（乘性叠加，pitch 与 roll 正交）；
    /// 与 HeadBob 无冲突：滑铲时 HeadBob 已被抑制（§4.1），避免"抖 + 倾斜"叠加。
    /// </summary>
    public sealed class SlideTilt : MonoBehaviour
    {
        [Tooltip("观感参数资产；为空时使用内置默认值")]
        [SerializeField] private PlayerCameraFeelProfile viewProfile;

        [Tooltip("角色控制器（Player 根）；留空时自动从父级查找")]
        [SerializeField] private PlayerController player;

        [Tooltip("相机合成器（本物体）；留空时自动查找")]
        [SerializeField] private CameraRig rig;

        private PlayerCameraFeelProfile _profile;
        private ICameraEffectTarget _target;
        private float _roll;              // 当前倾斜角（度；SmoothDamp 输出）
        private float _smoothVelocity;

        /// <summary>装配（Awake 调用；**测试可注入假目标**后直接驱动 <see cref="Tick"/>）。</summary>
        public void Initialize(ICameraEffectTarget target, PlayerCameraFeelProfile profile)
        {
            _target = target;
            _profile = profile != null ? profile : PlayerCameraFeelProfile.Default;
        }

        private void Awake()
        {
            player ??= GetComponentInParent<PlayerController>();
            rig ??= GetComponent<CameraRig>();
            Initialize(rig, viewProfile);
        }

        private void Update()
        {
            if (_target == null || player == null)
            {
                return;
            }

            Tick(Time.deltaTime, player.GetFeelSnapshot());
        }

        /// <summary>每帧求解（可测：给定快照与 dt 直接驱动）。</summary>
        public void Tick(float deltaTime, in FeelSnapshot snapshot)
        {
            if (_target == null)
            {
                return;
            }

            var tilt = _profile.slideTilt;
            float target = snapshot.IsSliding ? tilt.tiltAngle : 0f;

            _roll = Mathf.SmoothDamp(_roll, target, ref _smoothVelocity,
                tilt.transitionTime, Mathf.Infinity, deltaTime);

            if (Mathf.Abs(_roll) > 0.001f)
            {
                _target.AddRotationOffset(Quaternion.Euler(0f, 0f, _roll));
            }
        }
    }
}
