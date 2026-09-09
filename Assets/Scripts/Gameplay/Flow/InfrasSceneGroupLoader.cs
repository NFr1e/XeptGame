using System.Threading;
using Cysharp.Threading.Tasks;
using XeptKit.Asset;
using XeptKit.Scenes;

namespace XeptGame.Game
{
    /// <summary>
    /// 基础设施场景组加载辅助（纯 C# 静态，应用级与玩法级共用）：
    /// 按资产 key 加载场景组（幂等：组内主场景已加载则跳过——覆盖 Editor 直接 Play 场景）。
    /// 服务经参数注入（不持域上下文）——应用级（AppFSM.StartingState 加载 AppCore 组）与
    /// 玩法级（GameFSM.GameplayLoadState 加载基座组）复用同一逻辑。
    /// </summary>
    public static class InfrasSceneGroupLoader
    {
        /// <summary>按 key 加载基础设施场景组（幂等判定经 GameLoadingHelper，见 XeptKit_Scene_Feedback.md P2）。</summary>
        public static async UniTask LoadAsync(
            IAssetLoader assetLoader, IScenesManager scenesManager, string groupAssetKey, CancellationToken token)
        {
            using (var handle = await assetLoader.LoadAsync<SceneGroup>(groupAssetKey, token))
            {
                var group = handle.Result;
                if (!GameLoadingHelper.IsGroupLoaded(group, scenesManager))
                {
                    await scenesManager.LoadSceneGroupAsync(group, token);
                }
            }
        }
    }
}
