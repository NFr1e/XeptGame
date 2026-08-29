using System.Globalization;
using XeptKit.Core;

namespace XeptKit.Localization
{
    /// <summary>
    /// <see cref="ILocalizationManager"/> 的便利扩展方法。
    /// 类型化解析均走 <see cref="ILocalizationManager.TryGet"/> 安全路径（不记日志、不抛），
    /// 数值解析统一 <see cref="CultureInfo.InvariantCulture"/> 保证跨语言环境一致性。
    /// 注：扩展定义于接口上，仅对管理器实例可用（业务经组合根持有的实例调用）。
    /// </summary>
    public static class LocalizationExtensions
    {
        /// <summary>
        /// 查表，key 缺失时返回 <paramref name="defaultValue"/> 而非占位符。
        /// 等价于 <c>TryGet(key, out var v) ? v : defaultValue</c>。
        /// </summary>
        public static string GetOrDefault(this ILocalizationManager manager, string key, string defaultValue = "")
        {
            Guard.NotNull(manager, nameof(manager));
            return manager.TryGet(key, out var value) ? value : defaultValue;
        }

        /// <summary>尝试按 key 获取 int 值（InvariantCulture 解析，跨语言环境一致）。</summary>
        public static bool TryGetInt(this ILocalizationManager manager, string key, out int value)
        {
            Guard.NotNull(manager, nameof(manager));
            value = 0;
            return manager.TryGet(key, out var str)
                   && int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>尝试按 key 获取 float 值（InvariantCulture 解析，跨语言环境一致）。</summary>
        public static bool TryGetFloat(this ILocalizationManager manager, string key, out float value)
        {
            Guard.NotNull(manager, nameof(manager));
            value = 0f;
            return manager.TryGet(key, out var str)
                   && float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>尝试按 key 获取 bool 值。</summary>
        public static bool TryGetBool(this ILocalizationManager manager, string key, out bool value)
        {
            Guard.NotNull(manager, nameof(manager));
            value = false;
            return manager.TryGet(key, out var str)
                   && bool.TryParse(str, out value);
        }
    }
}
