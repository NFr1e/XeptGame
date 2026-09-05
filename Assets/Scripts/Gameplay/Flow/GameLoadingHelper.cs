using XeptKit.Scenes;

namespace XeptGame.Game
{
    /// <summary>
    /// 加载辅助（纯 C# 静态，供状态类复用）：
    /// 组是否已就位的幂等判定——主场景（IsMainScene 条目）已加载 = 组已就位；
    /// 无主场景标记时以首条目兜底。覆盖"关卡即当前场景"（Editor 直接 Play）与部分加载场景。
    /// 经 <see cref="IScenesManager.IsSceneLoaded"/> 判定——依赖其**世界事实**语义（任意方式加载的场景
    /// 均可见，见 XeptKit_Scene_Feedback.md 决议：管理/查询分离）。
    /// </summary>
    public static class GameLoadingHelper
    {
        /// <summary>组是否已就位（幂等判定，见类注释）。</summary>
        public static bool IsGroupLoaded(SceneGroup group, IScenesManager scenesManager)
        {
            if (group == null || group.Entries == null || group.Entries.Length == 0)
            {
                return false;
            }

            foreach (var entry in group.Entries)
            {
                if (entry.IsMainScene)
                {
                    return scenesManager.IsSceneLoaded(entry.SceneRef);
                }
            }

            return scenesManager.IsSceneLoaded(group.Entries[0].SceneRef);
        }
    }
}
