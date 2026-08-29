using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using XeptKit.Core;
using Object = UnityEngine.Object;

namespace XeptKit.Asset
{
    /// <summary>
    /// <see cref="IAssetLoader"/> 的 Editor 实现，基于 AssetDatabase 免 Build 加载，加速编辑器迭代。
    /// 释放为 No-Op（AssetDatabase 加载的资产由 Unity Editor 管理生命周期）。
    /// 地址解析以 Addressables address 为规范；GUID / "Assets/" 路径仅作 Editor 便捷回退（不可移植到运行时）。
    /// </summary>
    public sealed class EditorAssetLoader : IAssetLoader
    {
        public UniTask<AssetHandle<T>> LoadAsync<T>(string address, CancellationToken cancellationToken = default)
            where T : Object
        {
            Guard.NotNullOrWhiteSpace(address, nameof(address));

            if (cancellationToken.IsCancellationRequested)
            {
                return UniTask.FromCanceled<AssetHandle<T>>(cancellationToken);
            }

            try
            {
                var asset = LoadAsset<T>(address);
                return UniTask.FromResult(new AssetHandle<T>(asset, () => { }));
            }
            catch (Exception ex)
            {
                return UniTask.FromException<AssetHandle<T>>(ex);
            }
        }

        private static T LoadAsset<T>(string address) where T : Object
        {
            // 1. Addressables address（规范地址）：经 AddressableAssetSettings 查找
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                var entry = FindEntryByAddress(settings, address);
                if (entry != null)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<T>(entry.AssetPath);
                    if (asset != null)
                    {
                        return asset;
                    }

                    // 地址已命中条目，但无法作为 T 加载（类型不符或资产无效）——明确报错，不回退
                    throw new AssetLoadException(address, typeof(T),
                        new InvalidCastException(
                            $"EditorAssetLoader: 地址 '{address}' 已匹配到条目，但无法作为 {typeof(T).Name} 加载（路径 {entry.AssetPath}，可能类型不符）。"));
                }
            }

            // 2. GUID 回退（Editor-only 便捷）
            if (Guid.TryParse(address, out _))
            {
                var path = AssetDatabase.GUIDToAssetPath(address);
                if (!string.IsNullOrEmpty(path))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                    if (asset != null)
                    {
                        return asset;
                    }
                }
            }

            // 3. "Assets/" 路径回退（Editor-only 便捷）
            if (address.StartsWith("Assets/", StringComparison.Ordinal))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(address);
                if (asset != null)
                {
                    return asset;
                }

                throw new AssetLoadException(address, typeof(T),
                    new InvalidOperationException($"EditorAssetLoader: 路径 '{address}' 未找到类型为 {typeof(T).Name} 的资产。"));
            }

            throw new AssetLoadException(address, typeof(T),
                new InvalidOperationException($"EditorAssetLoader: 未找到地址 '{address}' 对应的 {typeof(T).Name} 资产。"));
        }

        private static AddressableAssetEntry FindEntryByAddress(AddressableAssetSettings settings, string address)
        {
            foreach (var group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (var entry in group.entries)
                {
                    if (entry.address == address)
                    {
                        return entry;
                    }
                }
            }

            return null;
        }
    }
}
