using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using XeptGame.Game.Flow;
using XeptKit.Core;

namespace XeptGame.Game
{
    public class GameplayUnloadState : GameStateBase
    {
        public override UniTask InitAsync(CancellationToken cancellationToken = default)
        {
            Game.LifecycleBridge.gameObject.Destroy();
            GameplaySessionEntry.Instance?.Dispose();

            return UniTask.CompletedTask;
        }
    }
}
