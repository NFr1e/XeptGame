using UnityEditor;
using UnityEngine;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// UIClipping 的自定义 Inspector。根据裁剪模式动态显示对应字段。
    /// </summary>
    [CustomEditor(typeof(UIClipping))]
    [CanEditMultipleObjects]
    public class UIClippingEditor : UnityEditor.Editor
    {
        private SerializedProperty m_ClipMode = null;
        private SerializedProperty m_AlignmentX = null;
        private SerializedProperty m_AlignmentY = null;
        private SerializedProperty m_Scale = null;
        private SerializedProperty m_Offset = null;

        private void OnEnable()
        {
            m_ClipMode = serializedObject.FindProperty("m_ClipMode");
            m_AlignmentX = serializedObject.FindProperty("m_AlignmentX");
            m_AlignmentY = serializedObject.FindProperty("m_AlignmentY");
            m_Scale = serializedObject.FindProperty("m_Scale");
            m_Offset = serializedObject.FindProperty("m_Offset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_ClipMode, new GUIContent("Clip Mode"));

            EditorGUILayout.Space();

            ClipMode mode = (ClipMode)m_ClipMode.enumValueIndex;

            switch (mode)
            {
                case ClipMode.None:
                    EditorGUILayout.HelpBox(
                        "No clipping. Source fills the container (may stretch if aspect ratios differ).",
                        MessageType.None);
                    break;

                case ClipMode.AspectFill:
                    DrawAspectFillFields();
                    break;

                case ClipMode.Custom:
                    DrawCustomFields();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAspectFillFields()
        {
            EditorGUILayout.LabelField("Alignment", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(m_AlignmentX, new GUIContent("Horizontal",
                "0 = left edge visible, 0.5 = center, 1 = right edge visible"));
            EditorGUILayout.PropertyField(m_AlignmentY, new GUIContent("Vertical",
                "0 = bottom edge visible, 0.5 = center, 1 = top edge visible"));

            EditorGUILayout.Space();

            var clip = target as UIClipping;
            if (clip != null && clip.TryGetComponent(out ProceduralImage img))
            {
                float srcW = 0f, srcH = 0f;
                if (img.sourceType == ProceduralImage.SourceType.Sprite && img.sprite != null)
                {
                    srcW = img.sprite.rect.width;
                    srcH = img.sprite.rect.height;
                }
                else if (img.sourceType == ProceduralImage.SourceType.Texture && img.texture != null)
                {
                    srcW = img.texture.width * img.uvRect.width;
                    srcH = img.texture.height * img.uvRect.height;
                }

                if (srcW > 0f && srcH > 0f)
                {
                    Rect rect = img.rectTransform.rect;
                    float srcAR = srcW / srcH;
                    float conAR = rect.width / rect.height;
                    string note = srcAR > conAR
                        ? $"Source AR {srcAR:F2} > Container AR {conAR:F2} → horizontal crop"
                        : $"Source AR {srcAR:F2} < Container AR {conAR:F2} → vertical crop";

                    EditorGUILayout.HelpBox(
                        $"Source: {srcW:F0}×{srcH:F0}  |  Container: {rect.width:F0}×{rect.height:F0}\n{note}",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Color mode — no source aspect ratio, AspectFill unavailable.\n" +
                        "Switch to Custom mode for manual UV control.",
                        MessageType.Warning);
                }
            }
        }

        private void DrawCustomFields()
        {
            EditorGUILayout.LabelField("UV Transform", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(m_Scale, new GUIContent("Scale",
                "UV scale multiplier. (1, 1) = full source visible. Values < 1 zoom in."));
            EditorGUILayout.PropertyField(m_Offset, new GUIContent("Offset",
                "UV offset in normalized UV space (0..1)."));
        }
    }
}
