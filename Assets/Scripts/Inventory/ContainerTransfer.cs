using XeptGame.Items;
using XeptKit.Core;

namespace XeptGame.Inv
{
    /// <summary>
    /// 容器间转移哑原语（Equip_FPV_Design.md §2.1/§3.3，T1）：
    /// 源 <see cref="IItemContainer.TryRemove"/> → 目标 <see cref="IItemContainer.TryAdd"/>，失败回滚（原子）。
    /// <list type="bullet">
    /// <item><b>不携带任何意图</b>："转移后是否补位/批量续搬"是操作层（搬运会话）的编排，不在本原语
    /// （Equip_FPV_Design.md §2.4）；</item>
    /// <item>v1 容器均为全量成功/全量失败语义，回滚（源 TryAdd 原样退回）必然成功；
    /// ⏳ 网格/部分放入容器出现时需升级为"返回实际放入数"的端口（迟到项，见设计 §6）；</item>
    /// <item>源 = 目标（同一容器）视为无操作成功（防止自转引发重复事件）。</item>
    /// </list>
    /// </summary>
    public static class ContainerTransfer
    {
        public static bool Move(IItemContainer source, IItemContainer destination, ItemDefinition definition, int count)
        {
            Guard.NotNull(source, nameof(source));
            Guard.NotNull(destination, nameof(destination));
            Guard.NotNullObject(definition, nameof(definition));
            Guard.True(count > 0, "转移数量必须为正。");

            if (ReferenceEquals(source, destination))
            {
                return true;
            }

            if (!source.TryRemove(definition, count))
            {
                return false;
            }

            if (destination.TryAdd(definition, count))
            {
                return true;
            }

            // 目标拒绝 → 回滚：原样退回源（v1 容器全量语义下必然成功）
            source.TryAdd(definition, count);
            return false;
        }
    }
}
