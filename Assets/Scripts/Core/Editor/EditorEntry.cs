using UnityEditor;
using XeptGame;
using XeptKit.Asset;

namespace XeptGame.Editor
{
    /// <summary>
    /// 编辑器环境引导
    /// </summary>
    [InitializeOnLoad]
    internal static class EditorEntry
    {
        static EditorEntry()
        {
            AppEntry.RegisterEditorAssetLoader(new EditorAssetLoader());
        }
    }
}
