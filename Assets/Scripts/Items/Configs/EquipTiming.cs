using System;
using UnityEngine;

namespace XeptGame.Items
{
    /// <summary>
    /// 可持物的拿放时长参数（内容侧，由 <see cref="HoldableFacetProfile"/> 持有；装备行为收解析后的值、不读配置）。
    /// <list type="bullet">
    /// <item>纯数值对；负 / NaN 非法（行为在 Reconcile 时校验抛错），<b>0 合法</b>（零时长 = 受控更新内立即收敛）；</item>
    /// <item><b>全 0 = 未配置</b>（旧资产反序列化 / 作者留空），由配置读取方
    /// （<see cref="HoldableFacetProfile.ResolvedTiming"/>）回退 <see cref="Default"/>；测试可直接构造显式值；</item>
    /// <item>物品差异（斧慢/石快）经不同 <see cref="HoldableFacetProfile"/> 共享或区分表达——时长随占用携带，行为无全局时长概念。</item>
    /// </list>
    /// </summary>
    [Serializable]
    public struct EquipTiming
    {
        [Tooltip("拿出时长（秒）；0 = 立即拿出")]
        public float drawSeconds;

        [Tooltip("收回时长（秒）；0 = 立即收回")]
        public float stowSeconds;

        public EquipTiming(float drawSeconds, float stowSeconds)
        {
            this.drawSeconds = drawSeconds;
            this.stowSeconds = stowSeconds;
        }

        /// <summary>是否已配置：**任一轴非 0** 即已配置（单项 0 = 该项"立即"，合法）；
        /// 仅<b>双轴全 0</b>（旧资产反序列化默认 / 作者留空）视为未配置（回退 Default）。</summary>
        public bool IsSet => drawSeconds != 0f || stowSeconds != 0f;

        /// <summary>拿出时长（秒；公共只读访问，序列化字段为小写）。</summary>
        public float DrawSeconds => drawSeconds;

        /// <summary>收回时长（秒；公共只读访问，序列化字段为小写）。</summary>
        public float StowSeconds => stowSeconds;

        /// <summary>缺省拿放时长（旧资产/未配置回退；沿用早期默认参数）。</summary>
        public static EquipTiming Default { get; } = new EquipTiming(0.35f, 0.30f);
    }
}
