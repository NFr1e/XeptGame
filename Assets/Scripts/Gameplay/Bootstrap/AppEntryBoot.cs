using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XeptKit.Core;
using XeptKit.Scenes;

namespace XeptGame.Gameplay
{
    /// <summary>
    /// 测试用，模拟Splash -> MainMenu流程。
    /// </summary>
    public sealed class AppEntryBoot : MonoBehaviour
    {
        [SerializeField] private SceneReference mainMenu;
        [SerializeField] private float bootDelay = 0f;

        private void Awake()
        {
            _ = BootAsync();
        }

        private async UniTaskVoid BootAsync()
        {
            try
            {
                if (bootDelay > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(bootDelay), cancellationToken: KitLifecycle.GlobalToken);
                }

                _ = AppEntry.ScenesManager.LoadSceneAsync(mainMenu, cancellationToken: KitLifecycle.GlobalToken);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
    }
}
