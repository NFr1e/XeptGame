using Cysharp.Threading.Tasks;

namespace XeptKit.Core
{
    /// <summary>UniTask 异步释放约定。</summary>
    public interface IDisposableAsync
    {
        UniTask DisposeAsync();
    }
}
