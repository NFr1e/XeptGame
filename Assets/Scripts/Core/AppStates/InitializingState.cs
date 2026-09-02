using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace XeptGame
{
    /// <summary>
    /// 基础设施初始化：Addressables 初始化等（原 AppEntry.InitializeAsync 迁入）。
    /// 完成 → Starting；失败 → Error（状态内显式自救：错误写入 App.LastError）。
    /// 自驱转移在钩子内发起（pending 机制保证时序）；异步绑定转移令牌，会话结束自动取消。
    /// </summary>
    public sealed class InitializingState : AppStateBase
    {
        public override async UniTask EnterAsync(CancellationToken cancellationToken = default)
        {
            #region InitializaAddressables
            try
            {
                AsyncOperationHandle initHandle = Addressables.InitializeAsync();
                await initHandle.ToUniTask(cancellationToken: cancellationToken);

                //移动到了AppEntry中的AfterSceneLoaded中
                //Fsm.RequestChange<StartingState>();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                App.LastError = new Exception(
                    $"[AppFSM] 初始化失败（{GetType().Name}）：{ex.Message}\n" +
                    "提示：若为 Addressables 加载错误，请确认已创建 Addressable Asset Settings 并构建内容" +
                    "（Window > Asset Management > Addressables > Groups，首次打开会提示创建 Settings）。",
                    ex);
                App.FailureSource = AppFailureSource.Startup; // ErrorState 重试路径据此分派
                Fsm.RequestChange<ErrorState>();
            }
            #endregion
        }
    }
}
