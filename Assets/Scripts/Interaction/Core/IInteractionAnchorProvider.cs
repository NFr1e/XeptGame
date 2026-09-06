using UnityEngine;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 世界锚点提供者（可选能力接口）：宿主（可交互对象）可显式声明"交互/表现应跟随的世界锚点"
    /// （如头顶提示点、长物末端）——由 <see cref="InteractionAnchorPoint"/> 通用组件实现，
    /// 作者在 Inspector 把锚点 Transform 拖入。未实现本接口或 AnchorPoint 为空 → 解析器回退宿主自身
    /// Transform（InteractionAnchorResolver.Resolve）。
    /// 与表现机制解耦：本接口只声明"锚在哪"，投影/显隐（HudBillboard/WorldBillboard）归显示模块。
    /// </summary>
    public interface IInteractionAnchorProvider
    {
        /// <summary>自定义世界锚点；null = 未指定（解析器回退宿主 Transform）。</summary>
        Transform AnchorPoint { get; }
    }
}
