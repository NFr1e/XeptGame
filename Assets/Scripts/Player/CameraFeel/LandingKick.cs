using UnityEngine;
using XeptKit.Core;

namespace XeptGame.Player
{
    /// <summary>
    /// 落地缓冲效果源（3C_CameraFeel_Design.md §4.2）：消费 <see cref="PlayerMotorContext.Landing"/>
    /// 事件，落地瞬间注入两通道弹簧脉冲 + 轻量 pitch 下压：
    /// <list type="bullet">
    /// <item><b>垂直通道</b>：相机下沉（模拟腿部吸收冲击）后阻尼弹簧回弹——冲击质量感；</item>
    /// <item><b>水平通道</b>：沿落地瞬间水平速度方向偏移（残余动量，"身体被向前带"）后缓慢归位——落地惯性感；</item>
    /// <item><b>pitch 通道</b>：轻量俯仰下压（<see cref="PlayerLookController.AddPunch"/>，幅度小、不干扰瞄准）。</item>
    /// </list>
    /// 强度 ∝ 落地速度（minFallSpeed 以下无感，maxFallSpeed 以上满幅；滑落触地天然无感）。
    /// 弹簧脉冲经 <see cref="SpringDamper.ImpulseToPeak"/>（临界阻尼峰值精确控制，v₀ = 幅度·ω·e）。
    /// 组件挂相机层级（眼位挂点）：PlayerController 经 GetComponentInParent 自动定位，不强制 Inspector 指定。
    /// </summary>
    public sealed class LandingKick : MonoBehaviour
    {
        [Tooltip("观感参数资产；为空时使用内置默认值")]
        [SerializeField] private PlayerCameraFeelProfile viewProfile;

        [Tooltip("角色控制器（Player 根）；留空时自动从父级查找")]
        [SerializeField] private PlayerController player;

        [Tooltip("相机合成器（本物体）；留空时自动查找")]
        [SerializeField] private CameraRig rig;

        private PlayerCameraFeelProfile _profile;
        private PlayerMotorContext _ctx;

        private SpringDamper _springY;      // 垂直下沉通道
        private SpringDamper _springHoriz;  // 水平惯性偏移通道（沿 _horizDirection）
        private Vector2 _horizDirection;    // 水平偏移方向（落地时按水平速度锁定）

        private void Awake()
        {
            _profile = viewProfile != null ? viewProfile : PlayerCameraFeelProfile.Default;
            player ??= GetComponentInParent<PlayerController>();
            rig ??= GetComponent<CameraRig>();

            var kick = _profile.landingKick;
            _springY = new SpringDamper(kick.springFreq, kick.damping);
            _springHoriz = new SpringDamper(kick.horizontalSpringFreq, kick.damping);
        }

        private void Start()
        {
            // MotorContext 在 PlayerController.Awake 装配——所有 Awake 先于 Start 完成，
            // 故在此捕获并订阅 Landing（效果源 Awake 与 PlayerController.Awake 顺序无保证）。
            _ctx = player != null ? player.MotorContext : null;
            if (_ctx == null)
            {
                Log.Warning("[LandingKick] 未找到 PlayerMotorContext（Player 根未挂 PlayerController 或未装配 KCC 电机），落地缓冲不可用。");
                return;
            }

            _ctx.Landing += OnPlayerLanded;
        }

        private void OnDestroy()
        {
            if (_ctx != null)
            {
                _ctx.Landing -= OnPlayerLanded;
            }
        }

        private void OnPlayerLanded(MotorLandingInfo info)
        {
            var kick = _profile.landingKick;
            var strength = Mathf.InverseLerp(kick.minFallSpeed, kick.maxFallSpeed, info.ImpactSpeed);
            if (strength <= 0f)
            {
                return; // 低速度落地（步行/小台阶）无感
            }

            // 垂直通道：向下脉冲 → 相机下沉（ImpulseToPeak：峰值精确 = dipDepth × strength）
            _springY.ImpulseToPeak(-kick.dipDepth * strength);

            // 水平通道：方向 = 落地瞬间水平速度方向（残余动量，向前带）；
            // 强度 = 水平速度联动（horizontalSpeedRef 满幅）× 冲击强度。
            var velocity = _ctx.Motor.Velocity;
            var horiz = new Vector2(velocity.x, velocity.z);
            var horizSpeed = horiz.magnitude;
            if (horizSpeed > 0.1f)
            {
                _horizDirection = horiz / horizSpeed;
                var horizStrength = Mathf.Clamp01(horizSpeed / kick.horizontalSpeedRef) * strength;
                _springHoriz.ImpulseToPeak(kick.horizontalDip * horizStrength);
            }

            // pitch 通道：轻量下压（正 pitch = 低头；幅度小、不干扰瞄准）
            if (player != null && player.LookController != null)
            {
                player.LookController.AddPunch(new Vector2(0f, kick.pitchPunch * strength));
            }
        }

        private void Update()
        {
            if (rig == null)
            {
                return;
            }

            _springY.Update(Time.deltaTime);
            _springHoriz.Update(Time.deltaTime);

            // 合成偏移：y = 垂直弹簧值（负 = 下沉）；xz = 水平弹簧值 × 锁定方向。
            // 直接写本地偏移，避免 Vector3.down × 负值 反号成上浮。
            var offset = new Vector3(
                _horizDirection.x * _springHoriz.Value,
                _springY.Value,
                _horizDirection.y * _springHoriz.Value);

            if (offset.sqrMagnitude > 0.0001f * 0.0001f)
            {
                rig.AddPositionOffset(offset);
            }
        }
    }
}
