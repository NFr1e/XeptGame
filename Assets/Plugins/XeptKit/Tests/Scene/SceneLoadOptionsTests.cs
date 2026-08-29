using NUnit.Framework;
using XeptKit.Scenes;

namespace XeptKit.Tests
{
    /// <summary>
    /// <see cref="SceneLoadOptions"/> 默认值语义（Scenes.design.md §3「default 语义对齐」）：
    /// readonly struct 的 default / new() 不会调用带可选参数的构造函数，
    /// 实现经反转存储（_deferActivation）保证零值默认即"立即激活"；
    /// 显式 new SceneLoadOptions(activateOnLoad: false) 仍可表达延迟激活。
    /// 覆盖 default / new() / 显式 false 三种构造。
    /// </summary>
    public class SceneLoadOptionsTests
    {
        [Test]
        public void Default构造_立即激活_其余字段为默认()
        {
            var options = default(SceneLoadOptions);

            Assert.IsTrue(options.ActivateOnLoad, "default 必须立即激活（否则 LoadSceneAsync 默认参数会永久驻留 Ready）。");
            Assert.IsFalse(options.IsMainScene);
            Assert.IsFalse(options.Persistent);
            Assert.AreEqual(0, options.Priority);
        }

        [Test]
        public void New构造_立即激活()
        {
            var options = new SceneLoadOptions();

            Assert.IsTrue(options.ActivateOnLoad);
            Assert.AreEqual(default(SceneLoadOptions), options);
        }

        [Test]
        public void 显式False_延迟激活_且其余字段保持默认()
        {
            var options = new SceneLoadOptions(activateOnLoad: false);

            Assert.IsFalse(options.ActivateOnLoad);
            Assert.IsFalse(options.IsMainScene);
            Assert.IsFalse(options.Persistent);
            Assert.AreEqual(0, options.Priority);
        }

        [Test]
        public void 显式True_立即激活()
        {
            var options = new SceneLoadOptions(activateOnLoad: true);

            Assert.IsTrue(options.ActivateOnLoad);
        }

        [Test]
        public void Default静态字段_与default语义一致()
        {
            var options = SceneLoadOptions.Default;

            Assert.IsTrue(options.ActivateOnLoad);
            Assert.AreEqual(default(SceneLoadOptions), options);
            Assert.AreEqual(default(SceneLoadOptions).GetHashCode(), options.GetHashCode());
        }

        [Test]
        public void 显式False_与default按值可区分()
        {
            // 反转存储的判定性用例：若按值归一化（options == default 时改默认），
            // 该断言将失败——延迟激活工作流会被静默破坏（设计文档明令禁止该方案）。
            var deferred = new SceneLoadOptions(activateOnLoad: false);

            Assert.AreNotEqual(default(SceneLoadOptions), deferred);
            Assert.IsTrue(default(SceneLoadOptions) != deferred);
        }
    }
}
