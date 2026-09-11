using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using XeptKit.Core;
using XeptKit.Scenes;

namespace XeptGame.Game
{
    /// <summary>
    /// 游戏流程加载阶段二：**关卡组加载**（执行器，GameplayFlow_Design.md §4.2）——
    /// **EnterAsync**：幂等加载 <see cref="GameContext.LevelGroup"/>（null/主场景已加载则跳过——
    /// 覆盖 Editor 直接 Play 关卡）→ **把关卡组主场景设为 Unity 活跃场景**（环境归属，见下）→
    /// 置位 <see cref="GameContext.LevelReady"/>（**不自驱**）。
    /// 业务模块随关卡场景激活自生命周期初始化（不进加载门控）。
    /// 失败：关卡组加载失败 Fail + 停驻（不置位门——错误路径收敛）。
    /// <para>
    /// **环境归属（GameplayFlow_Design.md §4.6）**：`RenderSettings`/`LightmapSettings` 是<b>每场景</b>设置，
    /// 多场景同开时 Unity 以**活跃场景**的设置为准（同时活跃场景是脚本新建对象的落点）。
    /// 因此"关卡拥有环境"落实为本状态加载完成后调 <c>SetActiveScene(组内主场景)</c>；
    /// 应用壳（AppEntry）与基座组刻意不设活跃，保持中性默认值，避免按启动路径不同而串味。
    /// </para>
    /// </summary>
    public sealed class LevelLoadState : GameStateBase
    {
        public override async UniTask EnterAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var group = Game.LevelGroup;

                // 幂等跳过（组已就位，如 Editor 直接 Play 关卡）时不取句柄：此时该场景已被 Unity 设为活跃
                SceneGroupHandle groupHandle = null;
                if (group != null && !GameLoadingHelper.IsGroupLoaded(group, Game.ScenesManager))
                {
                    groupHandle = await Game.ScenesManager.LoadSceneGroupAsync(group, cancellationToken);
                }

                // 顺序纪律：环境切换必须在 LevelReady 置位之前——否则 Playing 已开始、环境才整体替换（可见闪变）
                if (groupHandle?.MainScene != null)
                {
                    Game.ScenesManager.SetActiveScene(groupHandle.MainScene);
                    Log.Info($"[LevelLoadState] 活跃场景已切至关卡组主场景：{groupHandle.MainScene.SceneRef.SceneName}");
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
