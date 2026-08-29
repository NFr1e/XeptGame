using System;
using XeptKit.Core;

namespace XeptKit.UI.Manager
{
    /// <summary>
    /// 表单权限令牌（纯数据）：FormEntry 资产引用（身份键）+ 组内实例 ID。
    /// 纯值语义、零分配、可作字典键；持有句柄不保活任何表单实例。
    /// 刻意与 SceneHandle / AssetHandle&lt;T&gt;（class、含 internal set 状态）不同——本类型为 readonly struct。
    /// 实例 ID 单调递增不复用；default(FormHandle)（Entry null + Id 0）恒为无效。
    /// </summary>
    public readonly struct FormHandle : IEquatable<FormHandle>
    {
        /// <summary>表单资产（身份键），非运行时实例引用。</summary>
        public FormEntry Entry { get; }

        /// <summary>组内实例 ID（单调递增，不复用）。</summary>
        public int InstanceId { get; }

        internal FormHandle(FormEntry entry, int instanceId)
        {
            Entry = entry;
            InstanceId = instanceId;
        }

        public bool Equals(FormHandle other)
            => ReferenceEquals(Entry, other.Entry) && InstanceId == other.InstanceId;

        public override bool Equals(object obj) => obj is FormHandle other && Equals(other);

        public override int GetHashCode()
        {
            int entryHash = Entry != null
                ? ReferenceEqualityComparer<FormEntry>.Instance.GetHashCode(Entry)
                : 0;
            return (entryHash * 397) ^ InstanceId;
        }

        public static bool operator ==(FormHandle left, FormHandle right) => left.Equals(right);

        public static bool operator !=(FormHandle left, FormHandle right) => !left.Equals(right);
    }
}
