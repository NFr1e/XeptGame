using System;

namespace XeptKit.Scenes
{
    /// <summary>
    /// 场景加载选项（只读 struct——栈分配，零 GC）。
    /// </summary>
    /// <remarks>
    /// 激活语义经反转存储实现（Scenes.design.md §3「default 语义对齐」）：
    /// readonly struct 的 <c>default</c> / <c>new()</c> 不会调用带可选参数的构造函数，
    /// 若 <see cref="ActivateOnLoad"/> 直接存 bool，零值将为 false，使参数默认 <c>default</c>
    /// 的常规加载（<see cref="IScenesManager.LoadSceneAsync"/>）永久驻留 <see cref="SceneState.Ready"/>
    /// 且无句柄可外部激活（挂死）。改为存储取反后的 <c>_deferActivation</c> 后，零值默认即
    /// "立即激活"——<c>default</c> 与 <see cref="Default"/> 语义真正一致，
    /// 显式 <c>new SceneLoadOptions(activateOnLoad: false)</c> 仍可表达延迟激活。
    /// </remarks>
    public readonly struct SceneLoadOptions : IEquatable<SceneLoadOptions>
    {
        /// <summary>存储取反：零值（default / new()）即"立即激活"（<see cref="ActivateOnLoad"/> 为 true）。</summary>
        private readonly bool _deferActivation;
        private readonly bool _isMainScene;
        private readonly bool _persistent;
        private readonly int _priority;

        /// <summary>加载完成后是否立即激活场景。默认 true。</summary>
        public bool ActivateOnLoad => !_deferActivation;
        /// <summary>是否将本场景设为主场景。默认 false。</summary>
        public bool IsMainScene => _isMainScene;
        /// <summary>是否为持久场景（不会被 <see cref="IScenesManager.SwitchMainSceneAsync"/> 自动卸载）。默认 false。</summary>
        public bool Persistent => _persistent;
        /// <summary>队列优先级，越大越优先加载。默认 0。</summary>
        public int Priority => _priority;

        /// <summary>
        /// 默认选项：立即激活，非主场景，非持久，优先级 0。
        /// 零值（new()，即 default）即正确默认——激活语义由反转存储保证，见类型备注。
        /// </summary>
        public static readonly SceneLoadOptions Default = new();

        /// <summary>
        /// 创建加载选项。
        /// </summary>
        /// <param name="activateOnLoad">加载后立即激活（默认 true）。</param>
        /// <param name="isMainScene">是否为主场景（默认 false）。</param>
        /// <param name="persistent">是否为持久场景（默认 false）。</param>
        /// <param name="priority">队列优先级（默认 0，越大越优先）。</param>
        public SceneLoadOptions(
            bool activateOnLoad = true,
            bool isMainScene = false,
            bool persistent = false,
            int priority = 0)
        {
            _deferActivation = !activateOnLoad;
            _isMainScene = isMainScene;
            _persistent = persistent;
            _priority = priority;
        }

        public bool Equals(SceneLoadOptions other) =>
            ActivateOnLoad == other.ActivateOnLoad &&
            IsMainScene == other.IsMainScene &&
            Persistent == other.Persistent &&
            Priority == other.Priority;

        public override bool Equals(object obj) =>
            obj is SceneLoadOptions other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(ActivateOnLoad, IsMainScene, Persistent, Priority);

        public static bool operator ==(SceneLoadOptions left, SceneLoadOptions right) => left.Equals(right);

        public static bool operator !=(SceneLoadOptions left, SceneLoadOptions right) => !left.Equals(right);
    }
}
