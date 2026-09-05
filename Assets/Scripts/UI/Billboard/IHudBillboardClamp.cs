using UnityEngine;

namespace XeptGame.UI.Billboard
{
    /// <summary>
    /// HudBillboard 出域放置策略（可插拔——效果器的策略面；marker/arrow 内容切换与旋转仍归上层业务）：
    /// HudBillboard 核心负责投影、前/后判定（委托 CameraFrustum）与 anchoredPosition 写入；
    /// 本策略只回答两问：
    /// <list type="bullet">
    /// <item><see cref="IsInside"/>：面前候选点是否在"界内"——界内 → 真实投影显示（marker 态，不钳制）；</item>
    /// <item><see cref="ClampToBoundary"/>：出界/背后时沿某方向（面前=目标方向；背后=相机本地横向）应落在边界的点。</item>
    /// </list>
    /// 内置实现：<see cref="RectEdgeClamp"/>（默认——矩形方向性保界，行为经验证）/
    /// <see cref="EllipseGuidance"/>（内切椭圆域：界内普通标志、界外箭头贴椭圆线——任务指引双态，供 Direction 旋转）。
    /// </summary>
    public interface IHudBillboardClamp
    {
        /// <summary>面前候选点是否在界内（在界内 → 真实投影显示，不钳制）。</summary>
        bool IsInside(Vector2 localPoint, HudClampArea area);

        /// <summary>沿方向把点放到边界（矩形策略：边框射线求交；椭圆策略：椭圆射线求交）。中心在界内、方向非零。</summary>
        Vector2 ClampToBoundary(Vector2 center, Vector2 dir, HudClampArea area);
    }
}
