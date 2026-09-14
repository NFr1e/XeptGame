using System.Collections.Generic;
using System.Text;
using XeptKit.Core;

namespace XeptGame.UI
{
    /// <summary>
    /// 本地化取词收口（Backpack_UI_Design.md B5；Interaction_Behaviour_Design.md D3 的通用形态）：
    /// <b>代码只存键，句子在 CSV</b>；本类是界面层唯一查表点，呈现件不自行查表、不拼接字符串。
    /// <list type="bullet">
    /// <item>命中 → 返回译文（带 <c>args</c> 时走 <c>string.Format</c> 填 <c>{0}</c> 等占位符）；</item>
    /// <item>未命中 / 本地化未就绪 → <b>回退为键本身并记一次 Warning</b>（绝不返回空文案——宁可显示
    /// <c>backpack.title</c> 也不让玩家看到空行，同时把缺键暴露到 Console）；</item>
    /// <item>告警按 key 去重，避免逐帧刷屏。</item>
    /// </list>
    /// 消费方：<c>InteractionPromptText</c>（交互动作行）、<c>BackpackUIFormLogic</c>（背包界面）。
    /// </summary>
    public static class LocalizedText
    {
        private static readonly HashSet<string> Reported = new(System.StringComparer.Ordinal);

        /// <summary>取词；键为空 → 返回空串（消费方据此隐藏该处）。</summary>
        public static string Resolve(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            var manager = AppEntry.LocalizationManager;
            if (manager != null && manager.TryGet(key, out var value) && !string.IsNullOrEmpty(value))
            {
                return args == null || args.Length == 0 ? value : Format(key, value, args);
            }

            Report(key);
            return key;
        }

        /// <summary>格式化容错：占位符与实参不匹配时保留原文并记告警（不抛、不显示半截文案）。</summary>
        private static string Format(string key, string format, object[] args)
        {
            try
            {
                return string.Format(format, args);
            }
            catch (System.FormatException)
            {
                Warn("[LocalizedText] 文案键 \"{0}\" 的占位符与实参不匹配（格式串：\"{1}\"）——按原文显示。", key, format);
                return format;
            }
        }

        private static void Report(string key)
        {
            Warn("[LocalizedText] 文案键未命中本地化表：\"{0}\"（回退显示键名；请检查 CSV 是否已导入生成语言包资产）。", key);
        }

        private static void Warn(string format, params object[] args)
        {
            var message = new StringBuilder().AppendFormat(format, args).ToString();
            if (Reported.Add(message))
            {
                Log.Warning(message);
            }
        }
    }
}
