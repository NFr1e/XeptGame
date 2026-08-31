using XeptKit.Core;

namespace XeptGame.Player
{
    public class PlayerInputSettings
    {
        #region Motor
        /// <summary>
        /// 冲刺输入模式：true=长按冲刺（按住激活/松开复位）；false=点击切换（按下翻转）
        /// </summary>
        public OverrideValue<bool> PressToSprint { get; } = new();

        /// <summary>
        /// 蹲伏输入模式：true=长按蹲伏（按住激活/松开复位）；false=点击切换（按下翻转）
        /// </summary>
        public OverrideValue<bool> PressToCrouch { get; } = new();

        public void SetDefaultMotor(PlayerMotorProfile profile)
        {
            PressToSprint.DefaultValue = profile.pressToSprint;
            PressToCrouch.DefaultValue = profile.pressToCrouch;
        }
        #endregion

        #region Look
        /// <summary>水平灵敏度（度/像素）。</summary>
        public OverrideValue<float> SensitivityX { get; } = new();

        /// <summary>垂直灵敏度（度/像素）。</summary>
        public OverrideValue<float> SensitivityY { get; } = new();

        /// <summary>俯仰角下限（度）。</summary>
        public OverrideValue<float> MinPitch { get; } = new();

        /// <summary>俯仰角上限（度）。</summary>
        public OverrideValue<float> MaxPitch { get; } = new();

        /// <summary>Y 轴反转。</summary>
        public OverrideValue<bool> InvertY { get; } = new();

        /// <summary>摇杆死区。</summary>
        public OverrideValue<float> StickDeadzone { get; } = new();

        public void SetDefaultLook(PlayerLookProfile profile)
        {
            SensitivityX.DefaultValue = profile.sensitivityX;
            SensitivityY.DefaultValue = profile.sensitivityY;
            MinPitch.DefaultValue = profile.minPitch;
            MaxPitch.DefaultValue = profile.maxPitch;
            InvertY.DefaultValue = profile.invertY;
            StickDeadzone.DefaultValue = profile.stickDeadzone;
        }
        #endregion
    }
}
