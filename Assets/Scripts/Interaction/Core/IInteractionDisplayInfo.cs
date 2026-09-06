using UnityEngine;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互提示头部可选显示信息（宿主实现；提示层消费，Interaction_Prompt_V3_Design.md §3.1 头部扩展）：
    /// 图标（Sprite/Texture 二选一，可为空）+ 交互物名字。
    /// 名字策略：<see cref="DisplayNameKey"/> 非空走本地化解析（未注册 → 提示隐名）；
    /// 键为空则回退 <see cref="DisplayName"/> 直显文案（火堆等无键对象）。实现/字段全空 → 提示隐藏对应栏。
    /// </summary>
    public interface IInteractionDisplayInfo
    {
        /// <summary>名字本地化键（Item 类宿主给 id 派生键）；空 = 无键。</summary>
        string DisplayNameKey { get; }

        /// <summary>名字直显文案（无键宿主用，如火堆序列化 displayName）；键路径下仅在键解析命中前作为无键回退。</summary>
        string DisplayName { get; }

        /// <summary>Sprite 图标（IconKind=Sprite 或任意 Sprite 源）；空 = 无。</summary>
        Sprite IconSprite { get; }

        /// <summary>纹理图标（Texture2D/RenderTexture 等）；空 = 无。</summary>
        Texture IconTexture { get; }
    }
}
