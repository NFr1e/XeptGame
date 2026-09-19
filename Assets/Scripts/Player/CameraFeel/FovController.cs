using UnityEngine;
using XeptKit.Core;

namespace XeptGame.Player
{
    /// <summary>
    /// FOV 动态效果源（3C_CameraFeel_Design.md §4.3）：按状态能力语义（蹲伏/冲刺）选择目标 FOV
    /// 写入 <see cref="CameraRig.Fov"/>（Effect 优先级）；平滑由 CameraRig 执行（帧率无关指数平滑）。
    /// OnDestroy 清除覆盖（避免禁用/销毁后残留覆盖冻结目标 FOV，恢复 CameraRig 默认基线）。
    /// 组件挂相机层级（眼位挂点）：PlayerController 经 GetComponentInParent 自动定位。
    /// </summary>
    public sealed class FovController : MonoBehaviour
    {
        [Tooltip("观感参数资产；为空时使用内置默认值")]
        [SerializeField] private PlayerCameraFeelProfile viewProfile;

        [Tooltip("角色控制器（Player 根）；留空时自动从父级查找")]
        [SerializeField] private PlayerController player;

        [Tooltip("相机合成器（本物体）；留空时自动查找")]
        [SerializeField] private CameraRig rig;

        private PlayerCameraFeelProfile _profile;
        private ICameraEffectTarget _target;

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

        /// <summary>
        /// 每帧求解（可测：给定快照与 dt 直接驱动）——按状态选择目标 FOV 写入 Effect 优先级。
        /// **滑铲取冲刺档**（用户决议：滑铲保持 Sprint FOV）；注意滑铲时
        /// <see cref="FeelSnapshot.IsCrouching"/>/<see cref="FeelSnapshot.IsSprinting"/> **均为 false**，
        /// 必须显式分支，否则会错误回落到 `fovIdle`（比冲刺还窄）。
        /// </summary>
        public void Tick(float deltaTime, in FeelSnapshot snapshot)
        {
            if (_target == null)
            {
                return;
            }

            // 目标 FOV：蹲伏 > 滑铲(= 冲刺档) > 疾跑 > 默认（蹲伏与滑铲/冲刺互斥；切换由 CameraRig 平滑过渡）
            float target = snapshot.IsCrouching
                ? _profile.fov.fovCrouch
                : snapshot.IsSprinting || snapshot.IsSliding
                    ? _profile.fov.fovSprint
                    : _profile.fov.fovIdle;

            _target.Fov.Set(target, OverridePriority.Effect);
        }

        private void OnDestroy()
        {
            // 移除本组件的 FOV 覆盖：禁用/销毁后不再冻结目标 FOV，恢复 CameraRig 默认基线
            if (_target != null)
            {
                _target.Fov.Clear(OverridePriority.Effect);
            }
        }
    }
}
