using UnityEngine;
using XeptKit.Core;

namespace XeptGame.Player
{
    /// <summary>
    /// 视角控制器（纯逻辑）：视角角度（Yaw/Pitch）的唯一权威持有者与维护者。
    ///
    /// 职责边界（见 Look 域设计决议）：
    /// - 玩家输入：经 <see cref="ApplyDelta"/> 进入（灵敏度/反转/累加/俯仰限制在此应用）；
    /// - 非输入系统：武器后坐力、受击镜头、过场接管等经 <see cref="AddPunch"/> 施加偏移——
    ///   不模拟玩家输入，直接操作视角角度；
    /// - 消费者（PlayerLook）只读 <see cref="Yaw"/>/<see cref="Pitch"/> 应用旋转，
    ///   不持有、不修改角度。
    /// 配置来自 <see cref="PlayerInputSettings"/> 的 Look 区（默认值由 PlayerLookProfile 配表提供）。
    /// </summary>
    public class PlayerLookController
    {
        private readonly PlayerInputSettings _settings;

        /// <summary>当前水平角（度，可无限累加，不限制）。</summary>
        public float Yaw { get; private set; }

        /// <summary>当前俯仰角（度，已限制在 [MinPitch, MaxPitch]）。</summary>
        public float Pitch { get; private set; }

        public PlayerLookController(PlayerInputSettings settings)
        {
            Guard.NotNull(settings, nameof(settings));
            _settings = settings;
        }

        /// <summary>
        /// 应用玩家视角增量（每帧由 PlayerLook 驱动；delta 为输入层产出的本帧 Look 增量，
        /// 已做摇杆死区预处理，此处应用灵敏度、反转与俯仰限制）。
        /// </summary>
        public void ApplyDelta(Vector2 delta)
        {
            var invertY = _settings.InvertY.Value;
            var y = invertY ? delta.y : -delta.y;

            Yaw += delta.x * _settings.SensitivityX.Value;
            Pitch += y * _settings.SensitivityY.Value;
            Pitch = Mathf.Clamp(Pitch, _settings.MinPitch.Value, _settings.MaxPitch.Value);
        }

        /// <summary>
        /// 施加非输入视角偏移（角度增量，同样受俯仰限制约束）。
        /// 预留接口：武器后坐力、受击镜头、震屏、过场运镜等"非玩家输入"的视角修改统一走这里，
        /// 避免各系统直接写视角状态造成写入者混乱。
        /// </summary>
        public void AddPunch(Vector2 offset)
        {
            Yaw += offset.x;
            Pitch = Mathf.Clamp(Pitch + offset.y, _settings.MinPitch.Value, _settings.MaxPitch.Value);
        }

        /// <summary>重置视角角度（重生/初始化/过场接管退出时使用）。</summary>
        public void Reset(float yaw = 0f, float pitch = 0f)
        {
            Yaw = yaw;
            Pitch = Mathf.Clamp(pitch, _settings.MinPitch.Value, _settings.MaxPitch.Value);
        }
    }
}
