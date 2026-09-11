using XeptGame.Container;

namespace XeptGame.Items.Operations
{
    /// <summary>世界源的短期占用标记；不改变物品归属。卸载后不可用，释放仍须可调用。</summary>
    public interface IWorldItemSource : IItemContainer
    {
        /// <summary>宿主仍存在且允许提交；不可用时尚未提交的拾取应失败。</summary>
        bool Available { get; }

        /// <summary>为单次操作标记忙碌，不改变容器占有。</summary>
        bool TryAcquire(long operationId);

        /// <summary>仅释放匹配操作的标记；宿主不可用时也必须可调用。</summary>
        void Release(long operationId);

        /// <summary>同步提交批结束后的场景反馈，不在移除/回滚中间刷新。</summary>
        void RefreshView();
    }
}
