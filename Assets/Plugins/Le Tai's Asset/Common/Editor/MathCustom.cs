using UnityEngine;

namespace LeTai.Common.Editor
{
public static class MathCustom
{
    public static float VecToAngle360(Vector2 from, Vector2 to)
    {
        float angle = Vector2.SignedAngle(from, to);
        return angle < 0 ? 360 + angle : angle;
    }

    public static Vector2 Angle360ToVec(float angle, Vector2 zeroVector)
    {
        float sin = Mathf.Sin(angle * Mathf.Deg2Rad);
        float cos = Mathf.Cos(angle * Mathf.Deg2Rad);

        return new Vector2(
            zeroVector.x * cos - zeroVector.y * sin,
            zeroVector.x * sin + zeroVector.y * cos
        );
    }
}
}
