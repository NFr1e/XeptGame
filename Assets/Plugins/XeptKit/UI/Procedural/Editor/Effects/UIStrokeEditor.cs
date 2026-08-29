using UnityEditor;
using UnityEngine;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// UIStroke 的自定义 Inspector。提供描边位置/宽度/颜色与行为说明。
    /// </summary>
    [CustomEditor(typeof(UIStroke))]
    [CanEditMultipleObjects]
    public class UIStrokeEditor : UnityEditor.Editor
    {
        private SerializedProperty m_Position = null;
        private SerializedProperty m_Width = null;
        private SerializedProperty m_Color = null;
        private SerializedProperty m_UseGraphicAlpha = null;
        private SerializedProperty m_AntiAliasing = null;

        private void OnEnable()
        {
            m_Position = serializedObject.FindProperty("m_Position");
            m_Width = serializedObject.FindProperty("m_Width");
            m_Color = serializedObject.FindProperty("m_Color");
            m_UseGraphicAlpha = serializedObject.FindProperty("m_UseGraphicAlpha");
            m_AntiAliasing = serializedObject.FindProperty("m_AntiAliasing");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_Position, new GUIContent("Position"));
            EditorGUILayout.PropertyField(m_Width, new GUIContent("Width"));
            EditorGUILayout.PropertyField(m_Color, new GUIContent("Color"));
            EditorGUILayout.PropertyField(m_UseGraphicAlpha, new GUIContent("Use Graphic Alpha",
                "When enabled, stroke alpha follows the host's alpha and CanvasGroup alpha."));

            EditorGUILayout.PropertyField(m_AntiAliasing, new GUIContent("Anti-Aliasing",
                "Edge softness in pixels. 0.1 = sharp, 4.0 = very soft."));

            EditorGUILayout.Space();

            var stroke = target as UIStroke;
            if (stroke != null && stroke.width > 0f)
            {
                string desc;
                switch (stroke.position)
                {
                    case StrokePosition.Inside:
                        desc = "Stroke stays inside the element bounds (fill shrinks).";
                        break;
                    case StrokePosition.Center:
                        desc = "Stroke straddles the element edge.";
                        break;
                    default:
                        desc = "Stroke is drawn outside the element bounds.";
                        break;
                }
                EditorGUILayout.HelpBox(desc, MessageType.None);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
