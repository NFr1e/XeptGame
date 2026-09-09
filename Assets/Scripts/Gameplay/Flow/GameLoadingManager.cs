using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using XeptKit.Core;
using XeptKit.Scenes;

namespace XeptGame.Game
{
    /// <summary>
    /// 游戏加载编排器（纯 C#，GameplayFlow_Design.md §4）：**读门控、推进状态**——
    /// 状态类（执行器）负责加载/初始化并置位完成门，本管理器统一消费门控驱动 GameFSM 转移。
    /// 门控仅覆盖确定性事项（基座组/关卡组加载完成）；业务模块自生命周期初始化、不进加载门控。
    /// </summary>
    public static class GameLoadingManager
    {
        private static GameStartRequest _pendingRequest;

        /// <summary>
        /// 请求开始游戏（场景组件调用：主菜单"开始游戏"按钮 / LevelBoot Editor 直接 Play 关卡）。
        /// 只写请求 + 置位启动请求门（AppContext.StartRequested，门同步消除与流程就绪的时序竞态）；
        /// **不等待、不启动编排**——RunAsync 由 GameplayManager.Start 常驻启动（FSM 一创建编排者就绪，
        /// 请求早于/晚于都经门同步被消费；本方法重复调用只覆盖请求，不重复启动编排）。
        /// 错误重试（AppManager.RetryGame）：门已置位 → 新 RunAsync 立即消费并重放最近请求（重试语义）。
        /// </summary>
        public static void RequestStart(GameStartRequest request)
        {
            _pendingRequest = request;
            AppManager.Context?.StartRequested.TrySetResult();
        }

        /// <summary>
        /// 启动加载编排（GameplayManager.Start 调用，fire-and-forget，常驻）：
        /// await 启动请求门（无超时：无请求 = 停在 Boot 等用户/菜单）→ 写关卡组上下文 →
        /// 推进两阶段加载：基座加载 → 关卡加载 → Playing（业务模块自生命周期初始化、不占状态）。
        /// **取消 = 正常收敛**：GameplayManager.Shutdown（AppFSM.ErrorState 收尾 / 会话结束）取消本机令牌
        /// 即 OCE 退出——本方法是 fire-and-forget，OCE 就地吞掉（返回），不产生未观察异常；
        /// 引用随方法退出释放（错误后不必活到会话结束）。
        /// 失败路径：状态类 Fail 上报错误事件（AppFSM 转 ErrorState → Shutdown 取消收敛），**不置位完成门**。
        /// </summary>
        public static async UniTask RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await RunCoreAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 收尾/会话结束取消——正常收敛（fire-and-forget 下静默退出）
            }
        }

        private static async UniTask RunCoreAsync(CancellationToken cancellationToken)
        {
            var app = AppManager.Context;
            var game = GameManager.Context;
            var fsm = GameManager.GameFSM;
            if (app == null || game == null || fsm == null)
            {
                return;
            }

            // ① 等待启动请求（无超时——等待用户是正常语义，仅取消退出）
            await app.StartRequested.Task.AttachExternalCancellation(cancellationToken);

            // ② 写关卡组上下文（基座组不在请求内——GameplayLoadState 经 AssetLoader 按 key 加载）
            game.LevelGroup = _pendingRequest.LevelGroup;

            // ③ 推进加载阶段（基座 → 关卡）
            fsm.RequestChange<GameplayLoadState>(cancellationToken);
            await game.GameplayReady.Task.AttachExternalCancellation(cancellationToken);

            fsm.RequestChange<LevelLoadState>(cancellationToken);
            await game.LevelReady.Task.AttachExternalCancellation(cancellationToken);

            fsm.RequestChange<PlayingState>(cancellationToken);
        }
    }
}
