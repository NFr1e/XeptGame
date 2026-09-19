using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 地面候选策略（决策层与适配层共用；纯判定、无状态）：
    /// **动态刚体 = 可推动道具 ⇒ 不是地面**（"单向碰撞"的判定核心，设计决议 §5.5）。
    /// 语义：命中动态刚体仍会**阻挡移动**并**参与推动**（KCC 移动 sweep 与交互刚体处理照旧），
    /// 但**不作为地面**——不吸附、`IsStableOnGround=false`、不触发着陆事件、也不进入"可滑面"语义。
    /// **kinematic 刚体不算**（`PhysicsMover` 移动平台必须仍可站立）；静态碰撞体不算（普通地面/墙）。
    /// 消费点：
    /// ① <see cref="PlayerCharacterController.ProcessHitStabilityReport"/>（KCC 原生"逐命中稳定性"钩子）；
    /// ② <see cref="KccMotorAdapter.GroundIsDynamicBody"/> → <see cref="PlayerMotorContext"/> 谓词
    /// （把道具从"可滑面"里排除）。
    /// 与 KCC 自身语义一致：KCC 的 step-up 也已拒绝动态刚体（"Stepping not supported on dynamic rigidbodies"）。
    /// </summary>
    public static class MotorGroundPolicy
    {
        /// <summary>该碰撞体是否可作为地面？动态刚体（可推动道具）返回 false；kinematic/静态返回 true。</summary>
        public static bool IsStandableSurface(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            var body = collider.attachedRigidbody;
            return body == null || body.isKinematic;
        }
    }
}
