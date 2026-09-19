using KinematicCharacterController;
using UnityEngine;
using XeptKit.Core;
using XeptKit.FSM;

namespace XeptGame.Player
{
    /// <summary>
    /// KCC 回调翻译器：实现 <see cref="ICharacterController"/>，把电机的 9 个回调分发到
    /// PlayerMotor FSM 的当前状态（见设计决议 §3.3 回调映射表）。
    /// 本类是决策层与 KCC 电机的唯一接触面（另一面是 <see cref="KccMotorAdapter"/> 端口实现）。
    /// </summary>
    public sealed class PlayerCharacterController : ICharacterController
    {
        private readonly Fsm _fsm;

        public PlayerCharacterController(Fsm fsm)
        {
            Guard.NotNull(fsm, nameof(fsm));
            _fsm = fsm;
        }

        /// <summary>当前叶子状态强转为电机状态（未就绪/非电机状态时返回 null）。</summary>
        private MotorStateBase CurrentMotorState => _fsm.CurrentState as MotorStateBase;

        // —— 决策求值（ref 参数，唯一改旋转/速度处）——

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            CurrentMotorState?.ApplyRotation(ref currentRotation, deltaTime);
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            CurrentMotorState?.ApplyVelocity(ref currentVelocity, deltaTime);
        }

        // —— 帧钩子（转移/后处理挂点）——

        public void BeforeCharacterUpdate(float deltaTime)
        {
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            // 物理转移统一在 Fsm.Tick（PlayerController.Update，执行序晚于电机）判定；
            // 本钩子保留为执行顺序被改动时的备选挂点（设计决议 §3.1）。
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            CurrentMotorState?.PostUpdate(deltaTime);
        }

        // —— 碰撞事件（默认空实现；UnstableGround 判定辅助等后续按需接入）——

        public bool IsColliderValidForCollisions(Collider coll) => true;

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>
        /// KCC 原生"逐命中稳定性"钩子（在 <c>EvaluateHitStability</c> 末尾回调）：
        /// **单向碰撞**（设计决议 §5.5）——可推动道具（动态刚体）不是地面。
        /// 命中仍会阻挡移动、参与推动（移动 sweep 与交互刚体处理照旧），但把稳定性判为 false ⇒
        /// 不吸附、`IsStableOnGround=false`、不进入 Grounded（因此不触发着陆事件）。
        /// </summary>
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
            if (!MotorGroundPolicy.IsStandableSurface(hitCollider))
            {
                hitStabilityReport.IsStable = false;
            }
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }
    }
}
