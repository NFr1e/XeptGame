using System;

namespace XeptKit.Localization
{
    /// <summary>
    /// 本地化数据源抽象——从外部文件格式（CSV、Google Sheets 等）读取待导入的本地化原始数据。
    /// 扩展方式：实现此接口以支持新的数据源；默认提供 <see cref="CsvLocalizationDataSource"/> 作为 CSV 文件读取器。
    /// </summary>
    /// <remarks>
    /// 数据源类位于 Editor 程序集（导入是编辑期能力）；原始数据为中间格式，
    /// 尚未分配到单语言 <see cref="LocalizationData"/> 资产，由 <see cref="LocalizationImporterWindow"/> 完成分配。
    /// </remarks>
    public interface ILocalizationDataSource
    {
        /// <summary>
        /// 从 <paramref name="sourcePath"/> 读取本地化数据。
        /// </summary>
        /// <param name="sourcePath">数据源路径（如 CSV 文件路径）。</param>
        /// <returns>解析后的本地化原始数据。</returns>
        /// <exception cref="System.IO.FileNotFoundException">文件不存在。</exception>
        /// <exception cref="System.IO.InvalidDataException">格式非法（文件为空、表头不合规等）。</exception>
        LocalizationRawData Read(string sourcePath);
    }

    /// <summary>
    /// 从外部数据源解析出的本地化原始数据——尚未分配到单语言 SO 的中间格式。
    /// </summary>
    public sealed class LocalizationRawData
    {
        /// <summary>
        /// 检测到的语言代码列表（如 ["en", "zh-CN", "ja"]）。
        /// </summary>
        public string[] LanguageCodes = Array.Empty<string>();

        /// <summary>
        /// 全部条目的列表。每条包含一个 Key 和按 <see cref="LanguageCodes"/> 索引对齐的多语言翻译。
        /// </summary>
        public LocalizationRawEntry[] Entries = Array.Empty<LocalizationRawEntry>();
    }

    /// <summary>
    /// 本地化原始条目——一个 Key 的多种语言翻译。
    /// </summary>
    public sealed class LocalizationRawEntry
    {
        /// <summary>
        /// 本地化键。
        /// </summary>
        public string Key = string.Empty;

        /// <summary>
        /// 各语言的翻译值，索引与 <see cref="LocalizationRawData.LanguageCodes"/> 一一对应。
        /// </summary>
        public string[] Values = Array.Empty<string>();
    }
}
