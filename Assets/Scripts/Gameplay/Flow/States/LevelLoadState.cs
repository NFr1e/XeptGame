using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using XeptKit.Scenes;

namespace XeptGame.Gameplay
{
    /// <summary>
    /// 游戏流程加载阶段二：**关卡组加载**（执行器，GameplayFlow_Design.md §4.2）——
    /// 幂等加载 <see cref="GameplayContext.LevelGroup"/>（启动请求写入；null 或主场景已加载则跳过——
    /// 覆盖 Editor 直接 Play 关卡，只做基座/模块初始化），完成后置位 <see cref="GameplayContext.LevelReady"/>
    /// （**不自驱**，GameLoadingManager 消费推进 → Playing）。
    /// </summary>
    public sealed class LevelLoadState : GameplayStateBase
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
