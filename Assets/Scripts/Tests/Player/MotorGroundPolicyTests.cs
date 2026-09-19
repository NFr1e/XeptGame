using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Player;

namespace XeptGame.Tests
{
    /// <summary>
    /// 地面候选策略回归（设计决议 §5.5 单向碰撞）：**动态刚体 = 可推动道具 ⇒ 不是地面**；
    /// kinematic 刚体（PhysicsMover 移动平台）与静态碰撞体仍是地面。
    /// </summary>
    public class MotorGroundPolicyTests
    {
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
        }

        private Collider NewCollider(bool withRigidbody, bool kinematic)
        {
            var go = new GameObject(withRigidbody ? (kinematic ? "KinematicBody" : "DynamicBody") : "StaticCollider");
            _objects.Add(go);
            var box = go.AddComponent<BoxCollider>();
            if (withRigidbody)
            {
                var body = go.AddComponent<Rigidbody>();
                body.isKinematic = kinematic;
            }

            return box;
        }

        [Test]
        public void 动态刚体_不是地面()
        {
            Assert.IsFalse(MotorGroundPolicy.IsStandableSurface(NewCollider(withRigidbody: true, kinematic: false)),
                "可推动道具（动态刚体）不应作为地面——否则会吸附/接地并干扰着陆判定");
        }

        [Test]
        public void 运动学刚体_仍是地面()
        {
            Assert.IsTrue(MotorGroundPolicy.IsStandableSurface(NewCollider(withRigidbody: true, kinematic: true)),
                "移动平台（PhysicsMover = kinematic 刚体）必须仍可站立，不能被这条策略误伤");
        }

        [Test]
        public void 静态碰撞体_仍是地面()
        {
            Assert.IsTrue(MotorGroundPolicy.IsStandableSurface(NewCollider(withRigidbody: false, kinematic: false)),
                "普通地形（静态碰撞体）仍是地面");
        }

        [Test]
        public void 空碰撞体_不是地面()
        {
            Assert.IsFalse(MotorGroundPolicy.IsStandableSurface(null));
        }
    }
}
