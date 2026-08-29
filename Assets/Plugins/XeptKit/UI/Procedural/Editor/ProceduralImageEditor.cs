using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// ProceduralImage 的自定义 Inspector。
    /// 提供 SourceType 切换 UI、Sprite/Texture/UV 字段的动态显示。
    /// 效果列表的展示与开关职责集中在 EffectPipeline 的 Inspector（避免重复）。
    /// </summary>
    [CustomEditor(typeof(ProceduralImage), true)]
    [CanEditMultipleObjects]
    public class ProceduralImageEditor : GraphicEditor
    {
        private SerializedProperty m_SourceType = null;
        private SerializedProperty m_Sprite = null;
        private SerializedProperty m_Texture = null;
        private SerializedProperty m_UVRect = null;
        private SerializedProperty m_PreserveAspect = null;

        protected override void OnEnable()
        {
            base.OnEnable();

            m_SourceType = serializedObject.FindProperty("m_SourceType");
            m_Sprite = serializedObject.FindProperty("m_Sprite");
            m_Texture = serializedObject.FindProperty("m_Texture");
            m_UVRect = serializedObject.FindProperty("m_UVRect");
            m_PreserveAspect = serializedObject.FindProperty("m_PreserveAspect");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_SourceType, new GUIContent("Source Type"));
            EditorGUILayout.Space();

            ProceduralImage.SourceType currentType = (ProceduralImage.SourceType)m_SourceType.enumValueIndex;

            switch (currentType)
            {
                case ProceduralImage.SourceType.Sprite:
                    DrawSpriteFields();
                    break;
                case ProceduralImage.SourceType.Texture:
                    DrawTextureFields();
                    break;
            }

            EditorGUILayout.Space();

            // 通用属性
            EditorGUILayout.PropertyField(m_Color);
            EditorGUILayout.PropertyField(m_Material);
            EditorGUILayout.PropertyField(m_RaycastTarget);
            EditorGUILayout.PropertyField(m_Maskable);

            // 原生尺寸按钮
            if (currentType == ProceduralImage.SourceType.Sprite || currentType == ProceduralImage.SourceType.Texture)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Set Native Size"))
                {
                    foreach (var t in targets)
                    {
                        var pi = t as ProceduralImage;
                        if (pi != null)
                        {
                            Undo.RecordObject(pi.rectTransform, "Set Native Size");
                            pi.SetNativeSize();
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                foreach (var t in targets)
                {
                    (t as ProceduralImage)?.SetAllDirty();
                }
            }
        }

        private void DrawSpriteFields()
        {
            EditorGUILayout.PropertyField(m_Sprite, new GUIContent("Sprite"));
            EditorGUILayout.PropertyField(m_PreserveAspect, new GUIContent("Preserve Aspect"));

            ProceduralImage image = target as ProceduralImage;
            if (image != null && image.sprite != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(
                    $"Sprite Size: {image.sprite.rect.width}x{image.sprite.rect.height}\n" +
                    (image.sprite.border.sqrMagnitude > 0
                        ? $"Border: {image.sprite.border}\n⚠ Sliced mode not supported — using Simple."
                        : "No border (Simple sprite)"),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawTextureFields()
        {
            EditorGUILayout.PropertyField(m_Texture, new GUIContent("Texture"));
            EditorGUILayout.PropertyField(m_PreserveAspect, new GUIContent("Preserve Aspect"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UV Rect", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(m_UVRect, GUIContent.none);
            EditorGUI.indentLevel--;

            ProceduralImage image = target as ProceduralImage;
            if (image != null && image.texture != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(
                    $"Texture Size: {image.texture.width}x{image.texture.height}",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }
    }
}
