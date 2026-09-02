using System;
using UnityEngine;
using UnityEngine.InputSystem;
using XeptKit.Core;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互执行器（执行系统，引擎壳 MonoBehaviour，实现 <see cref="IInteractionExecutor"/>，契约模型 v2）：
    /// 持有当前可交互者（由选中系统**唯一推送**，单写者纪律），执行交互动作。
    /// <list type="bullet">
    /// <item><b>单一契约消费</b>：本组件是 <see cref="IInteractable"/>（交互能力）的唯一消费者——
    /// 从 <see cref="CurrentInteractable"/> 取交互入口；选中检测（<see cref="ISelectable"/>）不在此组件
    /// （见 <c>InteractionDetector</c>）；</item>
    /// <item><b>被推而非拉取（v2）</b>：不序列化目标源、不消费目标源接口——CurrentInteractable 由
    /// Detector 在目标变化时直接推入（含 null）；执行器不感知目标从哪来（探测/选中是选中系统职责）；</item>
    /// <item><b>输入层阻断自动生效</b>：经 <see cref="AppEntry.InputManager"/> 以 <c>GameplayInputLayer</c>
    /// 绑定，菜单打开（层失活）时回调被 InputManager 直接跳过，交互输入与移动输入同步失效；</item>
    /// <item><b>职责</b>：绑定 Interact 输入（Gameplay map）、交互门控（按下时重判
    /// <see cref="IInteractable.CanInteract"/>，状态帧间可能变化）、执行 <see cref="IInteractable.Interact"/>
    /// 并发布 <see cref="Interacted"/>。</item>
    /// </list>
    /// </summary>
    public sealed class InteractionExecutor : MonoBehaviour, IInteractionExecutor
    {
        private IInteractable _currentInteractable;
        private readonly CompositeDisposable _disposables = new();

        /// <inheritdoc cref="IInteractionExecutor.CurrentInteractable"/>
        /// <summary>当前可交互者（选中系统唯一推，含 null）。设置时发布 <see cref="InteractableChanged"/>。</summary>
        public IInteractable CurrentInteractable
        {
            get => _currentInteractable;
            set
            {
                if (_currentInteractable == value)
                {
                    return;
                }

                var previous = _currentInteractable;
                _currentInteractable = value;
                InteractableChanged?.Invoke(new InteractableChangeArgs(value, previous));
            }
        }

        /// <inheritdoc cref="IInteractionExecutor.InteractableChanged"/>
        /// <summary>交互者变化事件（负载含旧/新交互者；提示层/手持物 HUD 消费）。</summary>
        public event Action<InteractableChangeArgs> InteractableChanged;

        /// <summary>交互执行事件（参数 = 被交互对象）。</summary>
        public event Action<IInteractable> Interacted;

        private void OnEnable()
        {
            if (AppEntry.GlobalInput == null)
            {
                return;
            }

            _disposables.Add(AppEntry.InputManager.Bind<GameplayInputLayer>(
                AppEntry.GlobalInput.Gameplay.Interact, OnInteract));
        }

        private void OnDisable()
        {
            _disposables.Dispose();
        }

        private void OnInteract(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed)
            {
                return;
            }

            // 按下时重判：目标/交互状态在帧间可能变化（冷却/锁定/耗尽/移开）
            var interactable = _currentInteractable;
            if (interactable == null || !interactable.CanInteract(new InteractionContext(transform)))
            {
                return;
            }

            interactable.Interact(new InteractionContext(transform));

            Log.Info($"[InteractionExecutor] 交互执行：{GetInteractableName(interactable)}");
            Interacted?.Invoke(interactable);
        }

        private static string GetInteractableName(IInteractable interactable)
            => interactable is Component component ? component.name : interactable.GetType().Name;
    }
}
