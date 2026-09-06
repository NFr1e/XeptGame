using UnityEditor;
using UnityEngine;
using EGU = UnityEditor.EditorGUIUtility;

namespace LeTai.Common.Editor
{
public static class EditorGUICustom
{
    static readonly Texture2D KNOB_BG_TEXTURE = Assets.Find<Texture2D>("Knob_BG");
    static readonly Texture2D KNOB_FG_TEXTURE = Assets.Find<Texture2D>("Knob_FG");

    static readonly Color KNOB_BG_COLOR;
    static readonly Color KNOB_FG_COLOR;
    static readonly Color KNOB_FG_COLOR_ACTIVE;

    static EditorGUICustom()
    {
        if (EGU.isProSkin)
        {
            KNOB_BG_COLOR        = new Color(.164f, .164f, .164f);
            KNOB_FG_COLOR        = new Color(.701f, .701f, .701f);
            KNOB_FG_COLOR_ACTIVE = new Color(.49f,  .67f,  .94f);
        }
        else
        {
            KNOB_BG_COLOR        = new Color(.941f, .941f, .941f);
            KNOB_FG_COLOR        = new Color(.239f, .239f, .239f);
            KNOB_FG_COLOR_ACTIVE = new Color(.054f, .274f, .549f);
        }
    }


    public static float KnobField(GUIContent label, float angle, Vector2 zeroVector, float height = 0)
    {
        if (height <= 0)
            height = EGU.singleLineHeight * 1.3f;
        float knobSize    = height + EGU.standardVerticalSpacing * (4 - 1);
        float knobYOffset = (height - knobSize) / 2;

        Rect rect = EditorGUILayout.GetControlRect(true, height);

        var labelRect = new Rect(rect) {
            y      = rect.y + (height - EGU.singleLineHeight) / 2,
            height = EGU.singleLineHeight
        };

        var oldLabelWidth = EGU.labelWidth;
        EGU.labelWidth -= height;

        int fieldId   = GUIUtility.GetControlID(FocusType.Keyboard, labelRect);
        var fieldRect = EditorGUI.PrefixLabel(labelRect, fieldId, label);
        labelRect.xMax  =  fieldRect.x;
        fieldRect.x     += height;
        fieldRect.width -= height;

        Rect knobRect = new Rect(rect.x + EGU.labelWidth + knobYOffset,
                                 rect.y + knobYOffset,
                                 knobSize, knobSize);

        angle = Knob(knobRect, angle, zeroVector);
        angle = EditorInternal.DoFloatFieldInternal(fieldRect, labelRect, fieldId, angle);

        EGU.labelWidth = oldLabelWidth;

        return angle;
    }

    public static float Knob(Rect position, float angle, Vector2 zeroVector)
    {
        int controlID = GUIUtility.GetControlID(FocusType.Passive, position);

        if (Event.current != null)
        {
            if (Event.current.type == EventType.MouseDown && position.Contains(Event.current.mousePosition))
            {
                GUIUtility.hotControl = controlID;

                var dir = (Event.current.mousePosition - position.center).normalized;
                angle = MathCustom.VecToAngle360(zeroVector, dir);

                GUI.changed = true;
            }
            else if (Event.current.type == EventType.MouseUp && GUIUtility.hotControl == controlID)
            {
                GUIUtility.hotControl = 0;
            }
            else if (Event.current.type == EventType.MouseDrag && GUIUtility.hotControl == controlID)
            {
                var dir = (Event.current.mousePosition - position.center).normalized;
                angle = MathCustom.VecToAngle360(zeroVector, dir);

                GUI.changed = true;
            }
            else if (Event.current.type == EventType.Repaint)
            {
                var notRotated  = GUI.matrix;
                var oldColor    = GUI.color;
                var highlighted = GUIUtility.hotControl == controlID;

                GUIUtility.RotateAroundPivot(angle, position.center);
                GUI.color = KNOB_BG_COLOR;
                GUI.DrawTexture(position, KNOB_BG_TEXTURE, ScaleMode.ScaleToFit, true, 1);
                GUI.color = highlighted ? KNOB_FG_COLOR_ACTIVE : KNOB_FG_COLOR;
                GUI.DrawTexture(position, KNOB_FG_TEXTURE, ScaleMode.ScaleToFit, true, 1);

                GUI.matrix = notRotated;
                GUI.color  = oldColor;
            }
        }

        return angle;
    }
}
}
