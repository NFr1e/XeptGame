using System.Collections.Generic;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互宿主变化负载（只读结构体，v3；Interaction_Prompt_V3_Design.md §2）：
    /// 携带当前宿主与其动作集（含 null = 清除）——提示/标记等消费者直接消费清单，
    /// 免反向解析（镜像 v2 负载携带消费形状的先例）。
    /// </summary>
    public readonly struct InteractionHostChangedArgs
    {
        /// <summary>当前宿主（null = 无选中/已清除）。</summary>
        public readonly ISelectable Host;

        /// <summary>宿主当前动作集（槽序排序；宿主为 null 时空集）。</summary>
        public readonly IReadOnlyList<IInteractionAction> Actions;

        /// <summary>是否存在宿主。</summary>
        public bool HasHost => Host != null;

        public InteractionHostChangedArgs(ISelectable host, IReadOnlyList<IInteractionAction> actions)
        {
            Host = host;
            Actions = actions;
        }
    }
}
