using UnityEngine;
using XeptKit.Core;

namespace XeptGame.Player
{
    /// <summary>
    /// 相机效果目标契约（3C_CameraFeel_Design.md §2.3）：
    /// <see cref="CameraRig"/> 实现；效果源（HeadBob/LandingKick/FovController 及后续武器后坐力、震屏）
    /// 经契约写入，不依赖 Player 具体类型（可单测）。
    /// BaseRotation 不在契约内（PlayerLook 独占写，见设计决议）。
    /// </summary>
    public interface ICameraEffectTarget
    {
        /// <summary>当前累积位置偏移（本地空间；本帧应用后自动清零）。</summary>
        Vector3 PositionOffset { get; }

        /// <summary>当前累积旋转偏移（本地空间；本帧应用后自动重置）。</summary>
        Quaternion RotationOffset { get; }

        /// <summary>FOV 目标接收层（OverrideValue 多优先级，见设计决议 §3.3）。</summary>
        OverrideValue<float> Fov { get; }

        /// <summary>累加位置偏移（本地空间；多来源叠加，本帧应用后自动清零）。</summary>
        void AddPositionOffset(Vector3 offset);

        /// <summary>累乘旋转偏移（本地空间；多来源叠加，本帧应用后自动重置）。</summary>
        void AddRotationOffset(Quaternion offset);
    }
}
