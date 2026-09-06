using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LeTai.Common.Editor
{
public class EditorProperty
{
    public readonly SerializedProperty serializedProperty;

    readonly SerializedObject   serializedObject;
    readonly MethodInfo         propertySetter;
    readonly SerializedProperty dirtyFlag;

    public EditorProperty(SerializedObject obj, string name, string serializedName = null)
    {
        serializedObject   = obj;
        serializedProperty = FindPropertyByName(serializedObject, name);
        propertySetter     = serializedObject.targetObject.GetType().GetProperty(name).SetMethod;
        dirtyFlag          = serializedObject.FindProperty("modifiedFromInspector");
    }

    static SerializedProperty FindPropertyByName(SerializedObject obj, string name)
    {
        var sb       = new StringBuilder(name.Length + 1);
        var lower    = sb.Append(char.ToLowerInvariant(name[0])).Append(name[1..]).ToString();
        var property = obj.FindProperty(lower);
        if (property != null)
            return property;

        sb.Clear();
        property = obj.FindProperty(sb.Append('_').Append(lower).ToString());
        if (property != null)
            return property;

        throw new ArgumentException($"Can't automatically find serialized name for property {name} in {obj.targetObject.GetType()}");
    }

    public void Draw(params GUILayoutOption[] options)
    {
        using (var scope = new EditorGUI.ChangeCheckScope())
        {
            EditorGUILayout.PropertyField(serializedProperty, options);

            if (!scope.changed)
                return;

            if (dirtyFlag != null)
                dirtyFlag.boolValue = true;

            serializedObject.ApplyModifiedProperties();

            if (serializedProperty.propertyType != SerializedPropertyType.Generic) // Not needed for now
            {
                var propertyValue = GetPropertyValue();
                CallSetters(propertyValue);
            }

            // In case the setter changes any serialized data
            serializedObject.Update();
        }
    }

    public void CallSetters(object value)
    {
        foreach (var target in serializedObject.targetObjects)
            propertySetter.Invoke(target, new[] { value });
    }

    object GetPropertyValue()
    {
        switch (serializedProperty.propertyType)
        {
        case SerializedPropertyType.ObjectReference:
            return serializedProperty.objectReferenceValue;
        case SerializedPropertyType.Float:
            return serializedProperty.floatValue;
        case SerializedPropertyType.Integer:
            return serializedProperty.intValue;
        case SerializedPropertyType.Rect:
            return serializedProperty.rectValue;
        case SerializedPropertyType.Enum:
            return serializedProperty.enumValueIndex;
        case SerializedPropertyType.Boolean:
            return serializedProperty.boolValue;
        case SerializedPropertyType.Color:
            return serializedProperty.colorValue;
        case SerializedPropertyType.Vector2:
            return serializedProperty.vector2Value;
        default: throw new NotImplementedException($"Type {serializedProperty.propertyType} is not implemented");
        }
    }
}
}
