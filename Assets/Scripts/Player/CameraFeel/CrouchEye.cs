using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 蹲伏眼位降低（3C_CameraFeel_Design.md §4.5，**混合归属**）：
    /// 眼位**目标**属 3C 正确性——电机域发布（<see cref="PlayerMotorContext.TargetEyeHeight"/>，
    /// 与胶囊同源同步，眼位 = 胶囊顶部），防穿模/掩体遮挡；
    /// 本效果源只做**过渡动画**（SmoothDamp，蹲伏降低/起身恢复）。
    /// 偏移基准 = 站立眼位（FeelSnapshot.StandingEyeHeight）：站立时偏移 0（场景眼位挂点 = 站立眼位），
    /// 蹲伏时偏移 = 蹲伏胶囊顶 - 站立胶囊顶（负值 = 降低）。
    /// </summary>
    public sealed class CrouchEye : MonoBehaviour
    {
        [Tooltip("观感参数资产；为空时使用内置默认值")]
        [SerializeField] private PlayerCameraFeelProfile viewProfile;

        [Tooltip("角色控制器（Player 根）；留空时自动从父级查找")]
        [SerializeField] private PlayerController player;

        [Tooltip("相机合成器（本物体）；留空时自动查找")]
        [SerializeField] private CameraRig rig;

        private PlayerCameraFeelProfile _profile;
        private float _offsetY;   // 当前眼位 y 偏移（负 = 降低）
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

            // 目标偏移 = 电机发布的目标眼位 - 站立眼位基准（站立 = 0，蹲伏 = 负）
            float targetOffset = snapshot.TargetEyeHeight - snapshot.StandingEyeHeight;

            _offsetY = Mathf.SmoothDamp(_offsetY, targetOffset, ref _smoothVel, _profile.crouchEye.transitionTime);

            if (Mathf.Abs(_offsetY) > 0.0001f)
            {
                rig.AddPositionOffset(new Vector3(0f, _offsetY, 0f));
            }
        }
    }
}
