using System;
using System.Collections.Generic;
using NUnit.Framework;
using XeptGame.Equip;
using XeptGame.Items;

namespace XeptGame.Tests
{
    public class EquipBehaviorTests : EquipTestBase
    {
        [Test]
        public void 生命周期_占用与可用分离()
        {
            using var controller = new EquipController();
            var item = NewDef("stone", true);
            Assert.AreEqual(BehaviorRequest.Rejected, controller.RequestDraw());
            controller.ReconcileOccupancy(item, 1, new EquipTiming(1, 1));
            Assert.AreEqual(EquipPhase.Stowed, controller.Snapshot.Phase);
            Assert.AreEqual(BehaviorRequest.Started, controller.RequestDraw());
            controller.Tick(0.5f);
            Assert.AreEqual(EquipPhase.Drawing, controller.Snapshot.Phase);
            Assert.AreEqual(BehaviorRequest.AlreadyInProgress, controller.RequestDraw());
            controller.Tick(0.5f);
            Assert.AreEqual(EquipPhase.Ready, controller.Snapshot.Phase);
            controller.RequestStow();
            controller.Tick(1);
            Assert.IsTrue(controller.CanTransferOut);
            Assert.AreSame(item, controller.Snapshot.Item);
            controller.ReconcileOccupancy(null, 2, new EquipTiming(1, 1));
            Assert.AreEqual(EquipPhase.Empty, controller.Snapshot.Phase);
        }

        [Test]
        public void 打断_暂停_终局恰好一次()
        {
            using var controller = new EquipController();
            var ended = new List<EquipActionResult>();
            controller.ActionFinished += ended.Add;
            controller.ReconcileOccupancy(NewDef("stone", true), 1, new EquipTiming(1, 1));
            controller.RequestDraw();
            controller.Tick(0.4f);
            var first = controller.Snapshot.ActionId;
            controller.RequestStow();
            Assert.AreEqual(first, ended[0].ActionId);
            Assert.AreEqual(EquipActionOutcome.Superseded, ended[0].Outcome);
            Assert.AreEqual(BehaviorRequest.Rejected, controller.RequestDraw());
            controller.SetPaused(true);
            controller.Tick(10);
            Assert.AreEqual(0, controller.Snapshot.Elapsed);
            controller.SetPaused(false);
            controller.Tick(1);
            controller.Tick(10);
            Assert.AreEqual(2, ended.Count);
            Assert.AreEqual(EquipPhase.Stowed, controller.Snapshot.Phase);
        }

        [Test]
        public void 同定义新占用_终止旧动作_零时长有界()
        {
            using var controller = new EquipController();
            var item = NewDef("stone", true);
            var ended = new List<EquipActionResult>();
            controller.ActionFinished += ended.Add;
            controller.ReconcileOccupancy(item, 1, new EquipTiming(0, 0));
            controller.RequestDraw();
            controller.ReconcileOccupancy(item, 2, new EquipTiming(0, 0));
            Assert.AreEqual(EquipActionOutcome.Aborted, ended[0].Outcome);
            Assert.AreEqual(EquipPhase.Stowed, controller.Snapshot.Phase);
            controller.RequestDraw();
            controller.Tick(0);
            Assert.AreEqual(EquipPhase.Ready, controller.Snapshot.Phase);
            Assert.AreEqual(2, controller.Snapshot.OccupancyVersion);
            // Q4 坏帧防御（Debug.Assert + 忽略）不在单测覆盖：合法输入路径不会触发；
            // 防御分支语义简单（忽略坏帧），由开发期断言人工保护——触发即产生 Assert 日志噪音，故不喂 NaN。
        }
    }
}
