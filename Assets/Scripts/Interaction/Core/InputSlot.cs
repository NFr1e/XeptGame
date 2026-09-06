namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互输入槽（语义槽，v3；Interaction_Prompt_V3_Design.md §2）：
    /// 动作声明自己要哪个槽，**不持有物理键**——物理键绑定集中在输入层映射
    /// （本版 Primary=E（Gameplay.Interact）、Secondary=F（Gameplay.InteractSecondary）；Hold 预留，执行通道后续）。
    /// </summary>
    public enum InputSlot
    {
        /// <summary>主交互（E/默认按键；唯一动作的宿主即旧"按 E"行为）。</summary>
        Primary = 0,

        /// <summary>次交互（F 等；火堆"添柴"这类第二动作）。</summary>
        Secondary = 1,

        /// <summary>长按交互（采集/撬锁进度；执行通道与 IHoldInteraction 后续实现，契约预留）。</summary>
        Hold = 2,
    }
}
