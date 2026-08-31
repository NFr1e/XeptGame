using UnityEngine;

namespace XeptKit.Core
{
    /// <summary>
    /// 阻尼弹簧（纯逻辑，结构体，零分配）：位置/速度状态经 <see cref="Update"/> 每帧积分，
    /// <see cref="Impulse"/> 施加速度冲量。用于观感层效果的"被踢一下自然回位"平滑
    /// （落地镜头缓冲、后坐力、震屏等，见 docs/modules/3C_CameraFeel_Design.md §4.2）。
    ///
    /// 参数：
    /// - ω（omega）：角频率（rad/s），越大回弹越快；
    /// - ζ（zeta）：阻尼比，1 = 临界阻尼（无过冲、单调回零），&lt;1 欠阻尼（带回荡）。
    ///
    /// 临界阻尼脉冲反推：ζ=1 时脉冲响应峰值 x* = |v₀|/(ω·e)，故 <see cref="ImpulseToPeak"/>
    /// 可让峰值**精确**等于目标幅度——冲量强度连续可调（轻落踢得轻、重落踢得重），
    /// 且连续快速脉冲自然累积、弹簧自动收敛。
    /// </summary>
    public struct SpringDamper
    {
        private readonly float _omega;
        private readonly float _zeta;

        public SpringDamper(float omega, float zeta)
        {
            _omega = omega;
            _zeta = zeta;
            Value = 0f;
            Velocity = 0f;
        }

        /// <summary>当前输出（位置）。</summary>
        public float Value { get; private set; }

        /// <summary>当前速度。</summary>
        public float Velocity { get; private set; }

        /// <summary>施加速度冲量（叠加）。</summary>
        public void Impulse(float velocity) => Velocity += velocity;

        /// <summary>
        /// 按目标峰值反推初速施加脉冲：v₀ = peak × ω × e。
        /// 临界阻尼（ζ=1）下峰值精确等于 peak；ζ≠1 时为近似（欠阻尼峰值略高于目标）。
        /// </summary>
        public void ImpulseToPeak(float peak) => Impulse(peak * _omega * Mathf.Exp(1f));

        /// <summary>复位到零（清空动量和位置）。</summary>
        public void Reset()
        {
            Value = 0f;
            Velocity = 0f;
        }

        /// <summary>
        /// 每帧积分（半隐式欧拉：先用加速度更新速度，再用新速度更新位置）。
        /// 稳定性：ω·dt &lt; 2 时收敛；常规观感参数（ω ≤ 15、dt ≈ 1/60）远低于该界限。
        /// </summary>
        public void Update(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            // 标准阻尼振荡方程：a = -2ζω·v - ω²·x
            float acceleration = -2f * _zeta * _omega * Velocity - _omega * _omega * Value;
            Velocity += acceleration * deltaTime;
            Value += Velocity * deltaTime;
        }
    }
}
