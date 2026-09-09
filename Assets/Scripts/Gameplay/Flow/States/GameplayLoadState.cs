using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XeptGame.Core;
using XeptGame.Game.Flow;

namespace XeptGame.Game
{
    /// <summary>
    /// 游戏流程加载阶段一：**玩法级基座场景组加载**（执行器，GameplayFlow_Design.md §4.2）——
    /// 经 <see cref="InfrasSceneGroupLoader"/> 按 key 加载基座组（index 0 为 AppEntry 场景、应用级 AppCore 组
    /// 已由 AppFSM.StartingState 加载；玩法级基座非启动即加载，序列化注入有鸡生蛋问题故走资产 key），
    /// 幂等加载（组内主场景已加载则跳过——覆盖 Editor 直接 Play 基座场景），完成后置位
    /// <see cref="GameContext.GameplayReady"/>（**不自驱**）。业务模块随基座场景激活自生命周期初始化。
    /// </summary>
    public sealed class GameplayLoadState : GameStateBase
    {
        public override UniTask InitAsync(CancellationToken cancellationToken = default)
        {
            Game.LifecycleBridge = new GameObject("[GameplaySessionLifecycleBridge]")
            {
                hideFlags = HideFlags.NotEditable
            }
            .AddComponent<GameplaySessionLifecycleBridge>()
            .Bind(GameplaySessionEntry
                .Instantiate()
                .Init());

            return UniTask.CompletedTask;
        }

        public override async UniTask EnterAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await InfrasSceneGroupLoader.LoadAsync(
                    Game.AssetLoader, Game.ScenesManager,
                    XeptGameConsts.AssetKeys.GameplaySceneGroup, cancellationToken);

                Game.GameplayReady.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Fail($"基座场景组加载失败（{GetType().Name}）：{ex.Message}", ex);
            }
        }
    }
}
