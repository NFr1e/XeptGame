using XeptGame.Inv;

namespace XeptGame.Game
{
    /// <summary>
    /// 一轮游戏会话的业务状态容器（纯数据，无逻辑；ItemLoop_Design.md §2）——
    /// 与 GameplayEntry 同生共死（GameplayLoadState 创建 / GameplayUnloadState 弃），
    /// 生命周期由 <c>GameplayEntry.Instantiate()/Dispose()</c> 约束。
    /// <list type="bullet">
    /// <item><b>装"玩家拥有什么"</b>（Inventory → 未来 Equip/局内进度）；不装服务/编排（那是 GameContext/域级）；</item>
    /// <item><b>跨关卡保留</b>：LevelLoad/Unload 不碰本对象；错误 RetryGame（域重建）与回菜单再开新局随之一并弃；</item>
    /// <item>场景模块（拾取壳/背包 UI）经显式装配或 GameplayEntry.Instance.Session 读取。</item>
    /// </list>
    /// </summary>
    public sealed class GameplaySession
    {
        /// <summary>背包容器（一轮级，起步字段；后续 Equip/局内进度按需生长）。</summary>
        public Inventory Inventory { get; } = new();
    }
}
