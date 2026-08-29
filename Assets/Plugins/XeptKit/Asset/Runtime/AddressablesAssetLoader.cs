using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using XeptKit.Core;
using Object = UnityEngine.Object;

namespace XeptKit.Asset
{
    /// <summary>
    /// <see cref="IAssetLoader"/> 的运行时默认实现，底层基于 Addressables。
    /// 句柄释放对应底层 AsyncOperationHandle 的释放（Per-Load 引用计数），
    /// 释放一律以 <c>handle.IsValid()</c> 守卫，避免二次释放（见详细设计 §5）。
    /// </summary>
    public sealed class AddressablesAssetLoader : IAssetLoader
    {
        public async UniTask<AssetHandle<T>> LoadAsync<T>(string address, CancellationToken cancellationToken = default) where T : Object
        {
            Guard.NotNullOrWhiteSpace(address, nameof(address));

            // 已取消则不发起底层加载（LoadAssetAsync 会真实创建加载操作）
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            var handle = Addressables.LoadAssetAsync<T>(address);

            T result;
            try
            {
                result = await handle.ToUniTask(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                throw;
            }
            catch (Exception ex)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                throw new AssetLoadException(address, typeof(T), ex);
            }

            // 加载成功但结果为 null（资产被移除等边界）——视为失败
            if (result == null)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                throw new AssetLoadException(address, typeof(T), null);
            }

            return new AssetHandle<T>(result, () =>
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            });
        }
    }
}
