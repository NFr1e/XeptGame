using UnityEditor;
using UnityEngine;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// UISquircleCorners 的自定义 Inspector。支持统一/独立半径、超椭圆平滑度、抗锯齿强度。
    /// </summary>
    [CustomEditor(typeof(UISquircleCorners))]
    [CanEditMultipleObjects]
    public class UISquircleCornersEditor : UnityEditor.Editor
    {
        private SerializedProperty m_RadiusMode = null;
        private SerializedProperty m_Uniform = null;
        private SerializedProperty m_Radius = null;
        private SerializedProperty m_CornerRadii = null;
        private SerializedProperty m_Smoothness = null;
        private SerializedProperty m_AntiAliasing = null;

        private SerializedProperty m_RadiiX = null;
        private SerializedProperty m_RadiiY = null;
        private SerializedProperty m_RadiiZ = null;
        private SerializedProperty m_RadiiW = null;

        private void OnEnable()
        {
            m_RadiusMode = serializedObject.FindProperty("m_RadiusMode");
            m_Uniform = serializedObject.FindProperty("m_Uniform");
            m_Radius = serializedObject.FindProperty("m_Radius");
            m_CornerRadii = serializedObject.FindProperty("m_CornerRadii");
            m_Smoothness = serializedObject.FindProperty("m_Smoothness");
            m_AntiAliasing = serializedObject.FindProperty("m_AntiAliasing");

            m_RadiiX = m_CornerRadii.FindPropertyRelative("x");
            m_RadiiY = m_CornerRadii.FindPropertyRelative("y");
            m_RadiiZ = m_CornerRadii.FindPropertyRelative("z");
            m_RadiiW = m_CornerRadii.FindPropertyRelative("w");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_RadiusMode, new GUIContent("Radius Mode"));
            EditorGUILayout.PropertyField(m_Uniform, new GUIContent("Uniform Radius"));

            EditorGUILayout.Space();

            if (m_Uniform.boolValue)
                DrawUniformRadius();
            else
                DrawPerCornerRadii();

            EditorGUILayout.PropertyField(m_Smoothness, new GUIContent("Smoothness",
                "Superellipse curve smoothness.\n" +
                "0 = standard arc (G1, equivalent to Rounded Corners)\n" +
                "0.6 = Apple squircle style (approx G2)\n" +
                "1 = strongest curve transition"));

            EditorGUILayout.PropertyField(m_AntiAliasing, new GUIContent("Anti-Aliasing",
                "Edge softness in pixels. 0.1 = sharp, 4.0 = very soft."));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawUniformRadius()
        {
            bool isPercentage = m_RadiusMode.enumValueIndex == (int)RadiusMode.Percentage;
            string label = isPercentage ? "Radius (%)" : "Radius";
            string tooltip = isPercentage
                ? "Corner radius as percentage of min(width, height) / 2. 50% = fully rounded."
                : "Corner radius in canvas units (pixels for Screen Space Overlay).";

            EditorGUILayout.PropertyField(m_Radius, new GUIContent(label, tooltip));

            var corners = target as UISquircleCorners;
            if (corners != null && corners.TryGetComponent(out ProceduralImage image))
            {
                Rect rect = image.rectTransform.rect;
                float maxR = Mathf.Min(rect.width * 0.5f, rect.height * 0.5f);

                if (isPercentage)
                {
                    float effectiveR = maxR * Mathf.Clamp(m_Radius.floatValue, 0f, 100f) / 100f;
                    EditorGUILayout.HelpBox(
                        $"Effective radius: {effectiveR:F1} px ({m_Radius.floatValue:F0}% of {maxR:F0} px max)",
                        MessageType.Info);
                }
                else if (m_Radius.floatValue > maxR && maxR > 0f)
                {
                    EditorGUILayout.HelpBox(
                        $"Radius ({m_Radius.floatValue:F0}) exceeds max ({maxR:F0}). Will be clamped.",
                        MessageType.Warning);
                }
            }
        }

        private void DrawPerCornerRadii()
        {
            bool isPercentage = m_RadiusMode.enumValueIndex == (int)RadiusMode.Percentage;
            string label = isPercentage ? "Per-Corner Radius (%)" : "Per-Corner Radius";
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            // 紧凑四角布局：短前缀标签 + 无 label 字段，两列均分宽度——
            // 完整标签（"Top-Left" 等）并排时挤占数值框，右列尤甚。
            DrawCornerRow(m_RadiiX, "TL", "Top-Left", m_RadiiY, "TR", "Top-Right");
            DrawCornerRow(m_RadiiZ, "BL", "Bot-Left", m_RadiiW, "BR", "Bot-Right");

            if (isPercentage)
            {
                EditorGUILayout.HelpBox(
                    "Each value is a percentage of min(width, height) / 2. 50% = fully rounded.",
                    MessageType.Info);
            }
        }

        private static void DrawCornerRow(
            SerializedProperty left, string leftTag, string leftFull,
            SerializedProperty right, string rightTag, string rightFull)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent(leftTag, leftFull), GUILayout.Width(22f));
            EditorGUILayout.PropertyField(left, GUIContent.none);
            GUILayout.Space(8f);
            EditorGUILayout.LabelField(new GUIContent(rightTag, rightFull), GUILayout.Width(22f));
            EditorGUILayout.PropertyField(right, GUIContent.none);
            EditorGUILayout.EndHorizontal();
        }
    }
}
