using System;
using UnityEngine;

namespace XeptKit.Localization
{
    /// <summary>
    /// 语言包资产：一种语言的全体本地化键值对。
    /// 每语言一个资产文件，经 Asset 模块按地址加载（地址 = 前缀 + 语言代码，见 <see cref="ILocalizationManager"/>）。
    /// </summary>
    /// <remarks>
    /// 创建：Project 窗口右键 → Create → XeptKit → Localization Data。
    /// 典型用法：每语言创建一个 <see cref="LocalizationData"/> 资产，经 LocalizationImporter 从 CSV 批量生成，
    /// 运行时由 <see cref="LocalizationManager"/> 经 <see cref="XeptKit.Asset.IAssetLoader"/> 加载。
    /// </remarks>
    [CreateAssetMenu(menuName = "XeptKit/Localization Data", fileName = "Localization Data")]
    public sealed class LocalizationData : ScriptableObject
    {
        /// <summary>
        /// 语言代码（BCP-47 标签，如 "en"、"zh-CN"、"ja"），同一项目内每语言唯一。
        /// 编辑期元数据：运行时语言代码的权威是加载地址后缀，二者一致性由导入器保证
        /// （加载时若字段与请求代码不符会记 Warning，见 LocalizationManager）。
        /// </summary>
        [Tooltip("语言代码（BCP-47 标签，如 en、zh-CN、ja）。编辑期元数据，运行时以加载地址为权威。")]
        public string LanguageCode = "en";

        /// <summary>
        /// 回退语言代码：key 在当前语言中缺失时从此语言查找；与 <see cref="LanguageCode"/> 相同则回退直接跳过。
        /// </summary>
        [Tooltip("缺省回退语言代码。key 在当前语言缺失时从此语言查找。")]
        public string FallbackLanguageCode = "en";

        /// <summary>
        /// 该语言包的全体键值对条目。
        /// </summary>
        [Tooltip("该语言包的全部键值对条目。")]
        public LocalizationEntry[] Entries = Array.Empty<LocalizationEntry>();
    }

    /// <summary>
    /// 本地化条目的单个键值对。
    /// </summary>
    [Serializable]
    public struct LocalizationEntry
    {
        /// <summary>
        /// 本地化键——跨语言唯一标识（如 "menu_start"、"hud_health"）。
        /// </summary>
        [Tooltip("本地化键——跨语言唯一标识，例如 menu_start。")]
        public string Key;

        /// <summary>
        /// 该键在当前语言下的翻译文本。可含 {0}、{1} 占位符，由 <see cref="ILocalizationManager.Get(string, object[])"/> 运行时填充。
        /// </summary>
        [Tooltip("当前语言下的翻译文本。可含 {0}、{1} 等格式化占位符。")]
        public string Value;
    }
}
