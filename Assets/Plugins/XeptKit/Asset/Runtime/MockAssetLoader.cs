using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using XeptKit.Core;
using Object = UnityEngine.Object;

namespace XeptKit.Asset
{
    /// <summary>
    /// <see cref="IAssetLoader"/> 的 Mock 实现，用于单元测试解耦。
    /// 资产经 <see cref="Register"/> 预置，加载同步完成（无 IO 延迟），释放为 No-Op（生命周期由测试管理）。
    /// </summary>
    public sealed class MockAssetLoader : IAssetLoader
    {
        private readonly Dictionary<string, Object> _assets = new Dictionary<string, Object>();

        /// <summary>注册 address → asset 映射（测试 Setup 用，非 IAssetLoader 接口成员）。</summary>
        public void Register(string address, Object asset)
        {
            Guard.NotNullOrWhiteSpace(address, nameof(address));
            Guard.NotNullObject(asset, nameof(asset));
            _assets[address] = asset;
        }

        /// <summary>清空所有已注册资产（测试间隔离）。</summary>
        public void Clear() => _assets.Clear();

        public UniTask<AssetHandle<T>> LoadAsync<T>(string address, CancellationToken cancellationToken = default)
            where T : Object
        {
            Guard.NotNullOrWhiteSpace(address, nameof(address));

            if (cancellationToken.IsCancellationRequested)
            {
                return UniTask.FromCanceled<AssetHandle<T>>(cancellationToken);
            }

            if (!_assets.TryGetValue(address, out var asset))
            {
                // 失败以 faulted task 呈现（await 时抛出），与 Addressables 实现语义一致
                return UniTask.FromException<AssetHandle<T>>(new AssetLoadException(address, typeof(T),
                    new KeyNotFoundException($"MockAssetLoader: 地址 '{address}' 未注册。请先调用 Register()。")));
            }

            if (asset is not T typedAsset)
            {
                return UniTask.FromException<AssetHandle<T>>(new AssetLoadException(address, typeof(T),
                    new InvalidCastException($"MockAssetLoader: 地址 '{address}' 注册的类型为 {asset.GetType().Name}，但请求类型为 {typeof(T).Name}。")));
            }

            return UniTask.FromResult(new AssetHandle<T>(typedAsset, () => { }));
        }
    }
}
