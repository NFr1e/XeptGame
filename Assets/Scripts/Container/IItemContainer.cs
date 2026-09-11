using System;
using System.Collections.Generic;
using XeptGame.Items;

namespace XeptGame.Container
{
    /// <summary>
    /// 容器公共端口（机制层，Container 层；Equip_FPV_Design.md §3.1 / SlotStore_Design.md §1）——
    /// 占有 = 物品分布在容器中，容器间转移经 <see cref="ContainerTransfer.Move"/> 哑原语。
    /// <list type="bullet">
    /// <item><b>面向"物品"的端口</b>（对比：槽位面面向"格"）——编排器/世界/身体/背包都以本端口互相认识；</item>
    /// <item><b>公共端口、不压平插入语义</b>：<see cref="TryAdd"/> 是否成功、按什么规则放入由各实现自定
    /// （背包 = 行聚合合并；身体 = 单位制、空且接纳才成功；槽容器 = 容量与分配规则）——禁止用一个
    /// 统一 Insert 语义去压平不同容器形状；</item>
    /// <item><b>TryRemove 原子</b>：不足返回 false 且不改动（Move 依赖该原子性做回滚）；
    /// <b>TryAdd 全量</b>：装不下整批拒绝（"部分接受"是容量生效时的迟到项，见 SlotStore_Design.md §5）；</item>
    /// <item><b>Changed 为容器级状态轨</b>：负载 <see cref="ContainerChangeArgs"/>（Item/Old/New，New=0 = 移除语义
    /// 随容器定义）；每容器各自发布，无跨容器聚合（无"总拥有"概念）；</item>
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
        /// 尝试放入指定数量（全量：放不下则整批失败且不改动）：插入语义随容器实现——
        /// 行容器合并；身体槽单位制（count 必须为 1 且存在接纳该物的空槽）；槽容器按容量与槽序分配。
        /// </summary>
        bool TryAdd(ItemDefinition definition, int count);

        /// <summary>尝试移除指定数量（原子）：不足返回 false 且不改动。</summary>
        bool TryRemove(ItemDefinition definition, int count);

        /// <summary>容器级状态轨（细粒度推送；负载含前后数量）。</summary>
        event Action<ContainerChangeArgs> Changed;
    }
}
