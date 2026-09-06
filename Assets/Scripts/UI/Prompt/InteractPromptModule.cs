using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XeptGame.Interaction;
using XeptKit.Core;
using XeptKit.UI.Manager;

namespace XeptGame.UI
{
    /// <summary>
    /// 交互提示模块（基座场景宿主，自生命周期）：挂在 GameplayCore 基座——
    /// OnEnable 打开常驻 HUD Form（executor/相机经 args 注入 PromptRuntimeContext），
    /// OnDisable 关闭（一轮会话装载/卸载对齐）。显隐与内容由 FormLogic 按选中宿主驱动（表单不随选中开关）。
    /// 装配纪律：FormEntry / executor / 相机 全序列化引用；缺引用 Log.Error。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractPromptModule : MonoBehaviour
    {
        [Tooltip("HUD 提示 Form 入口（Prefab 根含 UIForm + InteractPromptHudFormLogic，Group = HUD）")]
        [SerializeField] private FormEntry promptFormEntry;

        [Tooltip("交互执行器（Player 上的 InteractionExecutor）——HostChanged/动作集数据源")]
        [SerializeField] private InteractionExecutor interactionExecutor;

        [Tooltip("投影相机（世界相机 Transform；HudBillboard 必填注入）")]
        [SerializeField] private Transform hudCamera;

        private bool _opened;

        private async void OnEnable()
        {
            if (promptFormEntry == null || interactionExecutor == null)
            {
                Log.Error("[InteractPromptModule] promptFormEntry / interactionExecutor 未接线——提示不可用。");
                return;
            }

            try
            {
                await AppEntry.UIManager.OpenAsync(
                    promptFormEntry,
                    new PromptRuntimeContext(interactionExecutor, hudCamera),
                    KitLifecycle.GlobalToken);
                _opened = true;
            }
            catch (OperationCanceledException)
            {
                // 会话结束/取消——静默（表单打开被中止）
            }
            catch (Exception ex)
            {
                Log.Exception(ex); // 打开失败 fail-fast 语义的本地隔离（async void 不能外抛）
            }
        }

        private void OnDisable()
        {
            if (_opened)
            {
                AppEntry.UIManager.Close(promptFormEntry);
                _opened = false;
            }
        }
    }
}
