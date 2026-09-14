using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Player;
using XeptKit.FSM;

namespace XeptGame.Tests
{
    /// <summary>
    /// 3C 决策层测试基座：假电机（<see cref="IMotor"/> 测试替身）+ 意图/Profile 装配 + FSM 驱动辅助。
    /// 决策层零 KCC 依赖（设计决议 §5.2），因此可在 EditMode 下用假电机完整驱动状态转移、动作准入与
    /// 速度求解——不需要场景、物理或 Play Mode。
    /// 驱动原则：**只用公开 API**（`Fsm.RequestChange`/`Tick`/`CurrentState`）与**意图状态**推进，
    /// 不碰 `CompositeStateBase.SubMachine`（protected internal）——这样测试同时是对"意图 → 转移表"的回归。
    /// </summary>
    public abstract class MotorTestBase
    {
        private readonly List<Object> _owned = new();
        private readonly List<Fsm> _machines = new();

        protected PlayerMotorProfile Profile { get; private set; }
        protected PlayerMotorInputState Input { get; private set; }
        protected FakeMotor Motor { get; private set; }
        protected PlayerMotorContext Ctx { get; private set; }

        [SetUp]
        public void SetUp()
        {
            Profile = ScriptableObject.CreateInstance<PlayerMotorProfile>();
            _owned.Add(Profile);

            Input = new PlayerMotorInputState();
            Motor = new FakeMotor();
            Ctx = new PlayerMotorContext(Motor, Input, Profile);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var fsm in _machines)
            {
                fsm.Dispose();
            }

            _machines.Clear();

            foreach (var obj in _owned)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _owned.Clear();
        }

        // ============================================================
        // 装配 / 驱动辅助
        // ============================================================

        /// <summary>新建电机 HFSM（不进入任何状态；用于"未初始化即调度"等边界）。</summary>
        protected Fsm NewFsm()
        {
            var fsm = new Fsm(Ctx);
            _machines.Add(fsm);
            return fsm;
        }

        /// <summary>新建并进入 Grounded（初始子状态 Idle）——与 PlayerController.Awake 装配一致（令牌用 default）。</summary>
        protected Fsm NewGroundedFsm()
        {
            var fsm = NewFsm();
            fsm.RequestChange<GroundedState>();
            return fsm;
        }

        protected MotorActionDispatcher NewDispatcher(Fsm fsm) => new MotorActionDispatcher(fsm, Ctx);

        protected static void Tick(Fsm fsm, float dt = 0.02f) => fsm.Tick(dt);

        /// <summary>按意图逐帧 Tick 直到到达目标状态（走真实转移，不使用内部子机器）。</summary>
        protected void TickUntil<TState>(Fsm fsm, int maxTicks = 8, float dt = 0.02f) where TState : StateBase
        {
            for (int i = 0; i < maxTicks && !fsm.IsInHierarchy(typeof(TState)); i++)
            {
                fsm.Tick(dt);
            }

            Assert.IsTrue(fsm.IsInHierarchy(typeof(TState)),
                $"未在 {maxTicks} 次 Tick 内到达 {typeof(TState).Name}（当前 {fsm.CurrentStateType?.Name}）");
        }

        /// <summary>当前叶子状态（KCC 回调在运行时就是分发给它的）。</summary>
        protected static MotorStateBase LeafOf(Fsm fsm) => (MotorStateBase)fsm.CurrentState;

        /// <summary>驱动到 Grounded/Sprint：移动意图 + 冲刺意图 + 自主速度。</summary>
        protected Fsm NewSprintingFsm(float speed = 8f)
        {
            var fsm = NewGroundedFsm();
            Input.LocalMoveIntent = Vector3.forward;
            Input.SprintIntent = true;
            Motor.SetVelocity(Vector3.forward * speed);
            TickUntil<SprintState>(fsm);
            return fsm;
        }

        /// <summary>驱动到 Grounded/Slide（冲刺 + 蹲伏意图 + 速度达门槛，经调度器准入）。</summary>
        protected Fsm NewSlidingFsm(float speed = 8f)
        {
            var fsm = NewSprintingFsm(speed);
            Input.CrouchIntent = true;
            NewDispatcher(fsm).Dispatch();

            Assert.IsTrue(fsm.IsInHierarchy(typeof(SlideState)),
                $"未进入滑铲（当前 {fsm.CurrentStateType?.Name}）");
            return fsm;
        }

        /// <summary>滑铲/空中切换用：把接地报告设为"无地面"。</summary>
        protected void SetAirborneGround()
            => Motor.GroundState = new MotorGroundState(false, false, Vector3.up);

        /// <summary>把接地报告设为"稳定接地"且层可站立（layer 6 ∈ StableGroundLayers 默认值）。</summary>
        protected void SetStableGround(Vector3 normal)
        {
            Motor.GroundState = new MotorGroundState(true, true, normal);
            Motor.GroundColliderLayerValue = 6;
        }

        /// <summary>
        /// 假电机：把决策层依赖的电机语义全部变为可编程（接地报告 / 速度 / 接地层 / 胶囊记录 / 重叠结果）。
        /// <see cref="GetDirectionTangentToSurface"/> 与 <see cref="TransformDirection"/> 逐字复刻
        /// KccMotorAdapter 依赖的 KCC 语义（切向重定向取归一化方向；body 局部 → 世界 = TransientRotation × local），
        /// 使状态求值在测试中与运行时同解。
        /// </summary>
        protected sealed class FakeMotor : IMotor
        {
            public MotorGroundState GroundState = new MotorGroundState(true, true, Vector3.up);

            /// <summary>当前接地碰撞体层（-1 = 无接地对象）。</summary>
            public int GroundColliderLayerValue = -1;

            /// <summary>起身重叠检查结果（true = 头顶被阻挡）。</summary>
            public bool OverlapBlocked;

            /// <summary>ForceUnground 调用次数（跳跃防抖断言用）。</summary>
            public int ForceUngroundCount;

            public Vector3 Position = Vector3.zero;
            public Quaternion Rotation = Quaternion.identity;

            /// <summary>最近一次设置的胶囊尺寸（半径/高度/Y 偏移）。</summary>
            public (float Radius, float Height, float YOffset)? Capsule;

            public MotorGroundState Ground => GroundState;
            public Vector3 CharacterUp => Vector3.up;
            public Vector3 TransientPosition => Position;
            public Quaternion TransientRotation => Rotation;
            public Vector3 Velocity { get; private set; }
            public Vector3 OwnVelocity { get; private set; }
            public Vector3 AttachedRigidbodyVelocity { get; private set; }
            public bool MustUnground { get; private set; }
            public float MaxStableSlopeAngle => 45f;
            public int GroundColliderLayer => GroundColliderLayerValue;
            public LayerMask StableGroundLayers { get; set; } = 1 << 6;

            /// <summary>按"自主速度 + 平台速度"设置（与生产一致：Velocity = Own + AttachedRigidbody）。</summary>
            public void SetVelocity(Vector3 ownVelocity, Vector3 attachedRigidbodyVelocity = default)
            {
                OwnVelocity = ownVelocity;
                AttachedRigidbodyVelocity = attachedRigidbodyVelocity;
                Velocity = ownVelocity + attachedRigidbodyVelocity;
            }

            public void SetCapsuleDimensions(float radius, float height, float yOffset)
                => Capsule = (radius, height, yOffset);

            public void ForceUnground(float time = 0.1f)
            {
                ForceUngroundCount++;
                MustUnground = true;
            }

            /// <summary>模拟电机"离地防抖窗口结束"（ForceUnground 的副作用复位）。</summary>
            public void ClearForceUnground() => MustUnground = false;

            public bool CharacterOverlapCheck(Vector3 position, Quaternion rotation,
                float radius, float height, float yOffset,
                QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore)
                => OverlapBlocked;

            /// <summary>与 KinematicCharacterMotor.GetDirectionTangentToSurface 同解（归一化切向方向）。</summary>
            public Vector3 GetDirectionTangentToSurface(Vector3 direction, Vector3 surfaceNormal)
                => Vector3.Cross(surfaceNormal, Vector3.Cross(direction, CharacterUp)).normalized;

            public Vector3 TransformDirection(Vector3 localDirection) => Rotation * localDirection;
        }
    }
}
