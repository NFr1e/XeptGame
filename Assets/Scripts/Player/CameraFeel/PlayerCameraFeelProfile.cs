using UnityEngine;
using XeptGame.Core;

namespace XeptGame.Player
{
    /// <summary>
    /// 相机观感参数配表（3C_CameraFeel_Design.md §5）：单资产三子区（FOV / LandingKick / HeadBob）。
    /// 对标 PlayerLookProfile / PlayerMotorProfile；**热调参**：每帧直读字段，Play Mode 下
    /// Inspector 改动即生效（SO 序列化字段改动即应用）。
    /// 字段默认值由子区类初始化提供（CreateAssetMenu 新建资产即有可用默认值）。
    /// </summary>
    [CreateAssetMenu(
        menuName = XeptGameConsts.Editor.PlayerCameraFeelProfileMenuName,
        fileName = XeptGameConsts.Editor.PlayerCameraFeelProfileFileName,
        order = XeptGameConsts.Editor.PlayerCameraFeelProfileOrder)]
    public class PlayerCameraFeelProfile : ScriptableObject
    {
        [System.Serializable]
        public class FovSection
        {
            [Header("FOV 目标（度）")]
            [Tooltip("基准视野")]
            public float fovIdle = 60f;

            [Tooltip("蹲伏视野（收窄）")]
            public float fovCrouch = 55f;

            [Tooltip("冲刺视野（展开）")]
            public float fovSprint = 70f;

            [Header("平滑")]
            [Tooltip("FOV 过渡时间（秒；帧率无关指数平滑的 τ）")]
            public float fovSmoothTime = 0.1f;
        }

        [System.Serializable]
        public class LandingKickSection
        {
            [Header("弹簧")]
            [Tooltip("垂直通道角频率 ω（rad/s；越大回弹越快）")]
            public float springFreq = 10f;

            [Tooltip("阻尼比 ζ（1 = 临界阻尼，无过冲单调回零）")]
            public float damping = 1f;

            [Header("强度映射")]
            [Tooltip("低于该下落速度（m/s）落地无感")]
            public float minFallSpeed = 3f;

            [Tooltip("高于该下落速度（m/s）落地满幅")]
            public float maxFallSpeed = 12f;

            [Header("垂直通道")]
            [Tooltip("相机下沉幅度（峰值，米；负方向为下沉）")]
            public float dipDepth = 0.08f;

            [Header("水平通道")]
            [Tooltip("水平通道角频率 ω（rad/s）")]
            public float horizontalSpringFreq = 5f;

            [Tooltip("落地水平速度满幅参考值（m/s；达到该值时前带满幅）")]
            public float horizontalSpeedRef = 8f;

            [Tooltip("水平前带幅度（峰值，米）")]
            public float horizontalDip = 0.04f;

            [Header("pitch 通道")]
            [Tooltip("轻量俯仰下压（度；正 = 低头，幅度小、不干扰瞄准）")]
            public float pitchPunch = 1.5f;
        }

        [System.Serializable]
        public class HeadBobSection
        {
            [Header("振幅（米）")]
            [Tooltip("行走竖摆振幅（y；竖摆频率 = 横摆 2 倍）")]
            public float walkAmplitude = 0.03f;

            [Header("频率")]
            [Tooltip("相位累积系数（每米弧长；位移积分：phase += 水平速度 × 此值 × dt）")]
            public float frequencyPerMeter = 6f;

            [Header("档位调制（倍数）")]
            [Tooltip("冲刺振幅倍率（约 1.4~1.6）")]
            public float sprintAmplitudeScale = 1.5f;

            [Tooltip("冲刺频率倍率（约 1.2~1.3）")]
            public float sprintFrequencyScale = 1.25f;

            [Tooltip("蹲伏振幅倍率（约 0.3~0.4）")]
            public float crouchAmplitudeScale = 0.35f;

            [Tooltip("蹲伏频率倍率（约 0.7）")]
            public float crouchFrequencyScale = 0.7f;

            [Header("平滑/抑制")]
            [Tooltip("偏移平滑时间（秒；SmoothDamp，停步惯性滑停）")]
            public float smoothTime = 0.08f;

            [Tooltip("落地后抑制 bob 的窗口（秒；避免与 LandingKick 弹簧打架）")]
            public float landingRecoveryTime = 0.2f;

            [Tooltip("水平速度低于该值（m/s）无 bob")]
            public float speedThreshold = 0.5f;
        }

        [System.Serializable]
        public class JumpInertiaSection
        {
            [Header("幅度（度）")]
            [Tooltip("最大俯仰偏移（正 = 低头）。起跳身体上加速 → 头部惯性滞后 → 下偏；越过最高点下落 → 上偏")]
            public float maxPitchOffset = 2.5f;

            [Tooltip("垂直速度映射系数（度/(m/s)）：pitchOffset = clamp(v_y × 此值, ±max)")]
            public float velocityScale = 0.15f;

            [Header("平滑/门控")]
            [Tooltip("平滑时间（秒；指数/阻尼平滑——v_y 是物理帧步进值，渲染帧必须平滑）")]
            public float smoothingTime = 0.12f;

            [Tooltip("死区（m/s）：|v_y| 低于此值无偏移（最高点附近微抖消除）")]
            public float deadZone = 1f;
        }

        [System.Serializable]
        public class CrouchEyeSection
        {
            [Header("过渡")]
            [Tooltip("眼位降低/恢复过渡时间（秒；SmoothDamp）。眼位**目标**由电机域发布（胶囊顶部），此处只调过渡手感")]
            public float transitionTime = 0.12f;
        }

        [Header("FOV")]
        public FovSection fov = new();

        [Header("LandingKick")]
        public LandingKickSection landingKick = new();

        [Header("HeadBob")]
        public HeadBobSection headBob = new();

        [Header("JumpInertia")]
        public JumpInertiaSection jumpInertia = new();

        [Header("CrouchEye")]
        public CrouchEyeSection crouchEye = new();

        /// <summary>运行时默认配置（未挂资产时使用）。非资产实例，不可在资源库中编辑。</summary>
        public static PlayerCameraFeelProfile Default
        {
            get
            {
                var profile = CreateInstance<PlayerCameraFeelProfile>();
                profile.name = "PlayerCameraFeelProfile_Default";
                return profile;
            }
        }
    }
}
