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

        private void Awake()
        {
            _profile = viewProfile != null ? viewProfile : PlayerCameraFeelProfile.Default;
        }

        private void Update()
        {
            if (player == null || rig == null)
            {
                return;
            }

            // 目标 FOV：蹲伏 > 疾跑 > 默认（蹲伏与冲刺互斥；切换由 CameraRig 平滑过渡）。
            var snapshot = player.GetFeelSnapshot();
            var target = snapshot.IsCrouching
                ? _profile.fov.fovCrouch
                : snapshot.IsSprinting
                    ? _profile.fov.fovSprint
                    : _profile.fov.fovIdle;

            rig.Fov.Set(target, OverridePriority.Effect);
        }

        private void OnDestroy()
        {
            // 移除本组件的 FOV 覆盖：禁用/销毁后不再冻结目标 FOV，恢复 CameraRig 默认基线
            if (rig != null)
            {
                rig.Fov.Clear(OverridePriority.Effect);
            }
        }
    }
}
