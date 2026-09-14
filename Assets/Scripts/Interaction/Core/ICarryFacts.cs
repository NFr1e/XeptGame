using System;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 携带事实（v5，Interaction_Behaviour_Design.md §2 D6）——**交互可用性所依据的"玩家身上带着什么"的现值 + 变化铃**。
    /// 名字刻意不含"交互"：它描述的是玩家状态，不是交互系统的私有物。
    /// <list type="bullet">
    /// <item><b>现值</b>：<see cref="HasBag"/>（有没有背包；字段按需生长，当前只有这一项）；</item>
    /// <item><b>变化铃</b>：<see cref="Changed"/>——背槽变化时发一次，宿主据此重建动作清单（成员收缩/生长）；</item>
    /// <item><b>实现方</b>：会话域（一轮会话的业务状态容器的持有者）；<b>消费方</b>：交互宿主；</item>
    /// <item><b>可测性</b>：测试注入假实现即可覆盖"有包/无包 × 各类物品"的组合，不需真会话。</item>
    /// </list>
    /// </summary>
    public interface ICarryFacts
    {
        /// <summary>玩家当前是否有背包（决定"拾取"这类需要去处的动作是否出现在清单里）。</summary>
        bool HasBag { get; }

        /// <summary>携带事实变化（背槽变化 → 一次）。宿主订阅后重建清单并通知。</summary>
        event Action Changed;
    }
}
