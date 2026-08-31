using KinematicCharacterController;
using UnityEngine;
using XeptKit.Core;

namespace XeptGame.Player
{
    /// <summary>
    /// KCC 电机端口适配器：把 <see cref="KinematicCharacterMotor"/> 映射为 <see cref="IMotor"/> 抽象。
    /// 端口-适配器结构（设计决议 §5.2）：决策层只依赖 IMotor，换底层时仅替换本类。
    /// 内部持有 KCC 电机并做接地报告映射、胶囊尺寸切换、重叠探测（隐藏复用缓冲数组）。
    /// </summary>
    public sealed class KccMotorAdapter : IMotor
    {
        private readonly KinematicCharacterMotor _motor;
        private readonly Collider[] _overlapBuffer = new Collider[8];

        public KccMotorAdapter(KinematicCharacterMotor motor)
        {
            Guard.NotNullObject(motor, nameof(motor));
            _motor = motor;
        }

        // —— 只读状态 ——

        public MotorGroundState Ground => new MotorGroundState(
            _motor.GroundingStatus.FoundAnyGround,
            _motor.GroundingStatus.IsStableOnGround,
            _motor.GroundingStatus.GroundNormal);

        public Vector3 CharacterUp => _motor.CharacterUp;
        public Vector3 TransientPosition => _motor.TransientPosition;
        public Quaternion TransientRotation => _motor.TransientRotation;
        public Vector3 Velocity => _motor.Velocity;
        public Vector3 AttachedRigidbodyVelocity => _motor.AttachedRigidbodyVelocity;
        public bool MustUnground => _motor.MustUnground();
        public float MaxStableSlopeAngle => _motor.MaxStableSlopeAngle;

        /// <inheritdoc />
        public int GroundColliderLayer
            => _motor.GroundingStatus.GroundCollider != null
                ? _motor.GroundingStatus.GroundCollider.gameObject.layer
                : -1;

        /// <inheritdoc />
        public LayerMask StableGroundLayers => _motor.StableGroundLayers;

        // —— 操作 ——

        public void SetCapsuleDimensions(float radius, float height, float yOffset)
            => _motor.SetCapsuleDimensions(radius, height, yOffset);

        public void ForceUnground(float time = 0.1f)
            => _motor.ForceUnground(time);

        public Vector3 GetDirectionTangentToSurface(Vector3 direction, Vector3 surfaceNormal)
            => _motor.GetDirectionTangentToSurface(direction, surfaceNormal);

        public Vector3 TransformDirection(Vector3 localDirection)
            => _motor.TransientRotation * localDirection;

        public bool CharacterOverlapCheck(Vector3 position, Quaternion rotation,
            float radius, float height, float yOffset,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore)
        {
            var capsule = _motor.Capsule;
            var oldRadius = capsule.radius;
            var oldHeight = capsule.height;
            var oldCenterY = capsule.center.y;

            _motor.SetCapsuleDimensions(radius, height, yOffset);
            int hitCount = _motor.CharacterOverlap(position, rotation, _overlapBuffer, _motor.CollidableLayers, triggerInteraction);

            // 恢复原尺寸（无副作用）
            _motor.SetCapsuleDimensions(oldRadius, oldHeight, oldCenterY);

            return hitCount > 0;
        }
    }
}
