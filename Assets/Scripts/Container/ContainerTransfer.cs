using XeptGame.Items;
using XeptKit.Core;

namespace XeptGame.Container
{
    /// <summary>
    /// 容器间转移哑原语（机制层；Equip_FPV_Design.md §2.1/§3.3，T1）：
    /// 源 <see cref="IItemContainer.TryRemove"/> → 目标 <see cref="IItemContainer.TryAdd"/>，失败回滚（原子）。
    /// <list type="bullet">
    /// <item><b>不携带任何意图</b>："转移后是否补位/批量续搬"是操作层的编排，不在本原语；</item>
    /// <item>v1 容器均为全量成功/全量失败语义，回滚（源 TryAdd 原样退回）必然成功；
    /// ⏳ 容量约束真正生效（部分放入）时需升级为"返回实际放入数"的端口（迟到项，SlotStore_Design.md §5）；</item>
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
            if (!source.TryAdd(definition, count))
            {
                throw new System.InvalidOperationException("容器转移回滚失败，必须停止后续操作并检查占用事实。");
            }

            return false;
        }

        /// <summary>
        /// 定向转移：把物品放进目标容器的<b>指定槽</b>（归位用，SlotStore_Design.md §6 归位语义）。
        /// 语义与 <see cref="Move"/> 同规格：目标拒绝则回滚源；只有槽容器具备定向放入能力。
        /// </summary>
        public static bool MoveAt(IItemContainer source, SlotContainer destination, ItemDefinition definition, int count, SlotId cell)
        {
            Guard.NotNull(source, nameof(source));
            Guard.NotNull(destination, nameof(destination));
            Guard.NotNullObject(definition, nameof(definition));
            Guard.True(count > 0, "转移数量必须为正。");

            if (!source.TryRemove(definition, count))
            {
                return false;
            }

            if (destination.TryPlaceAt(cell, definition, count))
            {
                return true;
            }

            if (!source.TryAdd(definition, count))
            {
                throw new System.InvalidOperationException("容器定向转移回滚失败，必须停止后续操作并检查占用事实。");
            }

            return false;
        }

        /// <summary>
        /// 按格转移一个<b>实例</b>（有状态载荷；Item_Instance_Design.md §2.3）：源格取出 → 目标格放入，失败回滚源格。
        /// <list type="bullet">
        /// <item>语义与 <see cref="Move"/> 同规格（原子、不携带意图、目标拒绝则回滚）；</item>
        /// <item><b>不是复制</b>：搬的是同一个实例对象（身份不变），"一个实例同一时刻只有一个位置"（不变量 I1）不破；</item>
        /// <item>源与目标是同一格 = 无操作成功；同一容器内换格（不同格）是正常搬运。</item>
        /// </list>
        /// </summary>
        public static bool MoveInstance(SlotContainer source, SlotId sourceCell, SlotContainer destination, SlotId destinationCell)
        {
            Guard.NotNull(source, nameof(source));
            Guard.NotNull(destination, nameof(destination));

            if (ReferenceEquals(source, destination) && sourceCell == destinationCell)
            {
                return true;
            }

            if (!source.TryTakeInstanceAt(sourceCell, out var instance))
            {
                return false;
            }

            if (destination.TryPlaceInstanceAt(destinationCell, instance))
            {
                return true;
            }

            // 目标拒绝 → 回滚：原样退回源格（此刻源格为空，必然成功）
            if (!source.TryPlaceInstanceAt(sourceCell, instance))
            {
                throw new System.InvalidOperationException("实例转移回滚失败，必须停止后续操作并检查占用事实。");
            }

            return false;
        }
    }
}
