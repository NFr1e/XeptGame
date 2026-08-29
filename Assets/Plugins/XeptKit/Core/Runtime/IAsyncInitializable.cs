using System.Threading;
using Cysharp.Threading.Tasks;

namespace XeptKit.Core
{
    /// <summary>
    /// UniTask 异步初始化约定。
    /// 取消令牌统一使用 System.Threading.CancellationToken（BCL）；
    /// 调用方应显式传入 <see cref="KitLifecycle.GlobalToken"/>。
    /// </summary>
    public interface IAsyncInitializable
    {
        UniTask InitializeAsync(CancellationToken cancellationToken = default);
    }
}
