using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// HeadBob 效果源（3C_CameraFeel_Design.md §4.1）：行走摆动。
    /// 实现要点：
    /// - **位移积分相位**：phase += 水平速度 × frequencyPerMeter × dt——慢走摆慢、快跑摆快，
    ///   急停相位即停，天然免"速度阈值停摆"逻辑；
    /// - **波形**：横摆 sin(phase)；竖摆 |sin(phase×2)|（频率 = 横摆 2 倍，每步"踩下-抬起"）；
    /// - **档位调制**：Sprint 大而快（amplitude/frequency 倍率）、Crouch 小而慢、Idle 无（速度阈值排除）；
    /// - **落地恢复窗口**：订阅 Context.Landing 记录时间戳，落地后 landingRecoveryTime 内抑制 bob
    ///   （避免与 LandingKick 弹簧打架）；
    /// - **停步**：偏移经 SmoothDamp 平滑回零（惯性滑停，非瞬间冻结）。
    /// 输出：AddPositionOffset（本地空间；只动位置，不动旋转——roll bob 留待 AddRotationOffset）。
    /// </summary>
    public sealed class HeadBob : MonoBehaviour
    {
        [Tooltip("观感参数资产；为空时使用内置默认值")]
        [SerializeField] private PlayerCameraFeelProfile viewProfile;

        [Tooltip("角色控制器（Player 根）；留空时自动从父级查找")]
        [SerializeField] private PlayerController player;

        [Tooltip("相机合成器（本物体）；留空时自动查找")]
        [SerializeField] private CameraRig rig;

        private PlayerCameraFeelProfile _profile;
        private PlayerMotorContext _ctx;

        private float _phase;          // 位移积分相位
        private float _offsetX;        // 平滑后当前偏移（SmoothDamp 输出）
        private float _offsetY;
        private float _smoothVelX;
        private float _smoothVelY;
        private float _timeSinceLanding = float.MaxValue; // 落地恢复窗口计时（未落地过则恒允许 bob）

        private void Awake()
        {
            _profile = viewProfile != null ? viewProfile : PlayerCameraFeelProfile.Default;
            player ??= GetComponentInParent<PlayerController>();
            rig ??= GetComponent<CameraRig>();
        }

        private void Start()
        {
            // MotorContext 在 PlayerController.Awake 装配——所有 Awake 先于 Start 完成，
            // 故在此捕获并订阅 Landing（效果源 Awake 与 PlayerController.Awake 顺序无保证）。
            _ctx = player != null ? player.MotorContext : null;
            if (_ctx != null)
            {
                _ctx.Landing += OnLanding;
            }
        }

        private void OnDestroy()
        {
            if (_ctx != null)
            {
                _ctx.Landing -= OnLanding;
            }
        }

        private void OnLanding(MotorLandingInfo info)
        {
            _timeSinceLanding = 0f;
        }

        private void Update()
        {
            if (player == null || rig == null)
            {
                return;
            }

            var snapshot = player.GetFeelSnapshot();
            var bob = _profile.headBob;

            _timeSinceLanding += Time.deltaTime;

            // 激活条件：稳定接地 + 超出落地恢复窗口 + 水平速度超过阈值（Idle 天然排除）
            bool bobActive = snapshot.IsGrounded
                && _timeSinceLanding > bob.landingRecoveryTime
                && snapshot.HorizontalSpeed > bob.speedThreshold;

            float targetX = 0f;
            float targetY = 0f;

            if (bobActive)
            {
                // 档位调制（蹲伏与冲刺互斥；Sprint 振幅 1.4~1.6×、频率 1.2~1.3×，Crouch 0.3~0.4×/0.7×）
                float ampScale = 1f;
                float freqScale = 1f;
                if (snapshot.IsCrouching)
                {
                    ampScale = bob.crouchAmplitudeScale;
                    freqScale = bob.crouchFrequencyScale;
                }
                else if (snapshot.IsSprinting)
                {
                    ampScale = bob.sprintAmplitudeScale;
                    freqScale = bob.sprintFrequencyScale;
                }

                // 位移积分相位（速度驱动；频率倍率作用在相位累积上，快跑自然摆得更快）
                _phase += snapshot.HorizontalSpeed * bob.frequencyPerMeter * freqScale * Time.deltaTime;

                targetX = Mathf.Sin(_phase) * bob.walkAmplitude * ampScale;
                targetY = Mathf.Abs(Mathf.Sin(_phase * 2f)) * bob.walkAmplitude * ampScale;
            }

            // SmoothDamp 平滑（停步/抑制时目标为零 → 惯性滑停回零）
            _offsetX = Mathf.SmoothDamp(_offsetX, targetX, ref _smoothVelX, bob.smoothTime);
            _offsetY = Mathf.SmoothDamp(_offsetY, targetY, ref _smoothVelY, bob.smoothTime);

            if (Mathf.Abs(_offsetX) > 0.0001f || Mathf.Abs(_offsetY) > 0.0001f)
            {
                rig.AddPositionOffset(new Vector3(_offsetX, _offsetY, 0f));
            }
        }
    }
}
