using XeptGame.Container;
using XeptGame.Items.Operations;

namespace XeptGame.UI.Backpack
{
    /// <summary>
    /// 背包界面打开参数（Backpack_UI_Design.md B6；与 <c>PromptRuntimeContext</c> 同一形态）：
    /// 由 <see cref="BackpackModule"/> 在打开时装配，界面逻辑只认这两个口——
    /// <b>界面不认识 GameplaySession，也不认识世界</b>。
    /// <list type="bullet">
    /// <item><see cref="Store"/>：当前背包的容器（<c>GameplaySessionContext.Inventory</c>，
    /// <b>无包 = null</b> → 模块在打开前就拒绝，不会把 null 递进来）；</item>
    /// <item><see cref="Operations"/>：使用/丢弃两个请求的唯一入口（界面<b>不自己组合</b>
    /// "移除 + 落地 + 回滚"，也不自己发播报）。</item>
    /// </list>
    /// 世界掉落口不在这里：它是编排器内部的接线（<c>ItemOperationCoordinator._worldDrop</c>），
    /// 界面没必要、也不应该知道"东西会落到哪"。
    /// </summary>
    public sealed class BackpackRuntimeContext
    {
        public BackpackRuntimeContext(SlotStore store, ItemOperationCoordinator operations)
        {
            Store = store;
            Operations = operations;
        }

        /// <summary>当前背包容器（槽位面 = 界面唯一数据源）。</summary>
        public SlotStore Store { get; }

        /// <summary>操作编排器（使用 / 丢弃请求口）。</summary>
        public ItemOperationCoordinator Operations { get; }
    }
}
