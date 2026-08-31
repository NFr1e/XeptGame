using UnityEngine;

namespace XeptGame.Player
{
    /// <summary>
    /// 接地状态报告（电机无关抽象，替代 KCC GroundingStatus 的接地部分）。
    /// 语义：稳定接地（可站立）→ Grounded；探测到地面但不稳定 → UnstableGround；无地面 → Fall。
    /// </summary>
    public readonly struct MotorGroundState
    {
        /// <summary>探测到任何地面（UnstableGround 判定用）。</summary>
        public readonly bool FoundAnyGround;

        /// <summary>稳定接地（Grounded/UnstableGround 分界）。</summary>
        public readonly bool IsStableOnGround;

        /// <summary>接地法线（起跳方向、斜坡速度重定向）。</summary>
        public readonly Vector3 GroundNormal;

        public MotorGroundState(bool foundAnyGround, bool isStableOnGround, Vector3 groundNormal)
        {
            FoundAnyGround = foundAnyGround;
            IsStableOnGround = isStableOnGround;
            GroundNormal = groundNormal;
        }
    }

    /// <summary>
    /// 电机端口：决策层（HFSM 状态类）对底层电机的唯一依赖（端口-适配器结构，见设计决议 §5.2）。
    /// 接口最小化——只抽象状态类真正需要的电机能力，不出现任何 KCC 类型；换底层只替换适配器实现。
    /// 注意：速度不进接口——底层电机为"回调拉取"模型（UpdateVelocity(ref v)），
    /// 状态类求值方法直接修改 ref Vector3（纯引擎类型）。
    /// </summary>
    public interface IMotor
    {
        // —— 只读状态 ——

        /// <summary>本帧接地报告（电机探测完成后权威值）。</summary>
        MotorGroundState Ground { get; }

        /// <summary>角色上方向（起跳方向、速度平面投影）。</summary>
        Vector3 CharacterUp { get; }

        /// <summary>当前瞬态位置（起身 overlap、移动平台跟随）。</summary>
        Vector3 TransientPosition { get; }

        /// <summary>当前瞬态旋转。</summary>
        Quaternion TransientRotation { get; }

        /// <summary>当前实际速度（调试/信息显示；决策层速度求值走回调 ref 参数，不经此属性）。</summary>
        Vector3 Velocity { get; }

        /// <summary>
        /// 贴附移动平台在当前角色位置处的速度（含垂直分量）。
        /// 跳跃动量保留用：升降平台起跳需把平台垂直速度并入起跳冲量
        /// （否则 Fall 消费冲量时 Project 掉垂直分量 → Y 轴惯性丢失）。
        /// </summary>
        Vector3 AttachedRigidbodyVelocity { get; }

        /// <summary>
        /// 是否处于强制离地状态（ForceUnground 生效中：跳过接地探测/吸附）。
        /// 用于"起跳瞬间不误判落地"（电机 GroundingStatus 可能滞后一物理帧）。
        /// </summary>
        bool MustUnground { get; }

        /// <summary>稳定接地最大坡度角（度；超过则不稳定）。用于区分"坡面滑动"与"悬崖边缘/悬空"。</summary>
        float MaxStableSlopeAngle { get; }

        /// <summary>
        /// 当前接地碰撞体所在的 Layer 索引（无接地时为 -1）。
        /// 业务基于 StableGroundLayers 做"可站立/不可站立"互斥判定（见设计决议 §5.4）。
        /// </summary>
        int GroundColliderLayer { get; }

        /// <summary>可站立地面层（KCC StableGroundLayers）：接地对象层不在此集合内 = 不可站立（UnstableGround）。</summary>
        LayerMask StableGroundLayers { get; }

        // —— 操作 ——

        /// <summary>设置胶囊尺寸（Crouch 进出）。</summary>
        void SetCapsuleDimensions(float radius, float height, float yOffset);

        /// <summary>强制离地（跳跃；跳过下一帧的接地吸附/探测）。</summary>
        void ForceUnground(float time = 0.1f);

        /// <summary>
        /// 以给定胶囊尺寸在指定位置探测是否有碰撞（起身检查用）。
        /// 无副作用：内部临时切换尺寸探测后恢复。
        /// </summary>
        bool CharacterOverlapCheck(Vector3 position, Quaternion rotation,
            float radius, float height, float yOffset,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore);

        /// <summary>把方向投影到表面切向（斜坡速度重定向）。</summary>
        Vector3 GetDirectionTangentToSurface(Vector3 direction, Vector3 surfaceNormal);

        /// <summary>body 局部方向 → 世界方向（参考系 = body；移动意图等局部向量消费前转换）。</summary>
        Vector3 TransformDirection(Vector3 localDirection);
    }
}
