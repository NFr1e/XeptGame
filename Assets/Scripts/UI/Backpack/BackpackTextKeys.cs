namespace XeptGame.UI.Backpack
{
    /// <summary>
    /// 背包界面文案键（Backpack_UI_Design.md B5）：<b>代码只存键，句子在 CSV</b>
    /// （<c>Assets/GameData/Localization/Items.zh-CN-en.csv</c>）。
    /// 纪律（沿 <c>Interaction_Behaviour_Design.md</c> D3）：<b>存完整句子、允许 <c>{0}</c> 占位符、
    /// 禁止"动词 + 名词"拼接</b>——中文与英文的语序不同，拼接必然在某一侧读起来是机翻。
    /// 新增键后必须同步 CSV；<c>BackpackTextKeyTests</c> 会双向断言，漏一侧即红。
    /// </summary>
    public static class BackpackTextKeys
    {
        /// <summary>界面标题。</summary>
        public const string Title = "backpack.title";

        /// <summary>容量：已用格 / 总格（实参：已用、总数）。</summary>
        public const string Capacity = "backpack.capacity";

        /// <summary>格内数量角标（实参：数量）。</summary>
        public const string CellCount = "backpack.cell.count";

        /// <summary>详情区的数量行（实参：数量）。</summary>
        public const string DetailCount = "backpack.detail.count";

        /// <summary>详情区的说明行（实参：每格上限；未约束时用 <see cref="DetailNoStackLimit"/>）。</summary>
        public const string DetailStackLimit = "backpack.detail.stacklimit";

        /// <summary>详情区：该物品不限制每格上限。</summary>
        public const string DetailNoStackLimit = "backpack.detail.nolimit";

        /// <summary>详情区：选了空格 / 未选中。</summary>
        public const string DetailNone = "backpack.detail.none";

        /// <summary>动作按钮：使用。</summary>
        public const string ActionUse = "backpack.action.use";

        /// <summary>动作按钮：丢弃。</summary>
        public const string ActionDrop = "backpack.action.drop";

        /// <summary>动作按钮：使用（不可用时的悬停说明，实参：物品名）。</summary>
        public const string ActionUseDisabled = "backpack.action.use.disabled";
    }
}
