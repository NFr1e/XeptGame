using System;
using System.Collections.Generic;
using XeptGame.Items;

namespace XeptGame.Inv
{
    /// <summary>
    /// 容器公共端口（Equip_FPV_Design.md §3.1 / Equip_FPV_Implement.md T1）——
    /// 占有 = 物品分布在容器中，容器间转移经 <see cref="ContainerTransfer.Move"/> 哑原语。
    /// <list type="bullet">
    /// <item><b>公共端口、不压平插入语义</b>：<see cref="TryAdd"/> 是否成功、按什么规则放入由各实现自定
    /// （背包 = 行聚合合并、恒成功；身体 = 单位制、空且接纳才成功；装备箱 = 网格/stackMax ⏳）——禁止用一个
    /// 统一 Insert 语义去压平不同容器形状；</item>
    /// <item><b>TryRemove 原子</b>：不足返回 false 且不改动（Move 依赖该原子性做回滚）；</item>
    /// <item><b>Changed 为容器级状态轨</b>：负载沿用 <see cref="InventoryChangeArgs"/> 形状
    /// （Item/Old/New，New=0 = 移除语义随容器定义）；每容器各自发布，无跨容器聚合（无"总拥有"概念）；</item>
    /// <item>纯 C# 可单测（唯一引擎耦合 = Definition 的 SO 引用）。</item>
    /// </list>
    /// </summary>
    public interface IItemContainer
    {
        /// <summary>当前内容只读视图（各容器自行定义行/槽语义）。</summary>
        IReadOnlyList<ItemStack> Stacks { get; }

        /// <summary>查询某定义的总数量（无 = 0）。</summary>
        int CountOf(ItemDefinition definition);

        /// <summary>是否持有某定义（数量 &gt; 0）。</summary>
        bool Contains(ItemDefinition definition);

        /// <summary>
        /// 尝试放入指定数量（原子：失败不改动）：插入语义随容器实现——背包行容器合并恒成功；
        /// 身体槽容器单位制（count 必须为 1 且存在空槽且接纳）才成功。
        /// </summary>
        bool TryAdd(ItemDefinition definition, int count);

        /// <summary>尝试移除指定数量（原子）：不足返回 false 且不改动。</summary>
        bool TryRemove(ItemDefinition definition, int count);

        /// <summary>容器级状态轨（细粒度推送；负载含前后数量）。</summary>
        event Action<InventoryChangeArgs> Changed;
    }
}
