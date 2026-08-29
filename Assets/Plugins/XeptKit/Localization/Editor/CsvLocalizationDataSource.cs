using System;
using System.Collections.Generic;
using System.IO;
using XeptKit.Core;

namespace XeptKit.Localization
{
    /// <summary>
    /// <see cref="ILocalizationDataSource"/> 的 CSV 文件实现。
    /// </summary>
    /// <remarks>
    /// CSV 格式要求：
    /// <list type="bullet">
    ///   <item>第一行为表头：第一个单元格固定为 <c>key</c>，后续单元格为语言代码（如 <c>en</c>、<c>zh-CN</c>）。</item>
    ///   <item>后续行为数据行：第一个单元格为本地化键，后续单元格为该键对应语言的翻译文本。</item>
    ///   <item>支持双引号包裹的字段（如 <c>"Hello, World"</c>）与转义引号（<c>""</c> → <c>"</c>）。</item>
    /// </list>
    /// 示例：
    /// <code>
    /// key,en,zh-CN,ja
    /// menu_start,Start Game,开始游戏,ゲームスタート
    /// menu_settings,Settings,设置,設定
    /// </code>
    /// </remarks>
    public sealed class CsvLocalizationDataSource : ILocalizationDataSource
    {
        /// <inheritdoc />
        public LocalizationRawData Read(string sourcePath)
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"CSV 文件不存在：{sourcePath}", sourcePath);
            }

            var lines = File.ReadAllLines(sourcePath);
            if (lines.Length == 0)
            {
                throw new InvalidDataException($"CSV 文件为空：{sourcePath}");
            }

            // 第一行：表头
            var headers = ParseCsvLine(lines[0]);
            if (headers.Length < 2)
            {
                throw new InvalidDataException(
                    $"CSV 表头至少需要 2 列（key + 至少一种语言），实际列数：{headers.Length}。文件：{sourcePath}");
            }

            if (!string.Equals(headers[0], "key", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"CSV 第一列表头必须为 \"key\"，实际值：\"{headers[0]}\"。文件：{sourcePath}");
            }

            // 语言代码 = 表头从第二列开始
            var languageCodes = new string[headers.Length - 1];
            Array.Copy(headers, 1, languageCodes, 0, languageCodes.Length);

            // 数据行
            var entries = new List<LocalizationRawEntry>();
            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex].Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                var cells = ParseCsvLine(line);
                if (cells.Length == 0 || string.IsNullOrEmpty(cells[0]))
                {
                    Log.Warning($"[CsvLocalizationDataSource] 第 {lineIndex + 1} 行的 Key 为空，已跳过。");
                    continue;
                }

                var entry = new LocalizationRawEntry
                {
                    Key = cells[0],
                    Values = new string[languageCodes.Length]
                };
                Array.Fill(entry.Values, string.Empty);

                int valuesToCopy = Math.Min(cells.Length - 1, languageCodes.Length);
                for (int i = 0; i < valuesToCopy; i++)
                {
                    entry.Values[i] = cells[i + 1];
                }

                entries.Add(entry);
            }

            return new LocalizationRawData
            {
                LanguageCodes = languageCodes,
                Entries = entries.ToArray()
            };
        }

        /// <summary>解析单行 CSV，处理双引号包裹字段与转义引号。</summary>
        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            int segmentStart = 0;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(ExtractField(line, segmentStart, i));
                    segmentStart = i + 1;
                }
            }

            // 最后一个字段
            result.Add(ExtractField(line, segmentStart, line.Length));

            return result.ToArray();
        }

        /// <summary>从线段中提取字段值，去除首尾空白与成对双引号，还原转义引号。</summary>
        private static string ExtractField(string line, int start, int end)
        {
            var segment = line.Substring(start, end - start).Trim();

            // 去除首尾成对的双引号
            if (segment.Length >= 2 && segment[0] == '"' && segment[segment.Length - 1] == '"')
            {
                segment = segment.Substring(1, segment.Length - 2);
                // 转义引号 "" → "
                segment = segment.Replace("\"\"", "\"");
            }

            return segment;
        }
    }
}
