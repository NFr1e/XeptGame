namespace XeptKit.Localization
{
    /// <summary>
    /// 语言切换事件——当 <see cref="ILocalizationManager.SetLanguageAsync"/> 完成语言切换（状态已提交）后，
    /// 经事件总线同步广播。订阅方据此刷新 UI 文本、音频等依赖语言的内容。
    /// </summary>
    /// <remarks>
    /// 首次切换语言时 <see cref="PreviousLanguage"/> 为空串。
    /// 相同语言切换（短路）不广播；切换失败/取消不广播——事件仅在状态真正生效后触发。
    /// </remarks>
    public readonly struct LanguageChangedEvent
    {
        /// <summary>
        /// 切换后的语言代码（如 "zh-CN"、"en"、"ja"）。
        /// </summary>
        public string NewLanguage { get; }

        /// <summary>
        /// 切换前的语言代码。首次切换时为空串。
        /// </summary>
        public string PreviousLanguage { get; }

        /// <summary>
        /// 构造语言切换事件。
        /// </summary>
        /// <param name="newLanguage">切换后的语言代码。</param>
        /// <param name="previousLanguage">切换前的语言代码。首次切换时传入 <see cref="string.Empty"/>。</param>
        public LanguageChangedEvent(string newLanguage, string previousLanguage)
        {
            NewLanguage = newLanguage;
            PreviousLanguage = previousLanguage;
        }
    }
}
