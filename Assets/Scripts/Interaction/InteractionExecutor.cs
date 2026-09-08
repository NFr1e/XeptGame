using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using XeptGame.Core;
using XeptKit.Core;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互执行器（v3.1+，引擎壳 MonoBehaviour，实现 <see cref="IInteractionExecutor"/>）：
    /// 持有当前**宿主 + 动作集**，按**输入槽**分派执行。
    /// <list type="bullet">
    /// <item><b>宿主推送</b>：<see cref="ApplyHost"/> 由选中系统在选中变化时调用（单写者纪律，含 null）；</item>
    /// <item><b>动作集权威 = 宿主</b>：宿主实现 <see cref="IInteractionActionsHost"/>（Actions 列表 + ActionsChanged
    /// 事件）——执行器读列表快照并订阅成员事件（不再组件扫描/enable 过滤）；纯 C# 动作由宿主管辖；</item>
    /// <item><b>E 手势判别（tap/hold，激活 v3 预留 Hold 通道，Equip_FPV 决议 DP5）</b>：E = Primary 与 Hold
    /// 两个语义槽共用同一物理动作——按下（started）计时，阈值内松开（canceled）= Primary 分派（tap）；
    /// 按住超过 <see cref="XeptGameConsts.Interaction.HoldPressThresholdSeconds"/> 且宿主有可用 Hold 动作
    /// = Hold 分派（tap 取消）；同一按下只产生一种结果；宿主无 Hold 动作时退化为纯"按 E"语义（回归零影响）；</item>
    /// <item><b>槽输入绑定</b>：Primary/Hold = Gameplay.Interact（E，手势判别）、Secondary =
    /// Gameplay.InteractSecondary（F），均以 GameplayInputLayer 绑定；</item>
    /// <item><b>同槽互斥</b>：一个槽取首个可用动作（多可用需菜单，决议 §2.2 禁止）。</item>
    /// </list>
    /// 计时器绑定 <see cref="KitLifecycle.GlobalToken"/>（会话终止即取消）；OnDisable 释放全部。
    /// </summary>
    public sealed class InteractionExecutor : MonoBehaviour, IInteractionExecutor
    {
        private readonly CompositeDisposable _disposables = new();

        private ISelectable _host;
        private IInteractionActionsHost _hostActions;
        private IInteractionActionsNotifier _hostNotifier; // 可选：仅宿主实现时订阅
        private IInteractionAction[] _actions = Array.Empty<IInteractionAction>();

        // ---- E 手势状态（tap/hold 判别）----
        private bool _pressActive;            // 按下进行中
        private bool _pressResolved;          // 本次按下已分派（tap 或 hold），防重复
        private float _pressStartTime;        // 按下时刻（Time.unscaledTime）
        private CancellationTokenSource _holdCts; // 阈值计时器（按次新建）

        /// <inheritdoc />
        public ISelectable CurrentHost => _host;

        /// <inheritdoc />
        public IReadOnlyList<IInteractionAction> CurrentActions => _actions;

        /// <inheritdoc />
        public event Action<InteractionHostChangedArgs> HostChanged;

        /// <summary>
        /// 选中系统推送宿主（含 null）：切换动作集权威并重读快照，变化时发布 <see cref="HostChanged"/>。
        /// 动作集变化通知为可选能力：仅在宿主实现 <see cref="IInteractionActionsNotifier"/> 时订阅。
        /// </summary>
        public void ApplyHost(ISelectable host)
        {
            if (_hostNotifier != null)
            {
                _hostNotifier.ActionsChanged -= OnHostActionsChanged;
                _hostNotifier = null;
            }

            _host = host;
            _hostActions = host as IInteractionActionsHost;

            if (host is IInteractionActionsNotifier notifier)
            {
                _hostNotifier = notifier;
                _hostNotifier.ActionsChanged += OnHostActionsChanged;
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

            // E：tap/hold 同源——started = 按下、canceled = 松开；performed 忽略（与 started 同帧双发，防误判）
            _disposables.Add(AppEntry.InputManager.Bind<GameplayInputLayer>(
                AppEntry.GlobalInput.Gameplay.Interact,
                OnInteractInput));
            _disposables.Add(AppEntry.InputManager.Bind<GameplayInputLayer>(
                AppEntry.GlobalInput.Gameplay.InteractSecondary,
                ctx => OnSlotInput(InputSlot.Secondary, ctx)));
        }

        private void OnDisable()
        {
            CancelPress();
            _disposables.Dispose();

            if (_hostNotifier != null)
            {
                _hostNotifier.ActionsChanged -= OnHostActionsChanged;
                _hostNotifier = null;
            }
        }

        private void OnInteractInput(InputAction.CallbackContext ctx)
        {
            switch (ctx.phase)
            {
                case InputActionPhase.Started:
                    BeginPress();
                    break;

                case InputActionPhase.Canceled:
                    EndPress();
                    break;
            }
        }

        private void BeginPress()
        {
            if (_pressActive)
            {
                return; // 防同帧/连点重复进入
            }

            _pressActive = true;
            _pressResolved = false;
            _pressStartTime = Time.unscaledTime;

            _holdCts = CancellationTokenSource.CreateLinkedTokenSource(KitLifecycle.GlobalToken);
            RunHoldTimer(_holdCts.Token).Forget();
        }

        /// <summary>阈值计时：按住超过阈值 → 升级为 Hold 分派（无可用 Hold 动作则维持等待松开走 tap）。</summary>
        private async UniTaskVoid RunHoldTimer(CancellationToken token)
        {
            try
            {
                await UniTask.WaitUntil(
                    () => !_pressActive || _pressResolved
                          || Time.unscaledTime - _pressStartTime >= XeptGameConsts.Interaction.HoldPressThresholdSeconds,
                    PlayerLoopTiming.Update,
                    token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!_pressActive || _pressResolved)
            {
                return; // 已松开或已分派
            }

            var hold = FindAvailable(InputSlot.Hold);
            if (hold == null)
            {
                return; // 宿主无可用 Hold 动作：维持等待，松开时走 tap（纯"按 E"宿主零影响）
            }

            _pressResolved = true;
            Execute(hold, InputSlot.Hold);
        }

        private void EndPress()
        {
            if (!_pressActive)
            {
                return;
            }

            _pressActive = false;
            if (_holdCts != null)
            {
                _holdCts.Cancel();
                _holdCts.Dispose();
                _holdCts = null;
            }

            if (_pressResolved)
            {
                return; // 已被长按占用，不再 tap
            }

            var tap = FindAvailable(InputSlot.Primary);
            if (tap == null)
            {
                return;
            }

            _pressResolved = true;
            Execute(tap, InputSlot.Primary);
        }

        private void CancelPress()
        {
            _pressActive = false;
            if (_holdCts != null)
            {
                _holdCts.Cancel();
                _holdCts.Dispose();
                _holdCts = null;
            }
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

            Execute(action, slot);
        }

        private void Execute(IInteractionAction action, InputSlot slot)
        {
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
