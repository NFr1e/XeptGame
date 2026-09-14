namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互文案键常量（v5，Interaction_Behaviour_Design.md §2 D3）：
    /// 代码只持有**键**，句子本体在 <c>Assets/GameData/Localization/Items.zh-CN-en.csv</c>。
    /// 键命名沿用点分小写约定；取值为**完整句子**（含数值时用 <c>{0}</c> 占位符），
    /// 禁止把"动词 + 对象名"拼成一句（Epic 官方本地化规范明列的反模式，见调研分册 03）。
    /// </summary>
    public static class InteractionPromptKeys
    {
        /// <summary>拾取（Primary，装入当前背包）。</summary>
        public const string Pickup = "interaction.pickup";

        /// <summary>装备（Hold，可持物拿到手上；无包时也出现——不经背包）。</summary>
        public const string Equip = "interaction.equip";

        /// <summary>背上（Hold，容器/背包背到背槽；无包时也出现——它就是"从无包变有包"的路）。</summary>
        public const string Wear = "interaction.wear";

        /// <summary>点燃（Primary）。</summary>
        public const string Ignite = "interaction.ignite";

        /// <summary>熄灭（Primary）。</summary>
        public const string Extinguish = "interaction.extinguish";

        /// <summary>添柴（Secondary；句子含 {0}/{1} 当前燃料与上限）。</summary>
        public const string AddFuel = "interaction.addfuel";

        /// <summary>交互（调试对照物，Primary）。</summary>
        public const string Debug = "interaction.debug";
    }
}
