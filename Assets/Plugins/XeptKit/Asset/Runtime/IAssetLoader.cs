using System.Threading;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace XeptKit.Asset
{
    /// <summary>
    /// 统一资产加载入口，隔离底层加载实现。
    /// 接口层仅依赖基元类型（string address）与框架基础类型，不泄漏任何底层实现类型（Addressables 等）。
    /// </summary>
    /// <remarks>
    /// 每个 <see cref="LoadAsync{T}"/> 调用返回独立的 <see cref="AssetHandle{T}"/>，
    /// 调用方通过 <c>handle.Dispose()</c> 释放引用，实现精确的 Per-Load 引用计数。
    /// </remarks>
    public interface IAssetLoader
    {
        /// <summary>
        /// 按地址异步加载单个资产。
        /// </summary>
        /// <typeparam name="T">资产类型（UnityEngine.Object）</typeparam>
        /// <param name="address">资产地址（Addressables address，唯一规范地址）</param>
        /// <param name="cancellationToken">取消令牌，调用方显式传入 KitLifecycle.GlobalToken 或业务令牌</param>
        /// <returns>持有资产的句柄，Dispose 时释放引用</returns>
        UniTask<AssetHandle<T>> LoadAsync<T>(string address, CancellationToken cancellationToken = default)
            where T : Object;
    }
}
