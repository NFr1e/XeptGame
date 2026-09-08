namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互输入槽（语义槽，v3；Interaction_Prompt_V3_Design.md §2）：
    /// 动作声明自己要哪个槽，**不持有物理键**——物理键绑定集中在输入层映射
    /// （Primary=E（Gameplay.Interact）、Secondary=F（Gameplay.InteractSecondary）、
    /// Hold=E 长按——手势判别已激活，见 InteractionExecutor / Equip_FPV 决议 DP5）。
    /// </summary>
    public enum InputSlot
    {
        /// <summary>主交互（E 点按/默认按键；唯一动作的宿主即旧"按 E"行为）。</summary>
        Primary = 0,

        /// <summary>次交互（F 等；火堆"添柴"这类第二动作）。</summary>
        Secondary = 1,

        /// <summary>长按交互（本版 = 拾取"长按拿取"等超阈值动作；IHoldInteraction 进度通道仍预留）。</summary>
        Hold = 2,
    }
}
