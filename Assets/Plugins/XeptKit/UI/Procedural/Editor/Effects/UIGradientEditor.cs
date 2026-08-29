using UnityEditor;
using UnityEngine;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// UIGradient 的自定义 Inspector。根据渐变类型动态显示对应字段。
    /// </summary>
    [CustomEditor(typeof(UIGradient))]
    [CanEditMultipleObjects]
    public class UIGradientEditor : UnityEditor.Editor
    {
        private SerializedProperty m_GradientType = null;
        private SerializedProperty m_Color1 = null;
        private SerializedProperty m_Color2 = null;
        private SerializedProperty m_CornerTopLeft = null;
        private SerializedProperty m_CornerTopRight = null;
        private SerializedProperty m_CornerBottomLeft = null;
        private SerializedProperty m_CornerBottomRight = null;
        private SerializedProperty m_Rotation = null;
        private SerializedProperty m_Center = null;

        private void OnEnable()
        {
            m_GradientType = serializedObject.FindProperty("m_GradientType");
            m_Color1 = serializedObject.FindProperty("m_Color1");
            m_Color2 = serializedObject.FindProperty("m_Color2");
            m_CornerTopLeft = serializedObject.FindProperty("m_CornerTopLeft");
            m_CornerTopRight = serializedObject.FindProperty("m_CornerTopRight");
            m_CornerBottomLeft = serializedObject.FindProperty("m_CornerBottomLeft");
            m_CornerBottomRight = serializedObject.FindProperty("m_CornerBottomRight");
            m_Rotation = serializedObject.FindProperty("m_Rotation");
            m_Center = serializedObject.FindProperty("m_Center");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_GradientType);

            EditorGUILayout.Space();

            GradientType type = (GradientType)m_GradientType.enumValueIndex;

            switch (type)
            {
                case GradientType.Linear:
                    DrawLinearFields();
                    break;
                case GradientType.Radial:
                    DrawRadialFields();
                    break;
                case GradientType.Angle:
                    DrawAngleFields();
                    break;
                case GradientType.FourCorner:
                    DrawFourCornerFields();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawLinearFields()
        {
            EditorGUILayout.PropertyField(m_Color1, new GUIContent("Color 1"));
            EditorGUILayout.PropertyField(m_Color2, new GUIContent("Color 2"));
            EditorGUILayout.PropertyField(m_Rotation, new GUIContent("Rotation",
                "Gradient direction angle in degrees (0 = left-to-right)"));
        }

        private void DrawRadialFields()
        {
            EditorGUILayout.PropertyField(m_Color1, new GUIContent("Inner Color"));
            EditorGUILayout.PropertyField(m_Color2, new GUIContent("Outer Color"));
            EditorGUILayout.PropertyField(m_Center, new GUIContent("Center",
                "Normalized center point (0..1)"));
        }

        private void DrawAngleFields()
        {
            EditorGUILayout.PropertyField(m_Color1, new GUIContent("Start Color"));
            EditorGUILayout.PropertyField(m_Color2, new GUIContent("End Color"));
            EditorGUILayout.PropertyField(m_Center, new GUIContent("Center",
                "Normalized pivot point for angle calculation (0..1)"));
        }

        private void DrawFourCornerFields()
        {
            EditorGUILayout.LabelField("Corner Colors", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Top", GUILayout.Width(48f));
            EditorGUILayout.PropertyField(m_CornerTopLeft, GUIContent.none);
            GUILayout.Space(4f);
            EditorGUILayout.PropertyField(m_CornerTopRight, GUIContent.none);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Bottom", GUILayout.Width(48f));
            EditorGUILayout.PropertyField(m_CornerBottomLeft, GUIContent.none);
            GUILayout.Space(4f);
            EditorGUILayout.PropertyField(m_CornerBottomRight, GUIContent.none);
            EditorGUILayout.EndHorizontal();
        }
    }
}
