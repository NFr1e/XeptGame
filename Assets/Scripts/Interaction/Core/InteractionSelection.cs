using System;
using UnityEngine;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 选中状态机（纯逻辑类，无 MonoBehaviour，契约模型 v2）：维护上一目标与当前目标（单一门控者），
    /// 目标变化时对门控者执行 <c>OnDeselected(旧) → OnSelected(新)</c>（先退场后登场，避免重叠高亮），
    /// 随后发布 <see cref="Changed"/>（负载 <see cref="SelectionChangeArgs"/>，含旧/新选中）。
    /// <list type="bullet">
    /// <item>目标相同（含均为 null）为 no-op；</item>
    /// <item>销毁防护：Unity 假 null 感知（对象销毁后跳过推送）；</item>
    /// <item>由引擎壳（<c>InteractionDetector</c>）每帧驱动，决策逻辑与引擎解耦、可单测。</item>
    /// </list>
    /// v2 变更：比较键从 InteractionTarget.Host 改为 ISelectable（并集快照退役，单一门控者）。
    /// </summary>
    public sealed class InteractionSelection
    {
        private ISelectable _previous;

        /// <summary>目标变化事件（负载 = 旧选中, 新选中；可 null）。</summary>
        public event Action<SelectionChangeArgs> Changed;

        /// <summary>上一目标（只读，供外部对比）。</summary>
        public ISelectable Previous => _previous;

        /// <summary>
        /// 应用目标变化：目标不同（含 null 判定）时扇出选中事件并发布 <see cref="Changed"/>。
        /// 目标相同（含均为 null）为 no-op。
        /// </summary>
        public void Apply(ISelectable current, InteractionContext ctx)
        {
            if (_previous == current)
            {
                return;
            }

            if (_previous != null && IsAlive(_previous))
            {
                _previous.OnDeselected();
            }

            if (current != null && IsAlive(current))
            {
                current.OnSelected(ctx);
            }

            Changed?.Invoke(new SelectionChangeArgs(current, _previous));
            _previous = current;
        }

        /// <summary>
        /// 清空并补 deselect（引擎壳 OnDestroy 调用）：以 null 目标应用一次变化，对旧目标退场。
        /// null 目标不触发 <see cref="ISelectable.OnSelected"/>，ctx 使用 default 安全
        /// （<see cref="InteractionContext"/> 的字段不会被读取）。
        /// </summary>
        public void Clear()
        {
            if (_previous != null)
            {
                Apply(null, default);
            }
            _previous = null;
        }

        /// <summary>Unity 假 null 感知的存活检查（接口引用直判 null 无法捕获已销毁对象）。</summary>
        private static bool IsAlive(ISelectable selectable)
            => selectable is UnityEngine.Object unityObject && unityObject != null;
    }
}
