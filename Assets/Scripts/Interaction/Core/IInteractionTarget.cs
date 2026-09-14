namespace XeptGame.Interaction
{
    /// <summary>
    /// 被交互对象（**目标**）标记接口（v5，Interaction_Behaviour_Design.md §2 D1）：
    /// 世界里"能被玩家做点什么"的那个对象（掉落物视图、火堆、调试物…）实现本接口，
    /// 交互系统只认这个标记，**不认识物品**（不引用 XeptGame.Items——这是纪律，见 §4 不变量 1）。
    /// <para>
    /// 行为（<see cref="IInteractionBehaviour"/>）需要目标提供具体能力时，由目标另行实现**小端口接口**
    /// （如"能被拾取""能被点燃"），行为内部按需转换；本接口不承载任何成员，避免变成杂物筐。
    /// </para>
    /// </summary>
    public interface IInteractionTarget
    {
    }
}
