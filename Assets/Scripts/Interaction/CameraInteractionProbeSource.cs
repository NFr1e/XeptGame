using UnityEngine;
using XeptKit.Core;

namespace XeptGame.Interaction
{
    /// <summary>
    /// 相机探测源（<see cref="IInteractionProbeSource"/> 默认实现）：
    /// 从玩家相机位置沿前向探测——第一人称交互的标准来源。
    /// 纯 C# 类，由消费方（InteractionDetector）在 Awake 创建并持有；
    /// 未来 AI/过场等交互器可自行实现 <see cref="IInteractionProbeSource"/>。
    /// </summary>
    public sealed class CameraInteractionProbeSource : IInteractionProbeSource
    {
        private readonly Transform _cameraTransform;

        /// <summary>构造并校验相机 Transform 非空（Unity 假 null 语义）。</summary>
        public CameraInteractionProbeSource(Transform cameraTransform)
        {
            Guard.NotNullObject(cameraTransform, nameof(cameraTransform));
            _cameraTransform = cameraTransform;
        }

        /// <inheritdoc />
        public InteractionProbeRay GetProbeRay() => new(
            _cameraTransform.position, 
            _cameraTransform.forward);
    }
}
