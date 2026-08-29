using System;
using XeptKit.Core;
using Object = UnityEngine.Object;

namespace XeptKit.Asset
{
    /// <summary>
    /// 资产加载句柄，持有加载结果与释放回调。
    /// 每个 <see cref="IAssetLoader.LoadAsync{T}"/> 调用返回独立句柄，确保 Per-Load 引用计数精确。
    /// </summary>
    /// <typeparam name="T">资产类型</typeparam>
    public sealed class AssetHandle<T> : IDisposable where T : Object
    {
        private readonly T _result;
        private Action _releaseAction;
        private bool _disposed;

        /// <summary>
        /// 加载完成的资产实例。释放后仍可访问（引用不置 null），
        /// 但底层资产可能已被卸载——生命周期由调用方自行保证。
        /// </summary>
        public T Result => _result;

        /// <summary>
        /// 仅由加载实现类构造（Editor 实现经友元程序集访问，见 §1）。
        /// </summary>
        internal AssetHandle(T result, Action releaseAction)
        {
            Guard.NotNullObject(result, nameof(result));
            _result = result;
            _releaseAction = releaseAction;
        }

        /// <summary>
        /// 释放本句柄持有的资产引用。幂等：重复调用无副作用。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            var release = _releaseAction;
            _releaseAction = null;
            release?.Invoke();
        }
    }
}
