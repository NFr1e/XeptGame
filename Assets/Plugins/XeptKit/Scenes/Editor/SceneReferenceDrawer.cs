using UnityEditor;
using UnityEngine;

namespace XeptKit.Scenes
{
    /// <summary>
    /// <see cref="SceneReference"/> 的 Inspector PropertyDrawer。
    /// 将内部存储的路径字符串表现为 SceneAsset 拖拽框，提供类型安全的场景选择体验；
    /// 路径指向的资产已缺失时在 label 上提示。
    /// </summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    public sealed class SceneReferenceDrawer : PropertyDrawer
    {
        private const string ScenePathPropertyName = "_scenePath";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty pathProp = property.FindPropertyRelative(ScenePathPropertyName) ??
                throw new System.InvalidOperationException(
                    $"[SceneReferenceDrawer] Could not find field '{ScenePathPropertyName}' on SceneReference.");

            string currentPath = pathProp.stringValue;

            // 将当前路径转为 SceneAsset（仅用于显示）
            SceneAsset currentScene = null;
            if (!string.IsNullOrEmpty(currentPath))
            {
                currentScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(currentPath);
            }

            var content = new GUIContent(label);
            if (!string.IsNullOrEmpty(currentPath) && currentScene == null)
            {
                // 路径非空但资产缺失（拖拽后资产被删除等）——提示
                content.text = label.text + " (场景资产缺失)";
                content.tooltip = $"场景资产缺失：{currentPath}";
            }

            EditorGUI.BeginChangeCheck();

            SceneAsset newScene = (SceneAsset)EditorGUI.ObjectField(
                position,
                content,
                currentScene,
                typeof(SceneAsset),
                allowSceneObjects: false);

            if (EditorGUI.EndChangeCheck())
            {
                string newPath = newScene != null
                    ? AssetDatabase.GetAssetPath(newScene)
                    : string.Empty;

                pathProp.stringValue = newPath;
                pathProp.serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
