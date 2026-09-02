using UnityEngine;
using XeptKit.Core;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互上下文：探测/执行时传递给可交互对象的最小信息。
    /// 仅承载交互者 <see cref="Transform"/>，不耦合 PlayerMotor 等业务组件——
    /// 可交互对象如需玩家状态（如"仅接地可交互"），按需自行解析引用，契约层保持轻量可测。
    /// 只读结构体，构造后不可变。
    /// </summary>
    public readonly struct InteractionContext
    {
        /// <summary>交互者（玩家）的 Transform；构造时校验非空。</summary>
        public Transform Interactor { get; }

        /// <summary>交互者位置（便捷访问）。</summary>
        public Vector3 InteractorPosition => Interactor != null ? Interactor.position : Vector3.zero;

        public InteractionContext(Transform interactor)
        {
            Guard.NotNullObject(interactor, nameof(interactor));
            Interactor = interactor;
        }
    }
}
