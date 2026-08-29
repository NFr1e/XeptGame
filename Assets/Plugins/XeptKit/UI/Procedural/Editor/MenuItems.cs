using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// 右键菜单和 GameObject 菜单项。
    /// </summary>
    public static class MenuItems
    {
        private const string CreateProceduralImagePath = "GameObject/UI/Procedural Image";

        [MenuItem(CreateProceduralImagePath, false, 10)]
        private static void CreateProceduralImage(MenuCommand menuCommand)
        {
            GameObject go = new("Procedural Image");
            go.AddComponent<ProceduralImage>();

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

            Canvas parentCanvas = go.GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                GameObject canvasGo = new GameObject("Canvas");
                Canvas canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<CanvasScaler>();
                canvasGo.AddComponent<GraphicRaycaster>();

                GameObjectUtility.SetParentAndAlign(go, canvasGo);
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Procedural Image");
            Selection.activeGameObject = go;
        }

        [MenuItem(CreateProceduralImagePath, true)]
        private static bool ValidateCreateProceduralImage()
        {
            return true;
        }

        /// <summary>
        /// 替代 Unity 内置 RemoveComponent，在销毁前先通知 EffectPipeline。
        /// Unity 编辑器 RemoveComponent 不触发 OnDisable/OnDestroy。
        /// CONTEXT/BaseEffect 会匹配所有 BaseEffect 子类。
        /// </summary>
        [MenuItem("CONTEXT/BaseEffect/Remove Effect", true, 1000)]
        private static bool ValidateRemoveEffect(MenuCommand command)
        {
            return command.context is BaseEffect;
        }

        [MenuItem("CONTEXT/BaseEffect/Remove Effect", false, 1000)]
        private static void RemoveEffect(MenuCommand command)
        {
            var effect = command.context as BaseEffect;
            if (effect == null) return;

            // Editor 清理效果特定状态（Shader 属性等）
            effect.CleanupBeforeDestroy();

            // 保存管线引用（销毁后无法从 effect 上获取）
            EffectPipeline pipeline = null;
            effect.TryGetComponent(out pipeline);

            Undo.DestroyObjectImmediate(effect);

            if (pipeline != null)
            {
                // Undo.DestroyObjectImmediate 不触发 OnDisable/OnDestroy：
                // RefreshEffects 重扫列表移除已销毁效果（public API，无友元程序集依赖），
                // Invalidate 触发宿主网格/材质重建——等价样本 UnregisterEffect 内部的 Invalidate，
                // 否则已编码进网格的效果参数（圆角半径/描边等）会残留，表现为"移除无效"。
                pipeline.RefreshEffects();
                pipeline.Invalidate();
            }
        }
    }
}
