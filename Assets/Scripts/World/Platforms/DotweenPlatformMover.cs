using DG.Tweening;
using KinematicCharacterController;
using UnityEngine;

namespace XeptGame.World
{
    /// <summary>
    /// DOTween 驱动的往返移动/旋转平台（<see cref="IMoverController"/> 实现）。
    ///
    /// 职责分离（KCC 移动平台机制，见设计决议 §3/示例）：
    /// - DOTween 只插值数值变量 _t（0→1→0 往返，**不直接驱动 transform**）——
    ///   否则与 PhysicsMover（FixedUpdate 写 transform）双写冲突；
    /// - PhysicsMover 每物理帧经 <see cref="UpdateMovement"/> 读取 _t 作为目标位姿，
    ///   反推速度并驱动角色跟随（站立被带着走/转、跳跃保留平台动量、边缘滑落均由 KCC 完成）。
    ///
    /// 旋转说明：平台旋转时 KCC 会把角色 body 一并旋转（电机旋转跟随机制，
    /// 物理必需——否则角色会被甩出/穿过）。旋转跟随与 FPS 视角决议（body 不随视角转）
    /// 不冲突：后者是视角行为，平台旋转是物理行为。
    ///
    /// 时序：DOTween 用 <see cref="UpdateType.Fixed"/> 与物理帧同步，
    /// 避免 UpdateMovement（FixedUpdate 读取）读到滞后的 Update 值。
    ///
    /// 场景装配：平台 GameObject 挂 Rigidbody（isKinematic 由 PhysicsMover 强制）+
    /// BoxCollider（非 Trigger）+ PhysicsMover + 本组件；平台 Layer 须在
    /// CollidableLayers 与 StableGroundLayers（可站立）。
    /// 注意：PhysicsMover.MoverController 为 [NonSerialized]，本组件 Awake 自动注入。
    /// </summary>
    public class DotweenPlatformMover : MonoBehaviour, IMoverController
    {
        [Header("运动")]
        [Tooltip("往返终点位置偏移（相对起始位置；零向量=仅旋转）")]
        public Vector3 endOffset = new Vector3(0f, 0f, 5f);

        [Tooltip("往返终点旋转偏移（欧拉角，度；零=仅平移）")]
        public Vector3 endRotation = Vector3.zero;

        [Tooltip("单程时长（秒）")]
        public float duration = 2f;

        [Tooltip("往返缓动曲线")]
        public Ease ease = Ease.InOutSine;

        private Vector3 _startPosition;
        private Vector3 _startEuler;
        private float _t;
        private Tween _tween;

        private void Awake()
        {
            // PhysicsMover.MoverController 为 [NonSerialized]，需代码注入
            var mover = GetComponent<PhysicsMover>();
            if (mover != null)
            {
                mover.MoverController = this;
            }

            _startPosition = transform.position;
            _startEuler = transform.eulerAngles;

            // 插值数值变量 _t（不动 transform）；SetUpdate(Fixed) 与物理帧同步
            _tween = DOTween.To(() => _t, x => _t = x, 1f, duration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(ease)
                .SetUpdate(UpdateType.Fixed);
        }

        private void OnDestroy()
        {
            _tween?.Kill();
            _tween = null;
        }

        /// <inheritdoc />
        public void UpdateMovement(out Vector3 goalPosition, out Quaternion goalRotation, float deltaTime)
        {
            goalPosition = Vector3.Lerp(_startPosition, _startPosition + endOffset, _t);
            goalRotation = Quaternion.Euler(_startEuler + endRotation * _t);
        }
    }
}
