namespace XeptGame.Interaction
{
    /// <summary>
    /// 可交互对象契约：可被玩家选中（继承 <see cref="ISelectable"/>）并可执行交互的世界目标。
    /// 探测循环每帧调用 <see cref="CanInteract"/> 判定交互有效性（冷却/锁定/耗尽等），
    /// 玩家按下交互键时调用 <see cref="Interact"/> 执行动作——"能否选中"由
    /// <see cref="ISelectable.CanSelect"/> 门控，"能否交互"由本接口门控，两阶段解耦：
    /// 对象可只选中（纯高亮）或选中 + 可交互。
    /// 实现方通常挂在带碰撞体的对象或其父级（探测经 GetComponentsInParent 匹配，
    /// 交互组件可挂在根、碰撞体在子物体）。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>当前是否可交互（每帧查询，供提示/输入消费；不得产生副作用）。</summary>
        bool CanInteract(InteractionContext context);

        /// <summary>执行交互（仅当 <see cref="CanSelect"/> 与 <see cref="CanInteract"/> 均通过后由输入触发）。</summary>
        void Interact(InteractionContext context);
    }
}
