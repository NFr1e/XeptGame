using UnityEngine;
using XeptKit.Core;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互探测解析器（纯逻辑静态类，无 MonoBehaviour，契约模型 v2）：
    /// 输入探测射线 + 配置 + 交互者，执行统一解析管线——
    /// <list type="bullet">
    /// <item>物理探测：SphereCast（小半径容错，0 = 纯射线）+ 层过滤；</item>
    /// <item>角度过滤：目标方向与探测方向的夹角超限视为不可瞄准；</item>
    /// <item><b>最近 ISelectable 解析</b>：沿碰撞体向上解析**最近一个** <see cref="ISelectable"/>
    /// （碰撞体自身优先、逐级上溯父链），以 <see cref="ISelectable.CanSelect"/> 门控选中——
    /// 选中严格由 ISelectable 门控（单一门控者，v2 移除"纯交互对象恒可选中"特殊规则）；</item>
    /// <item>探测快照：同步记录 <see cref="ProbeInfo"/>（画的是实际判定用的那次探测）。</item>
    /// </list>
    /// 动作（IInteractionAction）不在此管线（v3）：宿主推送后由执行器沿宿主链收集动作集。
    /// 探测源（<see cref="IInteractionProbeSource"/>）与解析逻辑解耦：任何消费方共用本管线。
    /// </summary>
    public static class InteractionResolver
    {
        /// <summary>
        /// 执行一次交互探测解析。不执行交互、不派发选中（选中推送由消费方状态机执行）。
        /// </summary>
        /// <param name="ray">探测射线（探测源提供）。</param>
        /// <param name="profile">探测配置（距离/半径/层/角度约束）。</param>
        /// <param name="interactor">交互者 Transform（构造 <see cref="InteractionContext"/> 用）。</param>
        public static InteractionProbeResult Resolve(
            InteractionProbeRay ray, InteractionProfile profile, Transform interactor)
        {
            GuardProfile(profile);

            if (!ray.IsValid)
            {
                return new InteractionProbeResult(null, 0f, default);
            }

            var origin = ray.Origin;
            var direction = ray.Direction;
            var ctx = new InteractionContext(interactor);

            if (!Probe(origin, direction, profile, out var hit))
            {
                // 未命中：快照终点 = 起点沿方向到最大距离（调试可视化）
                return new InteractionProbeResult(null, 0f,
                    ProbeInfo.Miss(origin, direction, profile.probeRadius, profile.maxRange));
            }

            // 角度过滤：目标方向与探测方向的夹角超限视为不可瞄准
            var angleValid = profile.maxAngle <= 0f
                || Vector3.Angle(direction, hit.point - origin) <= profile.maxAngle;

            // 目标匹配：最近 ISelectable（碰撞体自身优先、逐级上溯父链——单一门控者，无并集/无扇出数组）
            var selectable = ResolveNearestSelectable(hit.collider);
            var canSelect = angleValid && selectable != null && selectable.CanSelect(ctx);

            var probe = new ProbeInfo(
                origin, direction, profile.probeRadius, profile.maxRange,
                hit: true, hit.point, hit.distance, hit.normal,
                invalid: selectable != null && !canSelect,
                semantic: canSelect);

            return new InteractionProbeResult(
                canSelect ? selectable : null, hit.distance, probe);
        }

        /// <summary>
        /// 最近解析：碰撞体自身 <c>GetComponent</c> 优先，未命中则逐级上溯父链（最近者胜）。
        /// 契约组件可挂父级、碰撞体在子级；父子都有 ISelectable 时最近者胜（消除祖先误伤）。
        /// 逐级 GetComponent（非 GetComponentsInParent 数组）——常见路径零分配。
        /// </summary>
        private static ISelectable ResolveNearestSelectable(Collider collider)
        {
            var selectable = collider.GetComponent<ISelectable>();
            if (selectable != null && IsAlive(selectable))
            {
                return selectable;
            }

            var parent = collider.transform.parent;
            while (parent != null)
            {
                selectable = parent.GetComponent<ISelectable>();
                if (selectable != null && IsAlive(selectable))
                {
                    return selectable;
                }
                parent = parent.parent;
            }

            return null;
        }

        /// <summary>Unity 假 null 感知的存活检查（接口引用直判 null 无法捕获已销毁对象）。</summary>
        private static bool IsAlive(ISelectable selectable)
            => selectable is UnityEngine.Object unityObject && unityObject != null;

        /// <summary>物理探测：半径 &gt; 0 用 SphereCast，否则纯射线（像素级瞄准）。</summary>
        private static bool Probe(Vector3 origin, Vector3 direction, InteractionProfile profile, out RaycastHit hit)
        {
            if (profile.probeRadius <= 0f)
            {
                return Physics.Raycast(
                    origin, direction, out hit, profile.maxRange,
                    profile.layers, QueryTriggerInteraction.Ignore);
            }

            return Physics.SphereCast(
                origin, profile.probeRadius, direction, out hit, profile.maxRange,
                profile.layers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>配置非空校验（调用方传入已解析的配置；防御 null，含 Unity 假 null）。</summary>
        private static void GuardProfile(InteractionProfile profile)
        {
            Guard.NotNullObject(profile, nameof(profile));
        }
    }
}
