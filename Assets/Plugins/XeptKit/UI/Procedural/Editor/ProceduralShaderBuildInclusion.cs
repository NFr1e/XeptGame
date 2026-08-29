using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// 构建预处理：自动把 ProceduralImage 的自定义 SDF Shader 加入
    /// GraphicsSettings 的 Always Included Shaders。
    ///
    /// 背景：该 shader 仅经运行时 <see cref="Shader.Find"/> 按名字查找（非编译期资产引用），
    /// 构建剥离器无法推断其依赖——不显式包含会被剥离（编辑器有、包里无，圆角/描边静默失效）。
    /// 本类在每次构建前自动补齐 GraphicsSettings 的 m_AlwaysIncludedShaders，业务零配置。
    /// </summary>
    public class ProceduralShaderBuildInclusion : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // 经资产路径加载（Editor 侧不触碰 internal 常量，兑现「无友元程序集」决议）；
            // 路径相对 Kit 根目录约定（总设计 §6.3：Assets/XeptKit 为根）稳定。
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/XeptKit/UI/Procedural/Shaders/ProceduralImage.shader");
            if (shader == null)
                return;

            // GraphicsSettings 资产的 m_AlwaysIncludedShaders 序列化数组
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0)
                return;

            SerializedObject serialized = new SerializedObject(assets[0]);
            SerializedProperty prop = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (prop == null)
                return;

            for (int i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    return; // 已包含，无需处理
            }

            prop.arraySize++;
            prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = shader;
            serialized.ApplyModifiedProperties();
        }
    }
}
