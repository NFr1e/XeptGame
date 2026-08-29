namespace XeptKit.Scenes
{
    /// <summary>
    /// 场景生命周期状态。
    /// </summary>
    public enum SceneState
    {
        /// <summary>请求已入队，等待调度</summary>
        Pending,

        /// <summary>场景正在加载中</summary>
        Loading,

        /// <summary>场景加载至 90%，等待激活（ActivateOnLoad = false 时驻留）</summary>
        Ready,

        /// <summary>场景正在最终激活</summary>
        Activating,

        /// <summary>场景已加载并激活</summary>
        Active,

        /// <summary>场景正在卸载中</summary>
        Unloading,

        /// <summary>场景已卸载（终态）</summary>
        Unloaded,

        /// <summary>加载/卸载失败（终态；加载失败句柄随即从追踪移除，卸载失败句柄保留）</summary>
        Failed
    }
}
