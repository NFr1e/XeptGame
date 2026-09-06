// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using UnityEditor;
using UnityEngine;

namespace LeTai.Common.Editor
{
public static class EditorCustom
{
    static readonly GUIContent LABEL = new GUIContent();

    public class LabelWidthScope : GUI.Scope
    {
        readonly float oldLabelWidth;

        public LabelWidthScope(float labelWidth)
        {
            oldLabelWidth               = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = labelWidth;
        }

        protected override void CloseScope()
        {
            EditorGUIUtility.labelWidth = oldLabelWidth;
        }
    }

    public static (Rect labelRect, Rect fieldRect) PrefixLabel(Rect rect, int id, GUIContent label)
    {
        var fieldRect = EditorGUI.PrefixLabel(rect, id, label);
        var labelRect = new Rect(rect.x, rect.y, rect.width - fieldRect.width, rect.height);

        return (labelRect, fieldRect);
    }

    public static float LabelFloatField(Rect rectControl, Rect rectLabel, GUIContent label, float value, GUIStyle styleLabel = null, GUIStyle styleField = null)
    {
        var controlId = GUIUtility.GetControlID(FocusType.Keyboard, rectControl);
        EditorGUI.HandlePrefixLabel(rectControl, rectLabel, label, controlId, styleLabel);
        return EditorInternal.DoFloatFieldInternal(rectControl, rectLabel, controlId, value, style: styleField);
    }

    public static void ComputedPropertyField(SerializedProperty property, Func<SerializedProperty, string> compute)
    {
        using var _ = new EditorGUILayout.HorizontalScope();
        EditorGUILayout.PropertyField(property);
        EditorGUILayout.LabelField(compute(property), GUILayout.ExpandWidth(false));
    }

    public static bool LinkButton(string label, params GUILayoutOption[] options)
    {
        LABEL.text = label;
        return LinkButton(LABEL, options);
    }

    public static bool LinkButton(GUIContent label, params GUILayoutOption[] options)
    {
        var rect = GUILayoutUtility.GetRect(label, EditorStyles.linkLabel, options);
        rect = EditorGUI.IndentedRect(rect);

        // var prevHandleColor = Handles.color;
        // Handles.color = EditorStyles.linkLabel.normal.textColor;
        // Handles.DrawLine(new Vector3(position.xMin + EditorStyles.linkLabel.padding.left,  position.yMax),
        //                  new Vector3(position.xMax - EditorStyles.linkLabel.padding.right, position.yMax));
        // Handles.color = prevHandleColor;

        EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

        return GUI.Button(rect, label, EditorStyles.linkLabel);
    }
}
}
