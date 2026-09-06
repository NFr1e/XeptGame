namespace XeptGame.Interaction
{
    /// <summary>
    /// 目标性契约（可选）：对象可被交互器瞄准/选中。
    /// 这是"选中"的完整概念，承载三个面、两类消费方：
    /// <list type="bullet">
    /// <item><see cref="CanSelect"/>：**拉取式资格查询**，交互逻辑每帧调用（交互器消费）——
    /// 决定对象当前能否成为目标（高亮/提示的数据来源）；</item>
    /// <item><see cref="OnSelected"/> / <see cref="OnDeselected"/>：**推式状态事件**，
    /// 目标变化时交互器推送（视觉层消费）——驱动高亮等表现；</item>
    /// <item><see cref="IsSelected"/>：**状态查询**（业务消费）——初始化效果、
    /// 运行时补挂组件、其他系统同步查询；由实现方在状态事件中维护。</item>
    /// </list>
    /// 与动作（<see cref="IInteractionAction"/>）的关系：**宿主与动作解耦**——本接口是"宿主"身份
    /// （被选中节点即宿主）；需要"只选中不交互"（纯高亮/观察反馈）的对象只实现本接口即可；
    /// 可交互对象的动作组件（每动作一个 IInteractionAction 组件）可同挂或上挂于宿主链，
    /// 由执行器在宿主推送时收集（v3，见 Interaction_Prompt_V3_Design.md）。
    /// </summary>
    public interface ISelectable
    {
        /// <summary>
        /// 当前是否被驱动交互器选中（状态查询，外部只读）。
        /// 由实现方在 <see cref="OnSelected"/> / <see cref="OnDeselected"/> 中维护。
        /// <para>语义边界：单驱动源（当前架构）下即"被选中"；多交互器场景
        /// （AI 同伴/多人）为"任一交互器选中"的聚合，其正确性需中央协调器记账，
        /// 属已知边界（当前无此设施）。</para>
        /// </summary>
        bool IsSelected { get; }

        /// <summary>当前能否成为目标（每帧被交互器查询；不得产生副作用）。</summary>
        bool CanSelect(InteractionContext context);

        /// <summary>成为目标（交互器瞄准且 <see cref="CanSelect"/> 通过）。</summary>
        void OnSelected(InteractionContext context);

        /// <summary>不再是目标（移开 / 失效 / 销毁）。无参数：退场不需要交互者信息。</summary>
        void OnDeselected();
    }
}
