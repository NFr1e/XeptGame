using UnityEngine;
using XeptKit.Core;

namespace XeptGame.Core
{
    /// <summary>
    /// 渲染排序通用件（薄组件，Renderer 基类属性 sortingLayerName/sortingOrder 的序列化配置器——非 SpriteRenderer
    /// 独有，任何 Renderer（Mesh/Sprite/Skinned…）皆可挂）：把"排序层 + 层内序"从各效果宿主（如 WorldBillboard）
    /// 分离为正交的可组合组件——同对象挂多个渲染器叠层（图标/光晕…）时各配各的排序。
    /// **编辑即时反馈**：ExecuteAlways——Awake/OnEnable/OnValidate（Inspector 改动/Undo）时应用，仅变化时写入
    /// （防把场景标脏/undo 刷屏）。不解释渲染/显隐/内容——纯 Renderer 表现配置。
    /// 注：Renderer 为抽象类，无法 [RequireComponent] 自动添加——本组件自行 GetComponent 解析，缺失仅警告。
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class RendererSorting : MonoBehaviour
    {
        [Tooltip("排序层（sortingLayerName；默认 Default；多渲染器跨层叠序用）")]
        [SerializeField] private string sortingLayerName = "Default";

        [Tooltip("层内排序（sortingOrder；同层透明对象按此定序叠层）")]
        [SerializeField] private int sortingOrder;

        private Renderer _renderer;

        /// <summary>目标渲染器（自身组件；无则 null）。</summary>
        public Renderer TargetRenderer
        {
            get
            {
                if (_renderer == null)
                {
                    _renderer = GetComponent<Renderer>();
                }
                return _renderer;
            }
        }

        private void Awake()
        {
            EnsureRenderer();
            Apply();
        }

        private void OnEnable()
        {
            EnsureRenderer();
            Apply();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return; // 播放态由属性/外部驱动；编辑态改动即刷
            }

            if (!EnsureRenderer())
            {
                return; // 反序列化中途（Renderer 未就绪）
            }

            Apply();
        }

        /// <summary>运行期改排序层（应用并广播到渲染器）。</summary>
        public void SetSortingLayer(string layerName)
        {
            sortingLayerName = string.IsNullOrEmpty(layerName) ? "Default" : layerName;
            Apply();
        }

        /// <summary>运行期改层内序（应用并广播到渲染器）。</summary>
        public void SetSortingOrder(int order)
        {
            sortingOrder = order;
            Apply();
        }

        /// <summary>应用（仅变化时写入——编辑态防标脏；属性比对避免无谓赋值）。</summary>
        private void Apply()
        {
            if (!EnsureRenderer())
            {
                return;
            }

            if (_renderer.sortingLayerName != sortingLayerName)
            {
                _renderer.sortingLayerName = sortingLayerName;
            }
            if (_renderer.sortingOrder != sortingOrder)
            {
                _renderer.sortingOrder = sortingOrder;
            }
        }

        private bool EnsureRenderer()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<Renderer>();
                if (_renderer == null)
                {
                    Log.Warning("[RendererSorting] 目标对象无 Renderer（Mesh/Sprite/Skinned 等）——排序配置不生效。");
                    return false;
                }
            }
            return true;
        }
    }
}
