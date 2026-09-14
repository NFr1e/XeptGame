namespace XeptGame.UI
{
    /// <summary>
    /// 交互动作文案解析（v5，Interaction_Behaviour_Design.md §2 D3）：行为只给**键**，文本从本地化表取。
    /// 取词与回退逻辑已上收为通用件 <see cref="LocalizedText"/>（背包界面 B5 落地时的第二个消费者），
    /// 本类保留为**交互域的语义入口**——调用方读这里的名字就知道"这是交互文案"，不必知道底层表。
    /// 未命中回退为键本身、告警按 key 去重等纪律见 <see cref="LocalizedText"/>。
    /// </summary>
    public static class InteractionPromptText
    {
        /// <summary>按键解析文案；<paramref name="promptKey"/> 为空 → 返回空串（消费方隐藏该行）。</summary>
        public static string Resolve(string promptKey) => LocalizedText.Resolve(promptKey);
    }
}
