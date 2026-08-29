using UnityEngine;
using UnityEngine.UI;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// 效果互斥组。
    /// 同一 GameObject 上同组效果最多保留一个（后添加的替换先添加的）。
    /// </summary>
    public enum EffectGroup
    {
        /// <summary>无互斥约束</summary>
        None,
        /// <summary>形状效果互斥（UIRoundedCorners / UISquircleCorners）</summary>
        Shape
    }

    /// <summary>
    /// 程序化图像效果基类。所有视觉效果（渐变、圆角、描边等）均继承此类。
    /// 通过 OnEnable/OnDisable 自动向 EffectPipeline 注册/注销自身。
    /// 优先级区间约定：0-9 填充 / 10-49 变换 / 50-59 形状 / 60-69 描边 / 70+ 预留。
    /// </summary>
    [RequireComponent(typeof(EffectPipeline))]
    public abstract class BaseEffect : MonoBehaviour
    {
        /// <summary>执行优先级。值越小越先执行。0=填充，50=形状，60=描边</summary>
        public abstract int Priority { get; }

        /// <summary>所需网格细分度。1=无细分，值越大顶点越密</summary>
        public abstract int RequiredSegments { get; }

        /// <summary>是否需要自定义 Shader（GPU SDF）</summary>
        public virtual bool RequiresCustomShader => false;

        /// <summary>顶点向外扩展距离（像素），用于 AA/描边避免裁剪</summary>
        public virtual float FalloffDistance => 0f;

        /// <summary>
        /// 抗锯齿距离（像素），值越大边缘越柔和。
        /// 默认 0 = 无 AA 需求（不拉高管线聚合值）；需要软边的效果覆写为实际值。
        /// </summary>
        public virtual float AntiAliasingDistance => 0f;

        /// <summary>
        /// 互斥组标识。同一 GameObject 上同组（非 None）效果最多保留一个，
        /// 后注册的替换先注册的。
        /// </summary>
        public virtual EffectGroup ExclusiveGroup => EffectGroup.None;

        /// <summary>修改顶点网格。管线在每个 Effect 上按优先级依次调用</summary>
        public abstract void ModifyMesh(VertexHelper vh, Rect bounds);

        /// <summary>
        /// Editor 销毁前清理钩子。
        /// Undo.DestroyObjectImmediate 不触发 OnDisable/OnDestroy，
        /// MenuItems.RemoveEffect 在销毁前调用此方法以重置效果特定状态（如 Shader 属性）。
        /// </summary>
        public virtual void CleanupBeforeDestroy() { }

        #region Helpers

        protected void Invalidate()
        {
            if (TryGetComponent(out EffectPipeline pipeline))
                pipeline.Invalidate();
        }

        protected ProceduralImage GetProceduralImage()
        {
            return TryGetComponent(out EffectPipeline pipeline) ? pipeline.ProceduralImage : null;
        }

        #endregion

        #region Editor

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            Invalidate();
        }
#endif

        #endregion

        #region Unity Lifecycle

        protected virtual void OnEnable()
        {
            if (TryGetComponent(out EffectPipeline pipeline))
                pipeline.RegisterEffect(this);
        }

        protected virtual void OnDisable()
        {
            if (TryGetComponent(out EffectPipeline pipeline))
                pipeline.UnregisterEffect(this);
        }

        protected virtual void OnDestroy()
        {
            if (TryGetComponent(out EffectPipeline pipeline))
                pipeline.UnregisterEffect(this);
        }

        #endregion
    }
}
