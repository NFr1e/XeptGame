using XeptKit.Scenes;

namespace XeptGame.Game
{
    /// <summary>
    /// 启动请求（GameplayFlow_Design.md §4.1）：场景组件（主菜单"开始游戏"按钮 / LevelBoot / LoadLevelButton）写入，
    /// GameLoadingManager 消费。**基座组不在请求内**——基座场景组由 GameplayLoadState 经 IAssetLoader
    /// 按 key 加载（index 0 为 AppEntry 场景，基座非启动即加载；序列化注入有鸡生蛋问题）。
    /// <see cref="LevelGroup"/> 可 null：null = 已在关卡中/无关卡组（Editor 直接 Play 关卡），LevelLoad 幂等跳过。
    /// </summary>
    public readonly struct GameStartRequest
    {
        /// <summary>关卡组（可 null = 已在关卡中/无关卡组）。</summary>
        public readonly SceneGroup LevelGroup;

        public GameStartRequest(SceneGroup levelGroup)
        {
            LevelGroup = levelGroup;
        }
    }
}
