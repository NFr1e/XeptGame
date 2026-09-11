using UnityEngine;

namespace XeptGame.World
{
    /// <summary>
    /// 掉落锚（Item_Instance_Design.md §4 的"落点提供者"；世界层表现件，不含业务）：
    /// 回答"玩家要把东西丢到哪个世界坐标"，与"视图挂在哪"（<see cref="WorldViewSpawner"/> 的视图根）是两件事。
    /// <list type="bullet">
    /// <item><b>默认策略（脚高 + 前方 + 向下吸附）</b>：候选点 = 视线的<b>水平朝向</b> × 前方距离 + <b>脚底高度</b>
    /// ——刻意<b>不用视线的 y</b>，所以低头看地面时不会把落点带进地里；随后自候选点上方向下打一条射线吸附到地面
    /// （上下坡/台阶贴地），打不到（悬空、平台边缘）就用候选点原值；</item>
    /// <item>挂法：挂在<b>跟随视角</b>的锚点上（如 CameraRig 子物体）当"朝向来源"，另把 <c>feetSource</c> 指向玩家根
    /// （缺省用 <c>transform.root</c>）：<b>朝向来自视线、高度来自脚底</b>——这正是"低头不陷地"的关键；</item>
    /// <item>备选策略（需要时再换实现，接口不变）：<b>视线命中点</b>（丢到"你看的地方"，最直观但俯视会丢很远、看天要回退）、
    /// <b>固定脚下</b>（最省事、永不陷地，但常与玩家重叠）。</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldDropAnchor : MonoBehaviour
    {
        [Tooltip("朝向来源；留空 = 自身（挂 CameraRig 锚点时保持为空即可）")]
        [SerializeField] private Transform origin;

        [Tooltip("脚底高度来源（通常是玩家根）；留空 = transform.root——低下头时落点仍以脚高为准")]
        [SerializeField] private Transform feetSource;

        [Tooltip("沿水平朝向的前方距离（米）")]
        [SerializeField] private float forwardDistance = 1.2f;

        [Tooltip("向下探测：起点抬高（米）")]
        [SerializeField] private float probeUp = 0.5f;

        [Tooltip("向下探测：向下长度（米）")]
        [SerializeField] private float probeDown = 2f;

        [Tooltip("吸附到地面后抬高一点，避免半埋")]
        [SerializeField] private float surfaceOffset = 0.05f;

        [Tooltip("地面层；默认全部层")]
        [SerializeField] private LayerMask groundMask = ~0;

        /// <summary>解析本次掉落的世界落点（无副作用，可多次调用）。</summary>
        public Vector3 ResolveDropPoint()
        {
            var basis = origin != null ? origin : transform;
            var feet = feetSource != null ? feetSource : transform.root;

            // 1) 候选点：只用朝向的水平分量（低头/抬头不改变落点远近），高度取脚底
            var forward = Vector3.ProjectOnPlane(basis.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(basis.up, Vector3.up);
            }

            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            var candidate = basis.position + forward * forwardDistance;
            candidate.y = feet != null ? feet.position.y : basis.position.y;

            // 2) 向下吸附到地面：**排除玩家自身层级**（否则射线会先打到自己的胶囊/背包），
            //    取最近的有效命中；打不到就保持候选点（悬空/边缘也丢得出去）
            var from = candidate + Vector3.up * probeUp;
            var hits = Physics.RaycastAll(from, Vector3.down, probeUp + probeDown, groundMask, QueryTriggerInteraction.Ignore);
            var selfRoot = transform.root;
            var found = false;
            var bestDistance = float.MaxValue;
            var point = candidate;
            for (int i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || collider.transform.root == selfRoot)
                {
                    continue;
                }

                if (hits[i].distance < bestDistance)
                {
                    bestDistance = hits[i].distance;
                    point = hits[i].point;
                    found = true;
                }
            }

            return found ? point + Vector3.up * surfaceOffset : candidate;
        }
    }
}
