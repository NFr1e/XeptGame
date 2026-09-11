using XeptGame.Inv;

namespace XeptGame.Items.Operations
{
    /// <summary>
    /// 背包去向端口（Item_Instance_Design.md §4）：换包时"旧包去哪"的抽象。
    /// <list type="bullet">
    /// <item><b>整包接收</b>：接收的是同一个 <see cref="ContainerInstance"/>（内容不动、不拆不丢）——
    /// 实现方只需决定"把它安置在哪"（世界地面 / 营地仓库 / 脚本没收…）并保证接收后它是可寻回的；</item>
    /// <item><b>调用时机由编排器决定</b>：换包顺序是"先摘旧 → 放新 → <b>最后</b>交接旧包"，
    /// 因此本方法返回 false 时不会留下半换状态（编排器把新包摘回、旧包放回背槽）；</item>
    /// <item><b>当前实现</b>：⏳ T5 的 <c>WorldDropDestination</c>（落世界记录）。在此之前换下非空旧包会被
    /// 编排器直接拒绝（DP6：不静默丢包）。</item>
    /// </list>
    /// </summary>
    public interface ICarrierDestination
    {
        /// <summary>接收一个被换下的背包实例；失败时经 <paramref name="reason"/> 给出类型化原因（供回执）。</summary>
        bool TryAccept(ContainerInstance carrier, out string reason);
    }
}
