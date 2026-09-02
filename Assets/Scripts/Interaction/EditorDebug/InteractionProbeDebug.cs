using UnityEngine;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互判定调试绘制（Scene 视图 Gizmos）：可视化 <see cref="InteractionDetector"/> 的
    /// 实际判定数据——探测（球 + 线 + 命中点 + 法线）、角度约束锥、当前选中目标圆环。
    /// <list type="bullet">
    /// <item>数据来源：<see cref="InteractionDetector.LastProbe"/>（实际判定时记录），不重复探测；</item>
    /// <item>颜色约定：绿 = 已选中（含可交互目标）；红 = 未命中；黄 = 命中目标对象但选中被拒
    /// （CanSelect=false 或角度过滤失败）；白 = 命中普通表面（非目标对象）；青 = 角度约束锥；</item>
    /// <item>EditorDebug 目录（运行时程序集）：类体不包 #if（构建中类须存在，避免 missing script），
    /// 仅 <see cref="OnDrawGizmos"/> 体以 #if UNITY_EDITOR 包裹——构建中为惰性组件，零开销。</item>
    /// </list>
    /// 挂在任意物体（留空自动查找场景中的 InteractionDetector——调试便利，非运行时装配；同 MotorDebugGizmos 定位）。
    /// </summary>
    public sealed class InteractionProbeDebug : MonoBehaviour
    {
        [Tooltip("交互目标探测器（留空自动查找场景中的 InteractionDetector；调试便利，非运行时装配）")]
        [SerializeField] private InteractionDetector detector;

        [Tooltip("绘制交互探测（球 + 线 + 命中点 + 法线）")]
        [SerializeField] private bool drawProbe = true;

        [Tooltip("绘制角度约束锥（maxAngle > 0 时）")]
        [SerializeField] private bool drawAngleCone = true;

        [Tooltip("绘制当前选中目标的命中圆环")]
        [SerializeField] private bool drawTargetRing = true;

        private const float RingRadius = 0.15f;
        private const int ConeSegments = 16;

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            detector ??= FindAnyObjectByType<InteractionDetector>();
            if (detector == null)
            {
                return;
            }

            var probe = detector.LastProbe;
            if (drawProbe)
            {
                DrawProbe(probe);
            }

            var profile = detector.Profile;
            if (drawAngleCone && profile != null && profile.maxAngle > 0f)
            {
                DrawCone(probe.Origin, probe.Direction, profile.maxAngle, profile.maxRange);
            }

            if (drawTargetRing && probe.Semantic)
            {
                Gizmos.color = Color.green;
                DrawDisc(probe.HitPoint, probe.Normal, RingRadius);
            }
#endif
        }

        /// <summary>绘制一次探测快照（线 + 命中点 + 法线 + 起点球）。颜色约定见类注释。</summary>
        private static void DrawProbe(in ProbeInfo probe)
        {
            if (!probe.Hit)
            {
                // 未命中：终点 = 起点沿方向到最大距离
                Gizmos.color = Color.red;
                Gizmos.DrawLine(probe.Origin, probe.Origin + probe.Direction * probe.MaxDistance);
                return;
            }

            // 命中：按语义着色（选中=绿 / 被拒=黄 / 普通表面=白）
            Gizmos.color = probe.Semantic ? Color.green : (probe.Invalid ? Color.yellow : Color.white);
            Gizmos.DrawLine(probe.Origin, probe.HitPoint);
            Gizmos.DrawSphere(probe.HitPoint, 0.03f);
            Gizmos.DrawLine(probe.HitPoint, probe.HitPoint + probe.Normal * 0.3f);

            if (probe.Radius > 0f)
            {
                Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.5f);
                Gizmos.DrawWireSphere(probe.Origin, probe.Radius);
            }
        }

        /// <summary>角度约束锥可视化：沿探测方向 maxAngle 锥面上等距采样射线。</summary>
        private static void DrawCone(Vector3 origin, Vector3 direction, float maxAngle, float maxRange)
        {
            var perp = Vector3.Cross(direction, Vector3.up);
            if (perp.sqrMagnitude < 1e-6f)
            {
                perp = Vector3.right;
            }
            perp.Normalize();

            var cosA = Mathf.Cos(maxAngle * Mathf.Deg2Rad);
            var sinA = Mathf.Sin(maxAngle * Mathf.Deg2Rad);

            Gizmos.color = Color.cyan;
            for (int i = 0; i < ConeSegments; i++)
            {
                var offset = Quaternion.AngleAxis(i * 360f / ConeSegments, direction) * perp;
                var rayDir = (direction * cosA + offset * sinA).normalized;
                Gizmos.DrawRay(origin, rayDir * maxRange);
            }
        }

        /// <summary>
        /// 圆环可视化：Gizmos 无 DrawWireDisc（该 API 属 UnityEditor.Handles），
        /// 以折线段近似绘制（零依赖，编辑/运行时 Gizmos 通吃）。
        /// </summary>
        private static void DrawDisc(Vector3 center, Vector3 normal, float radius, int segments = 24)
        {
            var perp = Vector3.Cross(normal, Vector3.up);
            if (perp.sqrMagnitude < 1e-6f)
            {
                perp = Vector3.right;
            }
            perp.Normalize();

            var prev = center + perp * radius;
            for (int i = 1; i <= segments; i++)
            {
                var dir = Quaternion.AngleAxis(i * 360f / segments, normal) * perp;
                var next = center + dir * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
