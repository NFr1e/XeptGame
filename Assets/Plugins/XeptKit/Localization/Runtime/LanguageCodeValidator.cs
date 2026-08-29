using System.Text.RegularExpressions;

namespace XeptKit.Localization
{
    /// <summary>
    /// 语言代码校验（BCP-47 形状子集）。
    /// 轻量形状校验：语言子标签 2-8 个字母 + 可选连字符扩展段（如 "en"、"zh-CN"、"pt-BR"）。
    /// 非严格 BCP-47 全量规范——拼错的代码其失败本就响亮（地址拼接后加载必抛 AssetLoadException），
    /// 形状校验的价值仅在更早、更清晰的报错位置（入口 ArgumentException 而非深层加载异常）。
    /// </summary>
    /// <remarks>
    /// 不归一化大小写/连字符：语言代码按字面精确匹配（地址 = 前缀 + 代码，逐字一致）；
    /// 归一化会引入隐藏映射、静默破坏地址匹配，而精确匹配的失败是响亮的，安全。
    /// 不设白名单：支持的语种由项目内容（语言包资产集合）自然表达。
    /// </remarks>
    public static class LanguageCodeValidator
    {
        private static readonly Regex ShapeRegex = new Regex(
            @"^[a-zA-Z]{2,8}(-[a-zA-Z0-9]{1,8})*$", RegexOptions.Compiled);

        /// <summary>
        /// 校验语言代码形状。null / 空白返回 false（不抛）——调用方语义为"校验失败返回 false"。
        /// </summary>
        public static bool IsValid(string code)
        {
            return !string.IsNullOrEmpty(code) && ShapeRegex.IsMatch(code);
        }
    }
}
