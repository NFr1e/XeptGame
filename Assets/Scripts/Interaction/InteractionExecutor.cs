using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using XeptKit.Core;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互执行器（v3.1，引擎壳 MonoBehaviour，实现 <see cref="IInteractionExecutor"/>）：
    /// 持有当前**宿主 + 动作集**，按**输入槽**分派执行。
    /// <list type="bullet">
    /// <item><b>宿主推送</b>：<see cref="ApplyHost"/> 由选中系统在选中变化时调用（单写者纪律，含 null）；</item>
    /// <item><b>动作集权威 = 宿主</b>：宿主实现 <see cref="IInteractionActionsHost"/>（Actions 列表 + ActionsChanged
    /// 事件）——执行器读列表快照并订阅成员事件（不再组件扫描/enable 过滤）；纯 C# 动作由宿主管辖；</item>
    /// <item><b>槽输入绑定</b>：Primary = Gameplay.Interact（E）、Secondary = Gameplay.InteractSecondary（F），
    /// 均以 GameplayInputLayer 绑定；</item>
    /// <item><b>分派</b>：槽输入 performed → 快照中找该槽且 CanInteract 的动作 → Interact →
    /// 刷新快照（宿主在动作执行中可能已改成员并触发事件，双保险幂等）；</item>
    /// <item><b>同槽互斥</b>：一个槽取首个可用动作（多可用需菜单，决议 §2.2 禁止）。</item>
    /// </list>
    /// </summary>
    public sealed class InteractionExecutor : MonoBehaviour, IInteractionExecutor
    {
        private readonly CompositeDisposable _disposables = new();

        private ISelectable _host;
        private IInteractionActionsHost _hostActions;
        private IInteractionAction[] _actions = Array.Empty<IInteractionAction>();

        /// <inheritdoc />
        public ISelectable CurrentHost => _host;

        /// <inheritdoc />
        public IReadOnlyList<IInteractionAction> CurrentActions => _actions;

        /// <inheritdoc />
        public event Action<InteractionHostChangedArgs> HostChanged;

        /// <summary>
        /// 选中系统推送宿主（含 null）：切换动作集权威并重读快照，变化时发布 <see cref="HostChanged"/>。
        /// </summary>
        public void ApplyHost(ISelectable host)
        {
            if (_hostActions != null)
            {
                _hostActions.ActionsChanged -= OnHostActionsChanged;
            }

            _host = host;
            _hostActions = host as IInteractionActionsHost;

            if (_hostActions != null)
            {
                _hostActions.ActionsChanged += OnHostActionsChanged;
            }

            RefreshAndNotify();
        }

        private void OnHostActionsChanged()
        {
            RefreshAndNotify();
        }

        private void OnEnable()
        {
            if (AppEntry.GlobalInput == null)
            {
                return;
            }

            _disposables.Add(AppEntry.InputManager.Bind<GameplayInputLayer>(
                AppEntry.GlobalInput.Gameplay.Interact,
                ctx => OnSlotInput(InputSlot.Primary, ctx)));
            _disposables.Add(AppEntry.InputManager.Bind<GameplayInputLayer>(
                AppEntry.GlobalInput.Gameplay.InteractSecondary,
                ctx => OnSlotInput(InputSlot.Secondary, ctx)));
        }

        private void OnDisable()
        {
            _disposables.Dispose();
        }

        private void OnSlotInput(InputSlot slot, InputAction.CallbackContext ctx)
        {
            if (!ctx.performed)
            {
                return;
            }

            var action = FindAvailable(slot);
            if (action == null)
            {
                return;
            }

            action.Interact(new InteractionContext(transform));
            Log.Info($"[InteractionExecutor] 执行动作 {action.GetType().Name}（槽 {slot}）");

            // 动作执行可能改宿主成员（宿主已发 ActionsChanged）——快照刷新兜底（比较幂等，防重复事件）
            RefreshAndNotify();
        }

        private IInteractionAction FindAvailable(InputSlot slot)
        {
            var ctx = new InteractionContext(transform);
            for (int i = 0; i < _actions.Length; i++)
            {
                var action = _actions[i];
                if (action.Slot == slot && action.CanInteract(ctx))
                {
                    return action;
                }
            }

            return null;
        }

        private void RefreshAndNotify()
        {
            IInteractionAction[] next = _hostActions != null && _hostActions.Actions != null
                ? Copy(_hostActions.Actions)
                : Array.Empty<IInteractionAction>();

            if (SequenceEquals(_actions, next))
            {
                return;
            }

            _actions = next;
            HostChanged?.Invoke(new InteractionHostChangedArgs(_host, _actions));
        }

        private static IInteractionAction[] Copy(IReadOnlyList<IInteractionAction> source)
        {
            var array = new IInteractionAction[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                array[i] = source[i];
            }

            return array;
        }

        private static bool SequenceEquals(IInteractionAction[] current, IInteractionAction[] next)
        {
            if (current.Length != next.Length)
            {
                return false;
            }

            for (int i = 0; i < current.Length; i++)
            {
                if (!ReferenceEquals(current[i], next[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
