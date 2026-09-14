namespace XeptGame.Interaction
{
    /// <summary>
    /// 运行时动作（v5，Interaction_Behaviour_Design.md §2 D4）：把**一件行为**绑到**一个目标**上，
    /// 交给宿主清单与执行器消费。每次出现一份，持有目标与当前状态，**不是资产、不共享**。
    /// <list type="bullet">
    /// <item><see cref="Slot"/> / <see cref="PromptKey"/> 直接转发行为——动作没有自己的标签字段，
    /// 因此"界面上显示的名字"与"实际做的事"不可能不同源；</item>
    /// <item>门控与执行转发给 <see cref="IInteractionBehaviour.CanAct"/> / <see cref="IInteractionBehaviour.Act"/>。</item>
    /// </list>
    /// 对照先例：UE5 Lyra 的 <c>FInteractionOption</c> = 目标 + 能力类引用；Valve 的 <c>Interactable</c> 只是标识符、
    /// 行为在独立组件里（见调研分册 01/03）。
    /// </summary>
    public sealed class BoundAction : IInteractionAction
    {
        private readonly IInteractionBehaviour _behaviour;
        private readonly IInteractionTarget _target;

        public BoundAction(IInteractionBehaviour behaviour, IInteractionTarget target)
        {
            _behaviour = behaviour;
            _target = target;
        }

        /// <summary>本动作对应的行为（只读；诊断/测试用）。</summary>
        public IInteractionBehaviour Behaviour => _behaviour;

        /// <summary>本动作作用的目标（只读；诊断/测试用）。</summary>
        public IInteractionTarget Target => _target;

        /// <inheritdoc />
        public InputSlot Slot => _behaviour.Slot;

        /// <inheritdoc />
        public string PromptKey => _behaviour.PromptKey;

        /// <inheritdoc />
        public bool CanInteract(InteractionContext context)
            => _behaviour != null && _target != null && _behaviour.CanAct(_target, context);

        /// <inheritdoc />
        public void Interact(InteractionContext context)
        {
            if (_behaviour == null || _target == null)
            {
                return;
            }

            _behaviour.Act(_target, context);
        }
    }
}
