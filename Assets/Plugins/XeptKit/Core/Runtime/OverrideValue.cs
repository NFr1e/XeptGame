using System;
using System.Collections.Generic;

namespace XeptKit.Core
{
    /// <summary>
    /// 多优先级覆盖值：一个默认值 + 按优先级（值大优先）叠加的覆盖集。
    /// 读取 <see cref="Value"/> 时取最高优先级的覆盖，无覆盖时返回默认值。
    /// 用于"多个来源竞争同一属性"的场景：策划默认（基线）、玩家设置、
    /// 玩法效果（buff）、调试热改等，各来源以不同优先级注入，互不覆盖。
    /// 仅主线程使用；读热路径零分配（SortedList 常驻），写入低频。
    /// 复杂度：取 <see cref="Value"/>（最高优先级）O(1)；按优先级查找 O(log n)；写入 O(n)（数组移位）。
    /// <see cref="ValueChanged"/> 在有效值（Value）实际变化时触发（Set/Clear/ClearAll/DefaultValue 变更均覆盖）。
    /// </summary>
    public sealed class OverrideValue<T>
    {
        private readonly SortedList<int, T> _overrides = new();
        private T _defaultValue;

        /// <summary>有效值变化通知（参数 = 新有效值）。仅有效值实际变化时触发，无覆盖时默认值变更也会触发。</summary>
        public event Action<T> ValueChanged;

        /// <summary>基线值（优先级最低；非覆盖）。</summary>
        public T DefaultValue
        {
            get => _defaultValue;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_defaultValue, value))
                {
                    return;
                }

                var before = Value;
                _defaultValue = value;
                Notify(before);
            }
        }

        /// <summary>当前有效值：最高优先级覆盖，无覆盖时为 <see cref="DefaultValue"/>。O(1) 读。</summary>
        public T Value
        {
            get
            {
                if (_overrides.Count == 0)
                {
                    return _defaultValue;
                }

                return _overrides[_overrides.Keys[_overrides.Count - 1]];
            }
        }

        /// <summary>当前覆盖数量（不含默认值）。</summary>
        public int OverrideCount => _overrides.Count;

        /// <summary>
        /// 以指定优先级写入覆盖。同一优先级再次写入为覆盖更新（后写生效）；
        /// 不同优先级各自独立，读取时取最大优先级。有效值变化时触发 <see cref="ValueChanged"/>。
        /// </summary>
        public void Set(T value, int priority)
        {
            var before = Value;
            _overrides[priority] = value;

            Notify(before);
        }

        /// <summary>移除指定优先级的覆盖。未注册的优先级 no-op。有效值变化时触发 <see cref="ValueChanged"/>。</summary>
        public void Clear(int priority)
        {
            var before = Value;
            if (_overrides.Remove(priority))
            {
                Notify(before);
            }
        }

        /// <summary>该优先级是否已有覆盖。</summary>
        public bool HasOverride(int priority)
        {
            return _overrides.ContainsKey(priority);
        }

        /// <summary>清空全部覆盖（保留默认值）。有效值变化时触发 <see cref="ValueChanged"/>。</summary>
        public void ClearAll()
        {
            if (_overrides.Count == 0)
            {
                return;
            }

            var before = Value;
            _overrides.Clear();
            Notify(before);
        }

        /// <summary>有效值比较（null 安全）后通知。</summary>
        private void Notify(T before)
        {
            if (!EqualityComparer<T>.Default.Equals(before, Value))
            {
                ValueChanged?.Invoke(Value);
            }
        }
    }

    /// <summary>
    /// 配置覆盖优先级约定（值大优先，见 <see cref="OverrideValue{T}"/>）。
    /// 业务自定义优先级时建议沿用此区间划分，避免语义冲突。
    /// </summary>
    public static class OverridePriority
    {
        /// <summary>基线默认值（资产/代码默认，非覆盖）。</summary>
        public const int Default = 0;

        /// <summary>玩家自定义设置（设置菜单写入）。</summary>
        public const int Player = 10;

        /// <summary>玩法效果（buff/技能/状态）。</summary>
        public const int Effect = 20;

        /// <summary>调试热改（运行时覆盖，优先于一切）。</summary>
        public const int Debug = 100;
    }
}
