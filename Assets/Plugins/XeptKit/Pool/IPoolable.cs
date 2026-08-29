namespace XeptKit.Pool
{
    /// <summary>
    /// 对象池生命周期契约。实现此接口的对象在从池中取出/归还时自动获得状态重置回调。
    /// 适用于 <see cref="GameObjectPool{T}"/>（GameObject 池）与 <see cref="ReferencePool{T}"/>（C# 引用池）。
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 从池中取出（已激活）时调用。在此方法中初始化/激活状态。
        /// </summary>
        void OnSpawn();

        /// <summary>
        /// 归还池中（仍激活）时调用。在此方法中重置/清理状态。
        /// </summary>
        void OnDespawn();
    }
}
