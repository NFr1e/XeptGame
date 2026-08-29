using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XeptKit.Core;

namespace XeptKit.UI.Procedural
{
    /// <summary>
    /// 效果管线。与 ProceduralImage 同 GameObject，管理所有 BaseEffect 组件的注册、排序和按优先级执行。
    /// 主线程 only、无锁；全同步（方向文档 §2 决议）。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class EffectPipeline : MonoBehaviour
    {
        /// <summary>网格细分度硬上限。防第三方效果误设超大段数导致顶点爆炸（32 段 = 33×33 = 1089 顶点/效果）。</summary>
        public const int MaxSegments = 32;

        #region Fields

        private readonly List<BaseEffect> m_Effects = new List<BaseEffect>();
        private ProceduralImage m_ProceduralImage;
        private bool m_IsDirty = true;
#if UNITY_EDITOR
        // 效果 enabled 状态快照（Editor 轮询用）：编辑模式下 Behaviour.enabled 赋值
        // 不派发 OnEnable/OnDisable，须经快照比对检测变化并触发刷新（含 Undo 撤销路径）。
        private readonly Dictionary<BaseEffect, bool> m_EnabledSnapshot = new Dictionary<BaseEffect, bool>();
#endif

        #endregion

        #region Public Properties

        public IReadOnlyList<BaseEffect> Effects => m_Effects;

        public ProceduralImage ProceduralImage
        {
            get
            {
                if (m_ProceduralImage == null)
                    TryGetComponent(out m_ProceduralImage);
                return m_ProceduralImage;
            }
        }

        /// <summary>
        /// 获取所有效果中最大的网格细分度，clamp 到 [1, <see cref="MaxSegments"/>]。
        /// </summary>
        public int GetMaxRequiredSegments()
        {
            int max = 1;
            foreach (var effect in m_Effects)
            {
                if (effect != null && effect.isActiveAndEnabled)
                    max = Mathf.Max(max, effect.RequiredSegments);
            }
            return Mathf.Clamp(max, 1, MaxSegments);
        }

        /// <summary>
        /// 是否有任何激活效果需要自定义 Shader。
        /// </summary>
        public bool RequiresCustomShader
        {
            get
            {
                foreach (var effect in m_Effects)
                {
                    if (effect != null && effect.isActiveAndEnabled && effect.RequiresCustomShader)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 获取所有效果中最大的 falloff 距离（顶点向外扩展量）。
        /// </summary>
        public float GetFalloffDistance()
        {
            float maxFalloff = 0f;
            foreach (var effect in m_Effects)
            {
                if (effect != null && effect.isActiveAndEnabled)
                    maxFalloff = Mathf.Max(maxFalloff, effect.FalloffDistance);
            }
            return maxFalloff;
        }

        /// <summary>
        /// 获取所有效果中最大的抗锯齿距离。
        /// 初始 0：AA 距离低于 1 的声明（如 0.1-0.9）不被钳制；
        /// 无任何激活效果时返回 0（此时材质非 SDF，pixelScale 无渲染影响）。
        /// </summary>
        public float GetAntiAliasingDistance()
        {
            float maxDistance = 0f;
            foreach (var effect in m_Effects)
            {
                if (effect != null && effect.isActiveAndEnabled)
                    maxDistance = Mathf.Max(maxDistance, effect.AntiAliasingDistance);
            }
            return maxDistance;
        }

        #endregion

        #region Effect Registration

        internal void RegisterEffect(BaseEffect effect)
        {
            if (effect == null || m_Effects.Contains(effect))
                return;

            m_Effects.Add(effect);
            EnforceExclusiveGroups();   // 基于添加顺序仲裁（须先于 SortEffects）
            SortEffects();
            Invalidate();
        }

        internal void UnregisterEffect(BaseEffect effect)
        {
            if (effect == null) return;

            m_Effects.Remove(effect);
            Invalidate();
        }

        /// <summary>
        /// 互斥仲裁：同组（<see cref="EffectGroup"/> 非 None）效果只保留一个，
        /// 列表靠后者（后添加/后注册）生效，其余禁用并从列表移除。
        /// 须在 SortEffects 之前调用（基于添加顺序而非优先级顺序）。
        /// 编辑模式下设置 enabled 不派发 OnDisable，故手动移除列表项；
        /// Play 模式下 enabled = false 触发 OnDisable → UnregisterEffect 移除（RemoveAll 幂等）。
        /// </summary>
        private void EnforceExclusiveGroups()
        {
            if (m_Effects.Count < 2)
                return;

            HashSet<BaseEffect> toDisable = null;
            for (int i = 0; i < m_Effects.Count; i++)
            {
                BaseEffect e = m_Effects[i];
                if (e == null || e.ExclusiveGroup == EffectGroup.None)
                    continue;

                for (int j = i + 1; j < m_Effects.Count; j++)
                {
                    BaseEffect later = m_Effects[j];
                    if (later != null && later != e && later.ExclusiveGroup == e.ExclusiveGroup)
                    {
                        (toDisable ??= new HashSet<BaseEffect>()).Add(e);
                        break;
                    }
                }
            }

            if (toDisable == null)
                return;

            m_Effects.RemoveAll(x => x == null || toDisable.Contains(x));
        }

        private void SortEffects()
        {
            m_Effects.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                return a.Priority.CompareTo(b.Priority);
            });

            m_Effects.RemoveAll(e => e == null);
        }

        /// <summary>
        /// 重新扫描所有 BaseEffect 组件并刷新效果列表（含互斥仲裁）。
        /// </summary>
        public void RefreshEffects()
        {
            var found = GetComponents<BaseEffect>();
            m_Effects.Clear();
            m_Effects.AddRange(found);
            EnforceExclusiveGroups();   // 编辑模式效果经此路径进入列表，须执行互斥仲裁
            SortEffects();
        }

        #endregion

        #region Mesh Processing

        public void ProcessMesh(VertexHelper vh, Rect bounds)
        {
            if (m_IsDirty)
            {
                SortEffects();
                m_Effects.RemoveAll(e => e == null);
                m_IsDirty = false;
            }

            foreach (var effect in m_Effects)
            {
                if (effect != null && effect.isActiveAndEnabled)
                {
                    effect.ModifyMesh(vh, bounds);
                }
            }
        }

        #endregion

        #region Invalidation

        public void Invalidate()
        {
            m_IsDirty = true;

            var img = ProceduralImage;
            if (img != null)
            {
                img.SetVerticesDirty();
                img.SetMaterialDirty();
            }
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            TryGetComponent(out m_ProceduralImage);
            RefreshEffects();
            Invalidate();
        }

        private void OnDisable()
        {
            m_Effects.Clear();
            Invalidate();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            m_IsDirty = true;
        }

        /// <summary>
        /// Editor 轮询：检测效果组件的 enabled 状态变化（勾选/取消勾选、代码/Undo 修改等）。
        /// 编辑模式下设置 <see cref="Behaviour.enabled"/> 不派发 OnEnable/OnDisable 消息
        /// （与 Inspector 勾选不同），故与 enabled 快照比对检测变化——修正样本
        /// "found[i].enabled != m_Effects[i].enabled 恒 false"（二者为同一组件引用）。
        /// 仅在 Editor 且非 Play Mode 下运行，运行时零开销。
        /// </summary>
        private void Update()
        {
            if (Application.isPlaying) return;

            var found = GetComponents<BaseEffect>();

            bool changed = found.Length != m_Effects.Count;
            if (!changed)
            {
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] != null
                        && (!m_EnabledSnapshot.TryGetValue(found[i], out bool lastEnabled)
                            || lastEnabled != found[i].enabled))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            // 更新快照（始终执行，供下帧比对）
            m_EnabledSnapshot.Clear();
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                    m_EnabledSnapshot[found[i]] = found[i].enabled;
            }

            if (changed)
            {
                // 复用 RefreshEffects（含互斥仲裁），再触发宿主重建
                RefreshEffects();
                Invalidate();
            }
        }
#endif

        #endregion
    }
}
