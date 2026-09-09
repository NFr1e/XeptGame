using System;
using System.Collections.Generic;
using NUnit.Framework;
using XeptKit.Core;
using XeptKit.Event;

namespace XeptKit.Tests
{
    /// <summary>
    /// SafeEvent&lt;T&gt; 语义测试：订阅去重/null 策略、退订、广播逐监听异常隔离（A 抛 B 仍执行、不向外抛、日志记录）、
    /// 快照迭代（回调中增删不影响本轮）、句柄退订、Clear。
    /// </summary>
    public class SafeEventTests
    {
        private sealed class FakeLog : ILog
        {
            public int ExceptionCount;
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message) { }
            public void Exception(Exception exception) => ExceptionCount++;
        }

        private ILog _original;
        private FakeLog _fake;

        [SetUp]
        public void SetUp()
        {
            _original = Log.GetImplementation();
            _fake = new FakeLog();
            Log.SetImplementation(_fake);
        }

        [TearDown]
        public void TearDown() => Log.SetImplementation(_original);

        [Test]
        public void Add与Invoke_按订阅顺序执行()
        {
            var e = new SafeEvent<int>();
            var order = new List<int>();
            e.Add(v => order.Add(v * 10));
            e.Add(v => order.Add(v + 1));

            e.Invoke(1);

            Assert.AreEqual(new[] { 10, 2 }, order);
        }

        [Test]
        public void Add_重复订阅同一handler_幂等去重()
        {
            var e = new SafeEvent<int>();
            int calls = 0;
            void Handler(int v) => calls++;

            e.Add(Handler);
            e.Add(Handler);
            e.Invoke(1);

            Assert.AreEqual(1, calls, "重复订阅同一委托只保留首次");
        }

        [Test]
        public void Add_null_抛错_Remove_null为false()
        {
            var e = new SafeEvent<int>();
            Assert.Throws<ArgumentNullException>(() => e.Add(null));
            Assert.IsFalse(e.Remove(null));
        }

        [Test]
        public void Remove_未订阅返回false_订阅后退订成功且不再收到()
        {
            var e = new SafeEvent<int>();
            int calls = 0;
            void Handler(int v) => calls++;

            Assert.IsFalse(e.Remove(Handler));
            e.Add(Handler);
            Assert.IsTrue(e.Remove(Handler));
            e.Invoke(1);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Invoke_监听异常被隔离_其余监听仍执行且不向外抛()
        {
            var e = new SafeEvent<int>();
            var received = new List<int>();
            e.Add(v => throw new InvalidOperationException("boom"));
            e.Add(received.Add);

            Assert.DoesNotThrow(() => e.Invoke(7));

            Assert.AreEqual(1, received.Count, "异常监听之后的监听仍收到事件");
            Assert.AreEqual(7, received[0]);
            Assert.AreEqual(1, _fake.ExceptionCount, "异常被记录（Log.Exception）一次");
        }

        [Test]
        public void Invoke_回调中增删订阅_不影响本轮快照()
        {
            var e = new SafeEvent<int>();
            int after = 0;
            void LateAdd(int v) => e.Add(v => after++);
            e.Add(LateAdd);
            e.Add(v => after++);

            e.Invoke(1);

            Assert.AreEqual(1, after, "本轮快照后新增的监听不参与本轮");
            e.Invoke(1);
            Assert.AreEqual(3, after, "下轮起新监听参与（原两个 + 新增一个）");
        }

        [Test]
        public void Subscribe_句柄Dispose即退订且幂等()
        {
            var e = new SafeEvent<int>();
            int calls = 0;
            void Handler(int v) => calls++;

            var handle = e.Subscribe(Handler);
            e.Invoke(1);
            handle.Dispose();
            handle.Dispose();
            e.Invoke(1);

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Clear_清空全部订阅()
        {
            var e = new SafeEvent<int>();
            int calls = 0;
            void Handler(int v) => calls++;

            e.Add(Handler);
            e.Clear();
            e.Invoke(1);

            Assert.AreEqual(0, calls);
        }
    }
}
