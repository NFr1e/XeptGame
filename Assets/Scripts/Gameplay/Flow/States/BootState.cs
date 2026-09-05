using System.Threading;
using Cysharp.Threading.Tasks;

namespace XeptGame.Game
{
    /// <summary>
    /// 游戏流程启动态（GameplayFlow_Design.md §4.1）：**停驻**——系统就绪、等待内容入口。
    /// 是否进加载由"用户/配置"决定：场景组件写启动请求（<see cref="GameLoadingManager.RequestStart"/>），
    /// GameLoadingManager 消费请求门后推进。请求源：主菜单"开始游戏"按钮（<c>LoadLevelButton</c> 形态）/
    /// LevelBoot（Editor 直接 Play 关卡）/ LoadLevelButton（Editor 直接 Play 基座场景测试）。纯 C#，无场景依赖。
    /// </summary>
    public sealed class BootState : GameStateBase
    {
        public override UniTask EnterAsync(CancellationToken cancellationToken = default)
        {
            // 停驻：不做任何自驱转移；等待启动请求（GameLoadingManager 消费后推进）。
            return UniTask.CompletedTask;
        }
    }
}
