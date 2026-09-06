using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LeTai.Common.Editor
{
static class EditorInternal
{
    static readonly MethodInfo DO_FLOAT_FIELD_METHOD;
#if UNITY_6000_0_OR_NEWER
    static readonly PropertyInfo RECYCLED_EDITOR_PROPERTY;
#else
    static readonly FieldInfo  RECYCLED_EDITOR_PROPERTY;
#endif
    static readonly FieldInfo FLOAT_FIELD_FORMAT_STRING_CONST;

    static EditorInternal()
    {
        var                editorGUIType = typeof(EditorGUI);
        const BindingFlags flags         = BindingFlags.NonPublic | BindingFlags.Static;

        Type[] argumentTypes = {
            Assembly.GetAssembly(editorGUIType).GetType("UnityEditor.EditorGUI+RecycledTextEditor"),
            typeof(Rect),
            typeof(Rect),
            typeof(int),
            typeof(float),
            typeof(string),
            typeof(GUIStyle),
            typeof(bool)
        };
        DO_FLOAT_FIELD_METHOD = editorGUIType.GetMethod("DoFloatField", flags, null, argumentTypes, null);
#if UNITY_6000_0_OR_NEWER
        RECYCLED_EDITOR_PROPERTY = editorGUIType.GetProperty("s_RecycledEditor", flags);
#else
        RECYCLED_EDITOR_PROPERTY = editorGUIType.GetField("s_RecycledEditor", flags);
#endif
        FLOAT_FIELD_FORMAT_STRING_CONST = editorGUIType.GetField("kFloatFieldFormatString", flags);
    }

    internal static float DoFloatFieldInternal(
        Rect     position,
        Rect     dragHotZone,
        int      id,
        float    value,
        string   formatString = null,
        GUIStyle style        = null,
        bool     draggable    = true
    )
    {
        style        = style ?? EditorStyles.numberField;
        formatString = formatString ?? (string)FLOAT_FIELD_FORMAT_STRING_CONST.GetValue(null);

        var editor = RECYCLED_EDITOR_PROPERTY.GetValue(null);

        return (float)DO_FLOAT_FIELD_METHOD.Invoke(null, new[] {
            editor,
            position,
            dragHotZone,
            id,
            value,
            formatString,
            style,
            draggable
        });
    }
}
}
