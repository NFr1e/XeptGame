namespace XeptKit.Scenes
{
    /// <summary>
    /// 由 <see cref="IScenesManager"/> 经构造注入的 IEventBus 同步广播的场景生命周期事件。
    /// 事件类型定义于本模块（领域事件）。
    /// </summary>

    /// <summary>场景开始加载时广播。携带进度信息，可用于驱动 Loading UI。</summary>
    public readonly struct SceneLoadingEvent
    {
        /// <summary>正在加载的场景句柄。</summary>
        public SceneHandle Handle { get; }

        /// <summary>当前加载进度，范围 [0, 1]。</summary>
        public float Progress { get; }

        public SceneLoadingEvent(SceneHandle handle, float progress)
        {
            Handle = handle;
            Progress = progress;
        }
    }

    /// <summary>场景加载并激活完成时广播。</summary>
    public readonly struct SceneLoadedEvent
    {
        /// <summary>已加载完成的场景句柄。</summary>
        public SceneHandle Handle { get; }

        public SceneLoadedEvent(SceneHandle handle)
        {
            Handle = handle;
        }
    }

    /// <summary>场景开始卸载时广播。</summary>
    public readonly struct SceneUnloadingEvent
    {
        /// <summary>即将卸载的场景句柄。</summary>
        public SceneHandle Handle { get; }

        public SceneUnloadingEvent(SceneHandle handle)
        {
            Handle = handle;
        }
    }

    /// <summary>场景卸载完成时广播。此时 <see cref="SceneHandle"/> 已失效，仅携带 <see cref="SceneReference"/> 供识别。</summary>
    public readonly struct SceneUnloadedEvent
    {
        /// <summary>已卸载的场景引用。</summary>
        public SceneReference SceneRef { get; }

        public SceneUnloadedEvent(SceneReference sceneRef)
        {
            SceneRef = sceneRef;
        }
    }

    /// <summary>场景加载失败时广播（信息性通知，供非发起方感知；调用方以异常为准）。</summary>
    public readonly struct SceneLoadFailedEvent
    {
        /// <summary>加载失败的场景引用。</summary>
        public SceneReference SceneRef { get; }

        /// <summary>失败原因。</summary>
        public string ErrorMessage { get; }

        public SceneLoadFailedEvent(SceneReference sceneRef, string errorMessage)
        {
            SceneRef = sceneRef;
            ErrorMessage = errorMessage;
        }
    }
}
