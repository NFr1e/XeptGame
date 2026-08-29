using System;

namespace XeptKit.Scenes
{
    /// <summary>
    /// 场景操作异常。加载/卸载/切换/场景组操作失败时抛出（fail-fast，实现类不记日志，日志归调用方）。
    /// </summary>
    public sealed class SceneOperationException : Exception
    {
        /// <summary>操作涉及的场景引用。</summary>
        public SceneReference SceneRef { get; }

        /// <summary>操作名："Load" / "Unload" / "LoadSceneGroup"。</summary>
        public string Operation { get; }

        public SceneOperationException(
            SceneReference sceneRef,
            string operation,
            string message,
            Exception innerException = null)
            : base($"场景操作失败：{operation} '{sceneRef.SceneName}' — {message}", innerException)
        {
            SceneRef = sceneRef;
            Operation = operation;
        }
    }
}
