using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XeptKit.Core;
using Object = UnityEngine.Object;

namespace XeptKit.UI.Manager
{
    /// <summary>
    /// 表单加载抽象（seam，非双轨）：获取 Prefab 资产并实例化。v1 仅 <see cref="DirectFormLoader"/> 一条路径；
    /// 异步/热更实现（经 Asset 模块的加载抽象）出现需求时换实现即可，架构不需改（届时补 Asset 程序集引用）。
    /// </summary>
    public interface IFormLoader
    {
        /// <summary>实例化表单 Prefab。失败抛异常（fail-fast）；取消抛 OperationCanceledException。</summary>
        UniTask<GameObject> InstantiateAsync(FormEntry entry, CancellationToken cancellationToken = default);
    }

    /// <summary>默认加载实现：Prefab 直接引用、同步实例化（返回已完成 UniTask）。</summary>
    public sealed class DirectFormLoader : IFormLoader
    {
        /// <inheritdoc />
        public UniTask<GameObject> InstantiateAsync(FormEntry entry, CancellationToken cancellationToken = default)
        {
            Guard.NotNullObject(entry, nameof(entry));
            Guard.NotNullObject(entry.Prefab, "entry.Prefab");

            if (cancellationToken.IsCancellationRequested)
            {
                return UniTask.FromCanceled<GameObject>(cancellationToken);
            }

            return UniTask.FromResult(Object.Instantiate(entry.Prefab));
        }
    }
}
