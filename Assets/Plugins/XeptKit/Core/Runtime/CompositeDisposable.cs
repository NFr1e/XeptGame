using System;
using System.Collections.Generic;

namespace XeptKit.Core
{
    /// <summary>
    /// 组合释放器：收集多个 <see cref="IDisposable"/> 句柄，一次性统一释放。
    /// 用于"创建期批量收集、销毁期统一释放"的场景（如 Event 订阅句柄的组合释放）。
    /// Dispose 幂等；已 Dispose 后再 Add 会立即释放传入的句柄，防止泄漏。
    /// 仅主线程使用，不做并发设计。
    /// </summary>
    public sealed class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        private bool _disposed;

        /// <summary>
        /// 收集一个句柄。若已 <see cref="Dispose"/>，则立即释放该句柄（防止静默泄漏），
        /// 而非加入集合。
        /// </summary>
        public void Add(IDisposable disposable)
        {
            Guard.NotNull(disposable, nameof(disposable));

            if (_disposed)
            {
                disposable.Dispose();
                return;
            }

            _disposables.Add(disposable);
        }

        /// <summary>
        /// 释放全部已收集句柄；幂等（重复调用无副作用）。
        /// 释放顺序为 Add 的逆序（LIFO，后收集的先释放）。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            for (int i = _disposables.Count - 1; i >= 0; i--)
            {
                _disposables[i].Dispose();
            }

            // 清空以释放对句柄（及其持有的 handler/owner）的引用，便于 GC
            _disposables.Clear();
        }
    }
}
