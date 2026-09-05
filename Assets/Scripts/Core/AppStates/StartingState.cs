using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using XeptGame.Core;
using XeptGame.Game;

namespace XeptGame
{
    /// <summary>
    /// 内容入口（GameplayFlow_Design.md §2.1）：**先加载应用级基础设施组（AppCore 组：UI 上下文/音频等）**，
    /// 再拉起 GameplayFSM（fire-and-forget——AppFSM 不管理其生命周期），然后进入运行态。
    /// 层职责：应用级基础设施属 **AppFSM 层**（StartingState 加载 AppCore 组）；玩法级基座属 GameplayFSM 层
    /// （GameplayLoadState 加载基座组）。GameplayFSM 独立驱动后续流程（Boot 停驻 → 启动请求 → 两阶段加载 → Playing）。
    /// 失败（组加载 / GameplayContext 构造异常）→ 写 App.LastError + 转 ErrorState（显式自救）。
    /// </summary>
    public sealed class StartingState : AppStateBase
    {
        public override async UniTask EnterAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // 应用级基础设施组按 key 幂等加载（AppCore 组：AppCore 主场景 + AppMainMenu；index 0 为 AppEntry
                // 场景，应用级组非启动即加载，序列化注入有鸡生蛋问题故走资产 key，见 GameplayFlow_Design.md §2.2）。
                // AppEntryBoot 仅测试用（模拟 Splash → 主菜单跳转），不参与本组加载。
                await InfrasSceneGroupLoader.LoadAsync(
                    AppEntry.AssetLoader, AppEntry.ScenesManager,
                    XeptGameConsts.AssetKeys.AppCoreGroup, cancellationToken);

                // Gameplay 域上下文：自建域 EventBus（隔离）+ 服务注入；启动请求经 GameLoadingManager.RequestStart 写入
                var context = new GameContext(AppEntry.EventBus, AppEntry.ScenesManager, AppEntry.AssetLoader);
                GameManager.Start(context);
            }
            catch (OperationCanceledException)
            {
                throw; // 会话结束/取消——静默（FSM 取消收敛语义）
            }
            catch (Exception ex)
            {
                App.LastError = new Exception(
                    $"[AppFSM] 应用级基础设施加载/游戏流程启动失败（{GetType().Name}）：{ex.Message}", ex);
                App.FailureSource = AppFailureSource.Startup; // ErrorState 重试路径据此分派（RetryLaunch 全量重跑）
                Fsm.RequestChange<ErrorState>();
                return;
            }

            Fsm.RequestChange<RunningState>();
        }
    }
}
