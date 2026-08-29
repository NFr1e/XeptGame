using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using XeptKit.Asset;
using XeptKit.Core;
using XeptKit.Event;

namespace XeptKit.Localization
{
    /// <summary>
    /// <see cref="ILocalizationManager"/> 默认实现。
    /// 语言包经 <see cref="IAssetLoader"/> 按地址约定加载：地址 = <see cref="_languagePackAddressPrefix"/> + languageCode
    /// （默认前缀 "Localization/"、地址示例 "Localization/zh-CN"）。
    /// </summary>
    /// <remarks>
    /// 回退链（两级）：目标语言字典 → 回退语言字典（语言包声明）→ "??key??" 占位符。
    /// 切换为原子提交：目标包 + 回退包全部就绪后一次性提交并广播；失败/取消保持原语言，已加载包保留在缓存（自愈）。
    /// 线程模型：主线程 only、无锁。
    /// </remarks>
    public sealed class LocalizationManager : ILocalizationManager
    {
        /// <summary>
        /// 语言包缓存条目：查表字典 + 回退语言代码——避免重复加载 SO 仅为了读回退字段。
        /// </summary>
        private sealed class CachedPack
        {
            public readonly Dictionary<string, string> Entries;
            public readonly string FallbackLanguageCode;

            public CachedPack(Dictionary<string, string> entries, string fallbackLanguageCode)
            {
                Entries = entries;
                FallbackLanguageCode = fallbackLanguageCode;
            }
        }

        private const string MissingKeyPlaceholderFormat = "??{0}??";

        private readonly IEventBus _eventBus;
        private readonly IAssetLoader _assetLoader;
        private readonly string _languagePackAddressPrefix;

        /// <summary>已加载语言包缓存：languageCode → CachedPack。</summary>
        private readonly Dictionary<string, CachedPack> _loadedPacks = new Dictionary<string, CachedPack>();

        /// <summary>查表上报去重集：(key, languageCode) → 已上报过（缺失/回退/格式化失败仅首次上报，防热路径刷屏）。</summary>
        private readonly HashSet<(string Key, string Language)> _reportedIssues = new HashSet<(string, string)>();

        private string _currentLanguage = "";
        private string _currentFallbackLanguage = "";
        private bool _switching;
        private string _switchingTarget;

        /// <summary>
        /// 构造 LocalizationManager。
        /// </summary>
        /// <param name="eventBus">用于广播 <see cref="LanguageChangedEvent"/> 的事件总线（构造注入，不静态依赖全局单例）。</param>
        /// <param name="assetLoader">用于加载 <see cref="LocalizationData"/> 语言包资产的加载器（构造注入）。</param>
        /// <param name="languagePackAddressPrefix">语言包地址前缀，实际加载地址 = prefix + languageCode，默认 "Localization/"。</param>
        public LocalizationManager(
            IEventBus eventBus,
            IAssetLoader assetLoader,
            string languagePackAddressPrefix = "Localization/")
        {
            Guard.NotNull(eventBus, nameof(eventBus));
            Guard.NotNull(assetLoader, nameof(assetLoader));
            Guard.NotNullOrEmpty(languagePackAddressPrefix, nameof(languagePackAddressPrefix));

            _eventBus = eventBus;
            _assetLoader = assetLoader;
            _languagePackAddressPrefix = languagePackAddressPrefix;
        }

        // ===============================================================
        // ILocalizationManager
        // ===============================================================

        /// <inheritdoc />
        public string CurrentLanguage => _currentLanguage;

        /// <inheritdoc />
        public string Get(string key)
        {
            Guard.NotNullOrEmpty(key, nameof(key));

            if (string.IsNullOrEmpty(_currentLanguage))
            {
                throw new InvalidOperationException(
                    "[Localization] 尚未初始化语言，请先调用 SetLanguageAsync()。");
            }

            if (TryLookup(key, out var value))
            {
                return value;
            }

            // 两级均未命中 → 占位符 + Error（按 (key, languageCode) 首次去重，防热路径刷屏）
            if (_reportedIssues.Add((key, _currentLanguage)))
            {
                if (string.IsNullOrEmpty(_currentFallbackLanguage) || _currentFallbackLanguage == _currentLanguage)
                {
                    Log.Error($"[Localization] Key \"{key}\" 在语言 \"{_currentLanguage}\" 中未找到。");
                }
                else
                {
                    Log.Error(
                        $"[Localization] Key \"{key}\" 在语言 \"{_currentLanguage}\" 与回退 " +
                        $"\"{_currentFallbackLanguage}\" 中均未找到。");
                }
            }

            return string.Format(MissingKeyPlaceholderFormat, key);
        }

        /// <inheritdoc />
        public string Get(string key, params object[] args)
        {
            var format = Get(key);
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException ex)
            {
                if (_reportedIssues.Add((key, _currentLanguage)))
                {
                    Log.Error(
                        $"[Localization] Key \"{key}\" 的格式化字符串 \"{format}\" 与参数不匹配：" + ex.Message);
                }

                return format;
            }
        }

        /// <inheritdoc />
        public bool TryGet(string key, out string value)
        {
            Guard.NotNullOrEmpty(key, nameof(key));

            if (string.IsNullOrEmpty(_currentLanguage))
            {
                value = null;
                return false;
            }

            return TryLookup(key, out value);
        }

        /// <inheritdoc />
        public async UniTask SetLanguageAsync(string languageCode, CancellationToken cancellationToken = default)
        {
            Guard.NotNullOrEmpty(languageCode, nameof(languageCode));
            if (!LanguageCodeValidator.IsValid(languageCode))
            {
                throw new ArgumentException($"非法语言代码：\"{languageCode}\"。", nameof(languageCode));
            }

            cancellationToken.ThrowIfCancellationRequested(); // 调用时已取消 → 直接抛 OCE，不发起加载

            if (_currentLanguage == languageCode)
            {
                return; // 短路：不广播
            }

            if (_switching)
            {
                if (_switchingTarget == languageCode)
                {
                    return; // 切换中重复请求同一目标：幂等返回（不抛）
                }

                throw new InvalidOperationException(
                    "[Localization] 语言切换进行中，请 await 上一次切换完成后再发起。");
            }

            _switching = true;
            _switchingTarget = languageCode;
            var previousLanguage = _currentLanguage;

            try
            {
                // 1. 目标语言包（未缓存则加载）
                var pack = await LoadPackAsync(languageCode, cancellationToken);

                // 2. 回退语言包：先校验回退代码（数据来源，fail-fast 于入口暴露配置错误），再于需要时加载
                var fallbackCode = pack.FallbackLanguageCode;
                if (string.IsNullOrEmpty(fallbackCode) || !LanguageCodeValidator.IsValid(fallbackCode))
                {
                    throw new ArgumentException(
                        $"[Localization] 语言包 \"{languageCode}\" 声明的回退语言代码非法：\"{fallbackCode}\"。");
                }

                if (fallbackCode != languageCode && !_loadedPacks.ContainsKey(fallbackCode))
                {
                    await LoadPackAsync(fallbackCode, cancellationToken);
                }

                // 3. 原子提交（目标包 + 回退包全部就绪后一次性生效）
                _currentLanguage = languageCode;
                _currentFallbackLanguage = fallbackCode;

                // 4. 广播（提交后，订阅方读到已生效的新状态；同步广播，低频一次性通知）
                _eventBus.Publish(new LanguageChangedEvent(languageCode, previousLanguage));
            }
            finally
            {
                _switching = false;
                _switchingTarget = null;
            }
        }

        /// <inheritdoc />
        public void Clear()
        {
            _loadedPacks.Clear();
            _currentLanguage = "";
            _currentFallbackLanguage = "";
            _switching = false;
            _switchingTarget = null;
            _reportedIssues.Clear();
        }

        // ===============================================================
        // 内部方法
        // ===============================================================

        /// <summary>加载（未缓存时）并返回指定语言的语言包；句柄提取条目后立即释放（无长期持有资产引用）。</summary>
        private async UniTask<CachedPack> LoadPackAsync(string languageCode, CancellationToken cancellationToken)
        {
            if (_loadedPacks.TryGetValue(languageCode, out var cached))
            {
                return cached;
            }

            var address = _languagePackAddressPrefix + languageCode;
            var handle = await _assetLoader.LoadAsync<LocalizationData>(address, cancellationToken);
            var data = handle.Result;
            handle.Dispose();

            // 语言代码一致性防御：资产字段与请求代码不符 → Warning（加载已成功，仅提示数据错误，不阻断）
            if (!string.Equals(data.LanguageCode, languageCode, StringComparison.Ordinal))
            {
                Log.Warning(
                    $"[Localization] 语言包 \"{address}\" 的 LanguageCode 字段为 \"{data.LanguageCode}\"，" +
                    $"与请求代码 \"{languageCode}\" 不一致，请检查资产配置。");
            }

            var pack = new CachedPack(BuildDictionary(data.Entries), data.FallbackLanguageCode);
            _loadedPacks[languageCode] = pack;
            return pack;
        }

        /// <summary>将条目序列转换为查表字典；空 Key 跳过、重复 Key 后者覆盖，均记 Warning（构建期一次性）。</summary>
        private static Dictionary<string, string> BuildDictionary(LocalizationEntry[] entries)
        {
            var dict = new Dictionary<string, string>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrEmpty(entry.Key))
                {
                    Log.Warning($"[Localization] 条目索引 {i} 的 Key 为空或 null，已跳过。");
                    continue;
                }

                if (dict.ContainsKey(entry.Key))
                {
                    Log.Warning($"[Localization] Key \"{entry.Key}\" 重复，以后出现的值覆盖。");
                }

                dict[entry.Key] = entry.Value;
            }

            return dict;
        }

        /// <summary>按回退链查找：当前语言 → 回退语言（仅当不同于当前语言）。命中回退时记 Warning（首次去重）。</summary>
        private bool TryLookup(string key, out string value)
        {
            // 第一级：当前语言
            if (_loadedPacks.TryGetValue(_currentLanguage, out var currentPack)
                && currentPack.Entries.TryGetValue(key, out value))
            {
                return true;
            }

            // 第二级：回退语言
            if (_currentFallbackLanguage != _currentLanguage
                && _loadedPacks.TryGetValue(_currentFallbackLanguage, out var fallbackPack)
                && fallbackPack.Entries.TryGetValue(key, out value))
            {
                if (_reportedIssues.Add((key, _currentLanguage)))
                {
                    Log.Warning(
                        $"[Localization] Key \"{key}\" 在 \"{_currentLanguage}\" 中缺失，使用回退语言 " +
                        $"\"{_currentFallbackLanguage}\"。");
                }

                return true;
            }

            value = null;
            return false;
        }
    }
}
