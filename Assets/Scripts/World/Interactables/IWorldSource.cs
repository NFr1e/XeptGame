using XeptGame.Container;
using XeptGame.Items;

namespace XeptGame.World
{
    /// <summary>
    /// 世界掉落物的数据面（Item_Instance_Design.md §6）——<b>视图只认它</b>。
    /// 在容器端口（<see cref="IItemContainer"/>）之上补三件事：
    /// <list type="bullet">
    /// <item><b>记录身份</b> <see cref="RecordId"/>：拾取成功后由协调器请求记录层删除（视图不自行删记录，不变量 I3）；</item>
    /// <item><b>短期占用</b>：一次交互期间标记忙碌，不接纳第二笔操作（占用不改变归属）；</item>
    /// <item><b>场景可用性</b>：宿主卸载/失活后不得提交，未提交的拾取按失败处理。</item>
    /// </list>
    /// 两种实现：<see cref="WorldStackSource"/>（无状态堆叠：定义 + 数量）与
    /// <see cref="WorldInstanceSource"/>（有状态实例：背包等，见 <see cref="IWorldCarrierSource"/>）。
    /// </summary>
    public interface IWorldSource : IItemContainer
    {
        /// <summary>对应世界记录 id（0 = 无记录：场景直摆且尚未登记）。</summary>
        long RecordId { get; }

        /// <summary>宿主仍存在且允许提交；不可用时尚未提交的拾取应失败。</summary>
        bool Available { get; }

        /// <summary>是否已被某次操作占用（不做第二笔）。</summary>
        bool IsBusy { get; }

        /// <summary>是否还有内容（无内容 ⇒ 视图可隐藏/回收）。</summary>
        bool HasContent { get; }

        /// <summary>提示/图标用的展示定义（堆叠 = 物品定义；实例 = 实例的定义）。</summary>
        ItemDefinition DisplayDefinition { get; }

        /// <summary>为单次操作标记忙碌，不改变归属。</summary>
        bool TryAcquire(long operationId);

        /// <summary>仅释放匹配操作的标记；宿主不可用时也必须可调用。</summary>
        void Release(long operationId);

        /// <summary>同步提交批结束后的场景反馈，不在移除/回滚中间刷新。</summary>
        void RefreshView();
    }
}
