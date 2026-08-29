using System;
using System.Collections.Generic;
using NUnit.Framework;
using XeptKit.Core;

namespace XeptKit.Tests
{
    /// <summary>
    /// OverrideValue&lt;T&gt; 语义测试：默认值回退、优先级排序、同优先级覆盖更新、
    /// Clear/ClearAll 恢复、ValueChanged 触发契约（Set/Clear/ClearAll/DefaultValue 有效值变化均触发）、
    /// 引用类型 null 安全。
    /// </summary>
    public class OverrideValueTests
    {
        [Test]
        public void 无覆盖_返回默认值()
        {
            var ov = new OverrideValue<float> { DefaultValue = 60f };

            Assert.AreEqual(60f, ov.Value);
            Assert.AreEqual(0, ov.OverrideCount);
        }

        [Test]
        public void 覆盖_取最高优先级()
        {
            var ov = new OverrideValue<float> { DefaultValue = 60f };
            ov.Set(50f, OverridePriority.Default + 5);
            ov.Set(75f, OverridePriority.Effect);
            ov.Set(90f, OverridePriority.Debug);

            Assert.AreEqual(90f, ov.Value);
            Assert.AreEqual(3, ov.OverrideCount);
        }

        [Test]
        public void 同优先级重复写入_后写生效()
        {
            var ov = new OverrideValue<int>();
            ov.Set(1, 10);
            ov.Set(2, 10);

            Assert.AreEqual(2, ov.Value);
            Assert.AreEqual(1, ov.OverrideCount, "同优先级覆盖更新不新增条目");
        }

        [Test]
        public void Clear_移除指定优先级_恢复低优先级或默认()
        {
            var ov = new OverrideValue<float> { DefaultValue = 60f };
            ov.Set(75f, 20);
            ov.Clear(20);
            Assert.AreEqual(60f, ov.Value);

            ov.Clear(999); // 未注册优先级 no-op
            Assert.AreEqual(60f, ov.Value);
        }

        [Test]
        public void ClearAll_清空覆盖_保留默认()
        {
            var ov = new OverrideValue<float> { DefaultValue = 60f };
            ov.Set(75f, 20);
            ov.Set(80f, 100);
            ov.ClearAll();

            Assert.AreEqual(60f, ov.Value);
            Assert.AreEqual(0, ov.OverrideCount);
        }

        [Test]
        public void HasOverride_按优先级查询()
        {
            var ov = new OverrideValue<int>();
            ov.Set(1, 10);

            Assert.IsTrue(ov.HasOverride(10));
            Assert.IsFalse(ov.HasOverride(20));
        }

        [Test]
        public void ValueChanged_Set与Clear_有效值变化时触发()
        {
            var ov = new OverrideValue<int> { DefaultValue = 0 };
            var changes = new List<int>();
            ov.ValueChanged += v => changes.Add(v);

            ov.Set(5, 10);   // 0 → 5
            ov.Set(7, 10);   // 同优先级更新 → 7
            ov.Set(3, 1);    // 低优先级，有效值不变 → 不触发
            ov.Clear(10);    // 7 → 3
            ov.ClearAll();   // 3 → 0

            Assert.AreEqual(new List<int> { 5, 7, 3, 0 }, changes);
        }

        [Test]
        public void ValueChanged_DefaultValue变更_无覆盖时触发()
        {
            var ov = new OverrideValue<int>();
            var changes = new List<int>();
            ov.ValueChanged += v => changes.Add(v);

            ov.DefaultValue = 1; // 0 → 1
            ov.Set(5, 10);
            ov.DefaultValue = 2; // 有覆盖，有效值不变 → 不触发

            Assert.AreEqual(new List<int> { 1, 5 }, changes);
        }

        [Test]
        public void 引用类型_null值不抛异常()
        {
            var ov = new OverrideValue<string> { DefaultValue = "a" };

            Assert.DoesNotThrow(() => ov.Set(null, 10));
            Assert.IsNull(ov.Value);
            Assert.DoesNotThrow(() => ov.Set("b", 10));
            Assert.AreEqual("b", ov.Value);
            Assert.DoesNotThrow(() => ov.Clear(10));
            Assert.AreEqual("a", ov.Value);
        }
    }
}
