// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using UnityEditor;
using UnityEngine;

namespace LeTai.Common.Editor
{
[CustomPropertyDrawer(typeof(TinyTween.Spring))]
public class SpringDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty approxDurationProp = property.FindPropertyRelative(nameof(TinyTween.Spring.approxDuration));
        SerializedProperty overshootProp      = property.FindPropertyRelative(nameof(TinyTween.Spring.overshoot));
        SerializedProperty stiffnessProp      = property.FindPropertyRelative(nameof(TinyTween.Spring.stiffness));
        SerializedProperty dampingProp        = property.FindPropertyRelative(nameof(TinyTween.Spring.damping));

        position = EditorGUI.PrefixLabel(position, label);

        using var _       = new EditorCustom.LabelWidthScope(64f);
        using var changes = new EditorGUI.ChangeCheckScope();

        float lineHeight = EditorGUIUtility.singleLineHeight;
        Rect durationRect = new Rect(position.x, position.y,
                                     position.width, lineHeight);
        Rect overshootRect = new Rect(position.x, position.y + lineHeight + EditorGUIUtility.standardVerticalSpacing,
                                      position.width, lineHeight);

        float approxDuration = EditorGUI.FloatField(durationRect, "Duration", approxDurationProp.floatValue);
        float overshoot      = EditorGUI.Slider(overshootRect, "Overshoot", overshootProp.floatValue, 0, .5f);

        if (changes.changed)
        {
            TinyTween.Spring s = TinyTween.Spring.DurationOvershoot(approxDuration, overshoot);

            approxDurationProp.floatValue = s.approxDuration;
            overshootProp.floatValue      = s.overshoot;
            stiffnessProp.floatValue      = s.stiffness;
            dampingProp.floatValue        = s.damping;
        }

        EditorGUI.EndProperty();
    }
}
}
