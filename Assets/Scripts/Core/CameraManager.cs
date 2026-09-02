using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace XeptGame
{
    /// <summary>
    /// 相机栈角色：Base（基础相机，接收 Overlay 入栈）/ Overlay（叠加相机，推入所有 Base 的栈）。
    /// 由 CameraNotifier 判定（默认从 UniversalAdditionalCameraData.cameraType 读，单一事实源）。
    /// </summary>
    public enum CameraStackRole
    {
        Base,
        Overlay,
    }

    /// <summary>
    /// 跨场景相机栈管理器（URP，GameplayFlow_Design.md §2.2）：
    /// 持有当前所有已注册的 Base/Overlay 相机（CameraNotifier 在 OnEnable/OnDisable 自报），
    /// 按角色仲裁——将全部存活 Overlay 相机（按 stackPriority 升序，高值后渲染）推入每个 Base 相机的栈
    /// （**幂等重建**：清空重建，免疫重复 Add 污染与已销毁相机的脏引用）。
    /// 跨场景天然工作：相机进场景自报，无需序列化跨场景引用（URP 相机栈的序列化引用无法跨场景）。
    /// 经 AppEntry 组合根装配（AppEntry.CameraManager）；与 UIManager 正交——栈归本类，
    /// canvas 渲染上下文/相机注入归 UIManager（SetUIContext / ApplyUICamera）。
    /// </summary>
    public sealed class CameraManager
    {
        private readonly List<Camera> _baseCameras = new();
        private readonly List<OverlayEntry> _overlayCameras = new();

        private readonly struct OverlayEntry
        {
            public readonly Camera Camera;
            public readonly int Priority;

            public OverlayEntry(Camera camera, int priority)
            {
                Camera = camera;
                Priority = priority;
            }
        }

        /// <summary>
        /// 注册相机（CameraNotifier 判定角色后调用）：入对应注册表并重建全部栈。
        /// Overlay 重复注册刷新优先级（幂等：先移除旧条目）；Base 重复注册忽略。
        /// </summary>
        public void Register(CameraStackRole role, Camera camera, int stackPriority = 0)
        {
            switch (role)
            {
                case CameraStackRole.Base:
                    if (!_baseCameras.Contains(camera))
                    {
                        _baseCameras.Add(camera);
                    }
                    break;

                case CameraStackRole.Overlay:
                    RemoveOverlay(camera); // 幂等：重复注册刷新优先级
                    _overlayCameras.Add(new OverlayEntry(camera, stackPriority));
                    break;
            }

            RebindAll();
        }

        /// <summary>注销相机（OnDisable/销毁触发）：从注册表移除并重建栈（假 null 条目顺带清理）。</summary>
        public void Unregister(Camera camera)
        {
            bool removed = _baseCameras.Remove(camera);
            removed |= RemoveOverlay(camera);
            removed |= _baseCameras.RemoveAll(c => c == null) > 0; // 防御：已销毁 Base 顺带清理

            if (removed)
            {
                RebindAll();
            }
        }

        /// <summary>清空注册表（应用收尾 AppEntry.Shutdown 调用）。</summary>
        public void Clear()
        {
            _baseCameras.Clear();
            _overlayCameras.Clear();
        }

        private bool RemoveOverlay(Camera camera)
        {
            for (int i = 0; i < _overlayCameras.Count; i++)
            {
                if (_overlayCameras[i].Camera == camera)
                {
                    _overlayCameras.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 幂等重建：对每个存活 Base 相机，栈 = 排序后的存活 Overlay（清空重建——
        /// 免疫重复 Add 污染与已销毁相机的脏引用）。排序：stackPriority **升序**——
        /// 低优先级先渲染，高优先级（UI 相机设高值）在栈尾最后绘制，盖在最上层。
        /// </summary>
        private void RebindAll()
        {
            if (_baseCameras.Count == 0)
            {
                return;
            }

            var ordered = new List<OverlayEntry>(_overlayCameras);
            ordered.Sort((a, b) => a.Priority.CompareTo(b.Priority)); // 升序：高优先级后渲染

            foreach (var baseCam in _baseCameras)
            {
                if (baseCam == null)
                {
                    continue; // 防御：已销毁 Base（正常路径 OnDisable 已注销）
                }

                var stack = baseCam.GetUniversalAdditionalCameraData().cameraStack;
                stack.Clear(); // 重建而非增量：幂等 + 清脏引用

                foreach (var overlay in ordered)
                {
                    if (overlay.Camera != null && overlay.Camera != baseCam)
                    {
                        stack.Add(overlay.Camera);
                    }
                }
            }
        }
    }
}
