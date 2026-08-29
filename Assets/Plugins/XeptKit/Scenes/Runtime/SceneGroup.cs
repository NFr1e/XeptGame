using System;
using UnityEngine;

namespace XeptKit.Scenes
{
    /// <summary>
    /// ScriptableObject 资产：定义一组场景，作为整体加载单元。
    /// 适用于关卡由多个子场景构成（如 Main + Lighting + Audio）的场景分组加载。
    /// 创建：Project 窗口右键 → Create → XeptKit → Scene Group
    /// </summary>
    [CreateAssetMenu(menuName = "XeptKit/Scene Group", fileName = "Scene Group")]
    public sealed class SceneGroup : ScriptableObject
    {
        /// <summary>组内场景条目列表。需且仅需一个条目标记为 IsMainScene（编辑期由 SceneGroupEditor 校验，运行时宽容）。</summary>
        [Tooltip("组内场景条目。需且仅需一个条目标记为 IsMainScene。")]
        public SceneGroupEntry[] Entries = Array.Empty<SceneGroupEntry>();
    }

    /// <summary>
    /// <see cref="SceneGroup"/> 中的单个场景条目。
    /// </summary>
    [Serializable]
    public struct SceneGroupEntry
    {
        /// <summary>场景引用。</summary>
        [Tooltip("场景引用。")]
        public SceneReference SceneRef;

        /// <summary>是否作为组内主场景。一个 SceneGroup 中需且仅需一个（编辑期校验，运行时宽容）。</summary>
        [Tooltip("是否作为组内主场景。每个 SceneGroup 需且仅需一个。")]
        public bool IsMainScene;

        /// <summary>是否为持久场景。持久场景在 SwitchMainSceneAsync 时不会被自动卸载。</summary>
        [Tooltip("是否为持久场景（不会被 SwitchMainSceneAsync 自动卸载）。")]
        public bool Persistent;
    }
}
