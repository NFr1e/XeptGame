using System.Threading;
using Cysharp.Threading.Tasks;

namespace XeptKit.Localization
{
    /// <summary>
    /// 本地化管理器——运行时文本本地化的统一入口：语言包的异步加载与缓存、同步查表（两级回退）、
    /// 运行时语言切换（经构造注入的事件总线广播 <see cref="LanguageChangedEvent"/>）。
    /// </summary>
    /// <remarks>
    /// 查表为同步操作：语言包加载完成后全部条目驻留内存字典，无每查一次异步 I/O。
    /// 回退链（两级）：目标语言字典 → 回退语言字典（由语言包 <see cref="LocalizationData.FallbackLanguageCode"/> 指定）
    /// → "??key??" 占位符（双缺失时记 Error 日志，按 (key, languageCode) 首次去重）。
    /// 全部操作仅主线程使用、无锁。
    /// </remarks>
    public interface ILocalizationManager
    {
        /// <summary>
        /// 当前语言代码（如 "zh-CN"、"en"）。首次调用 <see cref="SetLanguageAsync"/> 之前为空串。
        /// </summary>
        string CurrentLanguage { get; }

        /// <summary>
        /// 同步查表，返回 <paramref name="key"/> 对应的当前语言文本。
        /// key 缺失时按回退链：当前语言 → 回退语言 → "??key??" 占位符（双缺失记 Error 日志，按 key 首次去重）。
        /// </summary>
        /// <exception cref="System.ArgumentException">key 为 null 或空字符串。</exception>
        /// <exception cref="System.InvalidOperationException">尚未调用 <see cref="SetLanguageAsync"/> 初始化语言。</exception>
        string Get(string key);

        /// <summary>
        /// 查表并按 <c>string.Format</c> 填充占位符（{0}、{1} 等）。
        /// 格式化失败（FormatException）→ 记 Error 日志（按 key 首次去重）→ 返回原始 format 串（容错，不抛）。
        /// </summary>
        string Get(string key, params object[] args);

        /// <summary>
        /// 尝试查表：当前语言或回退语言命中返回 true 并输出值；均未命中返回 false（value 为 null）。
        /// 不记日志、不抛异常（参数校验除外）——适用于"key 是否存在是正常分支逻辑"的场景。
        /// </summary>
        /// <exception cref="System.ArgumentException">key 为 null 或空字符串。</exception>
        bool TryGet(string key, out string value);

        /// <summary>
        /// 异步切换语言：加载目标语言包（未缓存）与回退语言包（需要时），全部就绪后原子提交当前语言并广播
        /// <see cref="LanguageChangedEvent"/>。任一步失败/取消 → 状态不变、不广播、异常上抛（取消抛
        /// <see cref="System.OperationCanceledException"/>，为正常流程不记日志）。
        /// 切换进行中重复请求同一目标幂等返回；请求不同目标抛 <see cref="System.InvalidOperationException"/>（fail-fast）。
        /// </summary>
        UniTask SetLanguageAsync(string languageCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// 清空全部状态（语言包缓存、当前语言、切换标志、查表上报去重集），实例仍可继续使用（幂等）。
        /// 生命周期清理入口：供组合根优雅关闭调用，业务通常不直接调用。
        /// </summary>
        void Clear();
    }
}
