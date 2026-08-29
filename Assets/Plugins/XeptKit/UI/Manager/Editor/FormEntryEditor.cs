using UnityEditor;
using UnityEngine;

namespace XeptKit.UI.Manager
{
    /// <summary>
    /// FormEntry 编辑期校验：Group 必填（错误级）；Prefab 根未挂 FormLogicBase 时提示（信息级，纯视图表单合法）。
    /// 经 SerializedProperty 访问序列化字段，不触碰 internal 成员（无友元程序集决议）。
    /// </summary>
    [CustomEditor(typeof(FormEntry))]
    public class FormEntryEditor : Editor
    {
        private SerializedProperty _prefab;
        private SerializedProperty _group;
        private SerializedProperty _autoCloseSeconds;

        private void OnEnable()
        {
            _prefab = serializedObject.FindProperty("Prefab");
            _group = serializedObject.FindProperty("Group");
            _autoCloseSeconds = serializedObject.FindProperty("AutoCloseSeconds");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_prefab);
            EditorGUILayout.PropertyField(_group);
            EditorGUILayout.PropertyField(_autoCloseSeconds);

            var entry = (FormEntry)target;
            if (entry.Group == null)
            {
                EditorGUILayout.HelpBox("Group 必填：请指定所属 UIGroupConfig。", MessageType.Error);
            }

            if (entry.Prefab != null && entry.Prefab.GetComponent<UIForm>() == null)
            {
                EditorGUILayout.HelpBox(
                    "Prefab 根节点缺少 UIForm 组件（必选）：请手动挂载 UIForm（可在 Inspector 配置转场与相机注入目标）。",
                    MessageType.Error);
            }

            if (entry.Prefab != null && entry.Prefab.GetComponent<FormLogicBase>() == null)
            {
                EditorGUILayout.HelpBox(
                    "Prefab 根节点未挂 FormLogicBase（可选）：将作为纯视图表单，逻辑钩子被跳过。",
                    MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
