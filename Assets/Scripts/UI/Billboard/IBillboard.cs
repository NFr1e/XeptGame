using System;
using UnityEngine;

namespace XeptGame.UI.Billboard
{
    /// <summary>
    /// 世界锚点驱动效果器的**共同最小面（位置语义契约）**——不同于已退役的域 IBillboard：不承载内容/显隐/仲裁，
    /// 只有"跟随哪个世界锚点"：
    /// <list type="bullet">
    /// <item><see cref="SetAnchor"/>：注入锚点并广播 <see cref="AnchorChanged"/>（负载 = 新锚点）；</item>
    /// <item><see cref="CurrentAnchor"/>：最近注入/配置的锚点（读回用）；</item>
    /// <item><see cref="ClearAnchor"/>：语义由实现定义——HudBillboard：无锚 = 不投影；WorldBillboard：回退自身跟随。</item>
    /// </list>
    /// 消费方（任务指引同时驱动世界图标与 Hud 箭头、测试、锚点绑定层）只关心类型无关的锚点操作，
    /// 经 GetComponent&lt;IBillboard&gt; / 字段注入持有，不依赖 World/Hud 具体类型。
    /// 实现：<see cref="WorldBillboard"/> / <see cref="HudBillboard"/>。
    /// </summary>
    public interface IBillboard
    {
        /// <summary>最近注入/配置的世界锚点（未设置/清除后可为 null——实现可另有内部默认，如 World 自身跟随）。</summary>
        Transform CurrentAnchor { get; }

        /// <summary>注入锚点（广播 <see cref="AnchorChanged"/>）。</summary>
        void SetAnchor(Transform anchor);

        /// <summary>清除注入锚点（广播 <see cref="AnchorChanged"/>(null)）；后续回退语义由实现定义。</summary>
        void ClearAnchor();

        /// <summary>锚点变化事件（负载 = 新锚点；清除为 null）。</summary>
        event Action<Transform> AnchorChanged;
    }
}
