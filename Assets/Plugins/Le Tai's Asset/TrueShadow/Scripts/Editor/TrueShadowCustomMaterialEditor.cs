// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using UnityEditor;

namespace LeTai.TrueShadow.Editor
{
[CustomEditor(typeof(TrueShadowCustomMaterial))]
public class TrueShadowCustomMaterialEditor : UnityEditor.Editor
{
    TrueShadowCustomMaterial _trueShadowCustomMaterial;
    MaterialEditor           _materialEditor;

    void OnEnable()
    {
        _trueShadowCustomMaterial = (TrueShadowCustomMaterial)target;

        if (_trueShadowCustomMaterial.material != null)
        {
            _materialEditor = (MaterialEditor)CreateEditor(_trueShadowCustomMaterial.material);
        }
    }

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("material"));

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();

            if (_materialEditor)
            {
                DestroyImmediate(_materialEditor);
            }

            if (_trueShadowCustomMaterial.material)
            {
                _materialEditor = (MaterialEditor)CreateEditor(_trueShadowCustomMaterial.material);
            }
        }


        if (_materialEditor)
        {
            _materialEditor.DrawHeader();

            bool isDefaultMaterial = !AssetDatabase.GetAssetPath(_trueShadowCustomMaterial.material).StartsWith("Assets");
            using (new EditorGUI.DisabledGroupScope(isDefaultMaterial))
            {
                EditorGUI.BeginChangeCheck();
                _materialEditor.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck())
                {
                    _trueShadowCustomMaterial.OnMaterialModified();
                }
            }
        }
    }

    void OnDisable()
    {
        if (_materialEditor)
        {
            DestroyImmediate(_materialEditor);
        }
    }
}
}
