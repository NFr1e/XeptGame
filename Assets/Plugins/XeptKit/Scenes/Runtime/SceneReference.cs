using System;
using System.IO;
using UnityEngine;

namespace XeptKit.Scenes
{
    /// <summary>
    /// 类型安全的场景引用，替代 string sceneName / int buildIndex。
    /// 运行时存储场景在 Build Settings 中的路径；Editor 中通过 <see cref="SceneReferenceDrawer"/>
    /// 提供 SceneAsset 拖拽赋值体验。
    /// </summary>
    [Serializable]
    public struct SceneReference : IEquatable<SceneReference>
    {
        [SerializeField]
        private string _scenePath;

        /// <summary>场景在 Build Settings 中的路径（如 "Assets/Scenes/Gameplay.unity"）。未赋值返回空字符串。</summary>
        public readonly string ScenePath => _scenePath ?? string.Empty;

        /// <summary>此引用是否有效（路径非空）。</summary>
        public readonly bool IsValid => !string.IsNullOrEmpty(_scenePath);

        /// <summary>从路径提取的场景名（不含扩展名，如 "Gameplay"）。</summary>
        public string SceneName
        {
            get
            {
                if (string.IsNullOrEmpty(_scenePath)) return string.Empty;
                return Path.GetFileNameWithoutExtension(_scenePath);
            }
        }

        /// <summary>
        /// 创建直接指定路径的引用。优先使用 Inspector 拖拽赋值；
        /// 仅在动态生成场景引用时使用此构造函数。路径以 Editor 拖拽或
        /// AssetDatabase.GetAssetPath 的规范输出为准（Ordinal 比较，大小写敏感）。
        /// </summary>
        public SceneReference(string scenePath)
        {
            _scenePath = scenePath;
        }

        public readonly bool Equals(SceneReference other) =>
            string.Equals(_scenePath, other._scenePath, StringComparison.Ordinal);

        public readonly override bool Equals(object obj) =>
            obj is SceneReference other && Equals(other);

        public readonly override int GetHashCode() =>
            _scenePath != null ? StringComparer.Ordinal.GetHashCode(_scenePath) : 0;

        public static bool operator ==(SceneReference left, SceneReference right) => left.Equals(right);

        public static bool operator !=(SceneReference left, SceneReference right) => !left.Equals(right);

        public override string ToString() =>
            string.IsNullOrEmpty(_scenePath) ? "SceneReference(empty)" : $"SceneReference({SceneName})";
    }
}
