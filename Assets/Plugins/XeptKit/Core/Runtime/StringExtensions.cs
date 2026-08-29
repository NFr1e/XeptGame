using System.Globalization;

namespace XeptKit.Core
{
    /// <summary>
    /// 字符串安全解析：null/空白返回 false，不抛异常；使用 InvariantCulture 避免本地化差异。
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>安全解析 int：失败（null/空白/格式错误）返回 false，并输出 0。</summary>
        public static bool TryParseInt(this string value, out int result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = 0;
                return false;
            }

            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        /// <summary>安全解析 float：失败（null/空白/格式错误）返回 false，并输出 0。</summary>
        public static bool TryParseFloat(this string value, out float result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = 0f;
                return false;
            }

            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }
    }
}
