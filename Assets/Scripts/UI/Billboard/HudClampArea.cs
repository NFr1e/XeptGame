using UnityEngine;

namespace XeptGame.UI.Billboard
{
    /// <summary>
    /// 出域放置的边界几何（目标父级本地坐标；已扣元素半尺寸——目标整体保留在容器内）。
    /// 由 HudBillboard 每帧从钳制 RectTransform（或目标父级）解析，传给出域策略
    /// （<see cref="IHudBillboardClamp"/>）。矩形策略用其四边；椭圆策略用其内切椭圆（中心 = <see cref="Center"/>，
    /// 半轴 = 宽/2、高/2）。
    /// </summary>
    public readonly struct HudClampArea
    {
        public readonly float MinX;
        public readonly float MaxX;
        public readonly float MinY;
        public readonly float MaxY;

        public HudClampArea(float minX, float maxX, float minY, float maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        /// <summary>边界矩形中心（椭圆策略的椭圆中心）。</summary>
        public Vector2 Center => new Vector2((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);
    }
}
