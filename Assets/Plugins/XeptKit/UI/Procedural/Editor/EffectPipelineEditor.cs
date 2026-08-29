using UnityEditor;
using UnityEngine;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// EffectPipeline 的自定义 Inspector：效果列表（按优先级升序）+ 启用开关。
    /// 效果列表的展示与开关职责集中于此（ProceduralImage 的 Inspector 不再重复显示）。
    /// </summary>
    [CustomEditor(typeof(EffectPipeline))]
    public class EffectPipelineEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EffectPipeline pipeline = target as EffectPipeline;
            if (pipeline == null) return;

            pipeline.RefreshEffects();
            var effects = pipeline.Effects;

            if (effects == null || effects.Count == 0)
            {
                return;
            }

            // 效果列表：顺序即执行顺序（优先级升序），每行提供启用开关
            foreach (var effect in effects)
            {
                if (effect == null) continue;

                EditorGUILayout.BeginHorizontal();

                bool isActive = effect.enabled;
                bool newActive = EditorGUILayout.Toggle(isActive, GUILayout.Width(20f));
                if (newActive != isActive)
                {
                    Undo.RecordObject(effect, "Toggle Effect");
                    effect.enabled = newActive;

                    pipeline.Invalidate();
                }

                EditorGUILayout.LabelField(effect.GetType().Name, EditorStyles.label);

                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
