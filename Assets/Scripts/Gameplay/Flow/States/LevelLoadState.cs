using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using XeptKit.Scenes;

namespace XeptGame.Game
{
    /// <summary>
    /// 游戏流程加载阶段二：**关卡组加载**（执行器，GameplayFlow_Design.md §4.2）——
    /// **EnterAsync**：幂等加载 <see cref="GameContext.LevelGroup"/>（null/主场景已加载则跳过——
    /// 覆盖 Editor 直接 Play 关卡），完成后置位 <see cref="GameContext.LevelReady"/>（**不自驱**）。
    /// 业务模块随关卡场景激活自生命周期初始化（不进加载门控）。
    /// 失败：关卡组加载失败 Fail + 停驻（不置位门——错误路径收敛）。
    /// </summary>
    public sealed class LevelLoadState : GameStateBase
    {
        public override async UniTask EnterAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var group = Game.LevelGroup;
                if (group != null && !GameLoadingHelper.IsGroupLoaded(group, Game.ScenesManager))
                {
                    await Game.ScenesManager.LoadSceneGroupAsync(group, cancellationToken);
                }

                Game.LevelReady.TrySetResult(); // 关卡组就绪（GameLoadingManager 消费推进 → Playing）
            }
            catch (OperationCanceledException)
            {
                throw; // 会话结束/取消——静默（FSM 取消收敛语义）
            }
            catch (Exception ex)
            {
                Fail($"关卡组加载失败（{GetType().Name}）：{ex.Message}", ex);
                // 停驻本状态（不置位完成门——错误路径收敛同 GameplayLoadState）
            }
        }
    }
}
