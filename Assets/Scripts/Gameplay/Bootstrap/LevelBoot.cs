using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XeptKit.Core;
using XeptKit.Scenes;

namespace XeptGame.Gameplay
{
    /// <summary>
    /// 关卡自举（关卡场景根，**关卡模板的一部分**，GameplayFlow_Design.md §2.4/§7.3）：
    /// **Editor 直接 Play 关卡**时发起开始游戏请求（<see cref="GameLoadingManager.RequestStart"/>——
    /// 只发请求，不加载任何场景；基座组由 GameplayLoadState 经 IAssetLoader 按 key 加载，
    /// 模块随基座激活自初始化，关卡组经本组件可选注入）——逻辑完全由状态机/加载编排负责。
    /// <list type="bullet">
    /// <item>Build 流程：启动请求已由 AppEntryBoot 发出（门已置位）→ 本组件 **no-op**；</item>
    /// <item>Editor 直接 Play 关卡：请求未发出 → 发请求（<see cref="levelGroup"/> 可 null =
    /// 纯关卡测试；填 = 搭配完整关卡组测试）。</item>
    /// </list>
    /// 无 #if UNITY_EDITOR 特判——靠"启动请求门是否已置位"判别，Build 下自然 no-op（显式装配纪律）。
    /// </summary>
    public sealed class LevelBoot : MonoBehaviour
    {
        [SerializeField] private SceneGroup levelGroup;

        private void Awake()
        {
            var app = AppManager.Context;
            if (app != null && app.StartRequested.Task.Status != UniTaskStatus.Pending)
            {
                return;
            }

            GameLoadingManager.RequestStart(new GameStartRequest(levelGroup));
        }
    }
}
