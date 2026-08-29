using UnityEditor;
using UnityEngine;

namespace XeptKit.Scenes
{
    /// <summary>
    /// <see cref="SceneGroup"/> 的 Inspector 校验器。
    /// 确保组内有且仅有一个 IsMainScene 为 true 的条目（编辑期强制，运行时宽容）。
    /// </summary>
    [CustomEditor(typeof(SceneGroup))]
    public sealed class SceneGroupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var group = (SceneGroup)target;

            DrawDefaultInspector();

            if (group.Entries == null || group.Entries.Length == 0)
            {
                EditorGUILayout.HelpBox("SceneGroup 中没有条目。请添加至少一个场景引用。", MessageType.Warning);
                return;
            }

            int mainSceneCount = 0;
            int validEntryCount = 0;

            for (int i = 0; i < group.Entries.Length; i++)
            {
                var entry = group.Entries[i];

                if (!entry.SceneRef.IsValid)
                {
                    EditorGUILayout.HelpBox(
                        $"Entry [{i}] 的场景引用未赋值。",
                        MessageType.Warning);
                }
                else
                {
                    validEntryCount++;
                }

                if (entry.IsMainScene) mainSceneCount++;
            }

            if (validEntryCount == 0)
            {
                EditorGUILayout.HelpBox("SceneGroup 中没有有效的场景引用。", MessageType.Error);
            }

            if (mainSceneCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "需要且仅需一个条目标记为 IsMainScene。当前：0。",
                    MessageType.Error);
            }
            else if (mainSceneCount > 1)
            {
                EditorGUILayout.HelpBox(
                    $"需要且仅需一个条目标记为 IsMainScene。当前：{mainSceneCount}。",
                    MessageType.Error);
            }
        }
    }
}
