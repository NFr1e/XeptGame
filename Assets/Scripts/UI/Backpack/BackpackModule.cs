using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using XeptGame.Game.Flow;
using XeptKit.Core;
using XeptKit.UI.Manager;

namespace XeptGame.UI.Backpack
{
    /// <summary>
    /// 背包界面场景模块（Backpack_UI_Design.md B6；与 <c>InteractPromptModule</c> 同层，挂玩法基座）：
    /// 把"按键 → 开会话域数据源 → 开 Form → 暂停世界"这条链子接起来。<b>不含界面逻辑</b>
    /// （那些在 <see cref="BackpackUIFormLogic"/> 与 <see cref="BackpackUIData"/>）。
    /// <list type="bullet">
    /// <item><b>无包不打开</b>：<c>Context.Inventory == null</c>（合法状态）→ 记一条 Info 直接返回，
    /// <b>不新建背包</b>（B8 验收项）；</item>
    /// <item><b>暂停成对</b>：开成功后 <c>Request(PauseReason.Backpack)</c>，关时 <c>Release</c>；异常/取消路径
    /// 也保证不留下"暂停着却没有界面"（见 <see cref="OpenAsync"/> 的 finally）；</item>
    /// <item><b>输入绑定在 <see cref="MenuInputLayer"/>（不是 Gameplay）</b>——这是一处必须记住的坑：
    /// 暂停会把 <see cref="GameplayInputLayer"/> 整层停掉（<c>EnginePauseEffect</c>），
    /// 若把开关绑在 Gameplay 层，<b>打开后就再也按不了 Tab 关闭</b>。
    /// <c>BlockLowerLayers</c> 只在"同一个 action 上绑了多层"时才有意义，这里是独立 action，不受影响；</item>
    /// <item><b>不动游标</b>：本工程当前不锁游标（全工程无 <c>Cursor.lockState</c> 写入），
    /// 所以界面天然可点。将来若引入游标锁定，打开/关闭必须在这里成对解锁/还原。</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackpackModule : MonoBehaviour
    {
        [Tooltip("背包 Form 入口（Prefab 根含 UIForm + BackpackUIFormLogic，Group = Modal）")]
        [SerializeField] private FormEntry backpackFormEntry;

        private IDisposable _toggleBinding;
        private bool _opening;

        /// <summary>背包界面当前是否打开。</summary>
        public bool IsOpen
            => backpackFormEntry != null
               && AppEntry.UIManager != null
               && AppEntry.UIManager.IsValid(AppEntry.UIManager.GetOpened(backpackFormEntry));

        private void OnEnable()
        {
            if (backpackFormEntry == null)
            {
                Log.Error("[BackpackModule] backpackFormEntry 未接线——背包界面不可用。");
                return;
            }

            _toggleBinding = AppEntry.InputManager.Bind<MenuInputLayer>(
                AppEntry.GlobalInput.Gameplay.Backpack, OnToggle);
        }

        private void OnDisable()
        {
            _toggleBinding?.Dispose();
            _toggleBinding = null;

            // 卸载/禁用一律收口：关界面 + 释放暂停（Release 幂等，重复调用无害）
            Close();
            AppEntry.Time?.Release(PauseReason.Backpack);
        }

        /// <summary>开关（只在按下那一刻响应，避免 started/canceled 双触发）。</summary>
        private void OnToggle(InputAction.CallbackContext ctx)
        {
            if (ctx.phase != InputActionPhase.Performed || _opening)
            {
                return;
            }

            if (IsOpen)
            {
                Close();
                return;
            }

            OpenAsync().Forget();
        }

        /// <summary>
        /// 打开：装配数据源 → 开 Form → 暂停。任一步失败/取消都<b>保证不停留在暂停态</b>。
        /// </summary>
        private async UniTask OpenAsync()
        {
            if (!GameplaySessionEntry.TryGetInstance(out var entry) || entry.Context == null)
            {
                Log.Info("[BackpackModule] 尚无游戏会话——背包界面不打开。");
                return;
            }

            var bag = entry.Context.Inventory;
            if (bag == null)
            {
                Log.Info("[BackpackModule] 当前没有背包——按 B8 语义不打开、也不新建。");
                return;
            }

            _opening = true;
            try
            {
                await AppEntry.UIManager.OpenAsync(
                    backpackFormEntry,
                    new BackpackRuntimeContext(bag, entry.Context.Operations),
                    KitLifecycle.GlobalToken);

                // 先开界面、后暂停：暂停把 deltaTime 置 0，入场过渡（若配置）需要在暂停前跑完
                AppEntry.Time?.Request(PauseReason.Backpack);
            }
            catch (OperationCanceledException)
            {
                // 会话结束/取消——静默（表单打开被中止）
            }
            catch (Exception ex)
            {
                Log.Exception(ex); // async 方法里不能外抛（调用方已 Forget）
            }
            finally
            {
                _opening = false;

                // 不变量：暂停着就必须有界面。任何非成功路径都在这里把暂停释放掉（Release 幂等）
                if (!IsOpen)
                {
                    AppEntry.Time?.Release(PauseReason.Backpack);
                }
            }
        }

        /// <summary>关闭：先释放暂停（让出场过渡能动），再关 Form。</summary>
        private void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            AppEntry.Time?.Release(PauseReason.Backpack);
            AppEntry.UIManager.Close(backpackFormEntry);
        }
    }
}
