namespace XeptKit.Event
{
    /// <summary>
    /// 订阅清空能力。EventBus / AsyncEventBus 实现之，供组合根在会话关闭时清空全部订阅。
    /// 定义于 Event 而非 Core：当前仅 Event 使用，符合 Core「单一模块使用的代码不进 Core」红线。
    /// </summary>
    public interface ISubscriptionClearable
    {
        /// <summary>清空全部订阅。</summary>
        void Clear();
    }
}
