using System;
using UnityEngine;
using XeptKit.Core;

namespace XeptGame.UI.Billboard
{
    /// <summary>
    /// 世界空间 Billboard 效果器（实现一——表现层重组决议：世界 Quad + WorldMarker.shader GPU 顶点 billboard）：
    /// 在**锚点位置**以广告牌效果渲染图标贴片——位置 = 锚点 Transform（零业务：偏移/高度语义由上层给到指定锚点
    /// 挂点）；显隐/染色/淡入由业务经 <see cref="SetVisible"/> / <see cref="SetTint"/> / <see cref="SetAlpha"/> 驱动。
    /// **效果配置（字段直给，宿主单点写 MPB）**——评估结论：效果本质是 shader/MPB 能力面、每属性单一写者，
    /// 无 Procedural 那种"多效果共同产出网格"的聚合需求，组件族/Pipeline 属提前抽象；组合自由在属性端已表达：
    /// <list type="bullet">
    /// <item>深度测试 <see cref="depthTestEnabled"/>：开 = 参与（可被墙挡，ZTest LEqual）/ 关 = 顶层常显；</item>
    /// <item>尺寸 = 世界大小 <see cref="worldSize"/>（透视近大远小；**恒定屏幕大小已决议删除，不保留**）；</item>
    /// <item>面向相机：billboard 定义性几何（view 空间铺 Quad），shader 恒开，不做可关效果。</item>
    /// </list>
    /// **编辑器即时反馈（参考 XeptKit.UI.Procedural 的 ExecuteAlways 哲学）**：本组件 [ExecuteAlways]——
    /// 编辑态改动（icon/depthTest/worldSize/锚点）即刷（OnValidate → Refresh；LateUpdate 每帧兜底）。
    /// 编辑态**跟随锚点仅在位置不一致时快照一次**（锚点 ≠ 自身时；防每帧写 transform 把场景标脏/undo 刷屏）；
    /// 播放态每帧跟随。MPB 写入不标脏场景（运行时数据），材质分配只在变化时赋值。
    /// 渲染资源自举：Quad 网格（内置）+ 共享材质（WorldMarker.shader 单例懒建），差异全走 MPB——零外部引用。
    /// **深度测试经双共享材质切换**：`ZTest` 是渲染状态（材质级），MaterialPropertyBlock 只能覆盖 shader uniform、
    /// 无法驱动渲染状态——故按 <see cref="depthTestEnabled"/> 在"参与（LEqual）/"顶层常显（Off）"两份共享材质间
    /// 切换 renderer.sharedMaterial；其余效果（icon/tint/alpha/世界尺寸）为 uniform，仍单点写 MPB。
    /// 不负责任何内容/显隐决策——内容（图标）与显隐/淡入时机归上层业务。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [ExecuteAlways]
    [DefaultExecutionOrder(1000)]
    public sealed class WorldBillboard : MonoBehaviour, IBillboard
    {
        // MPB 属性 id（与 WorldMarker.shader 对齐；静态懒初始化）
        private static readonly int IconId = Shader.PropertyToID("_IconTex");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int WorldSizeId = Shader.PropertyToID("_WorldSize");
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest"); // 材质级（双共享材质切换用）

        [SerializeField] private Transform anchor;
        [SerializeField] private Texture defaultIcon;
        
        [Space]
        [SerializeField] private bool depthTestEnabled = true;
        [SerializeField] private float worldSize = 0.4f;

        private static Material _sharedMaterialDepth;
        private static Material _sharedMaterialNoDepth;

        private MeshFilter _meshFilter;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;

        private Transform _runtimeAnchor;
        private bool _hasRuntimeAnchor;
        private Color _tint = Color.white;
        private float _alpha = 1f;

        public Transform BillboardAnchor => _hasRuntimeAnchor ? _runtimeAnchor : (anchor != null ? anchor : transform);
        public Transform CurrentAnchor => _hasRuntimeAnchor ? _runtimeAnchor : anchor;

        /// <inheritdoc />
        public event Action<Transform> AnchorChanged;

        /// <inheritdoc />
        public void SetAnchor(Transform worldAnchor)
        {
            _runtimeAnchor = worldAnchor;
            _hasRuntimeAnchor = true;
            AnchorChanged?.Invoke(worldAnchor);
        }

        /// <inheritdoc />
        public void ClearAnchor()
        {
            _runtimeAnchor = null;
            _hasRuntimeAnchor = false;
            AnchorChanged?.Invoke(null);
        }

        private void Awake()
        {
            EnsureRefs();
            EnsureRenderResources();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            EnsureRefs();
            if (_renderer == null || _meshFilter == null)
            {
                return;
            }

            EnsureRenderResources();
            Refresh();
        }

        private void LateUpdate()
        {
            if (_renderer == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                var current = BillboardAnchor;
                if (current != null)
                {
                    transform.position = current.position;
                }
            }
            else
            {
                // 编辑态：仅在锚点（非自身）与当前位置不一致时快照一次——防每帧写 transform 标脏场景/undo 刷屏
                EditModeFollowAnchor();
            }

            Refresh();
        }

        /// <summary>编辑态跟随：锚点 ≠ 自身且位置不一致 → 同步一次（用户拖动锚点/标记物时随动，静止时零写入）。</summary>
        private void EditModeFollowAnchor()
        {
            var current = BillboardAnchor;
            if (current == null || current == transform)
            {
                return; // 自身为锚点：位置由场景作者直接摆放，组件不写
            }

            if ((transform.position - current.position).sqrMagnitude > 0.0001f)
            {
                transform.position = current.position;
            }
        }

        /// <summary>表现刷新：双材质切换（ZTest 渲染状态）+ 单点写 MPB（uniform）。
        /// 渲染排序（sortingLayerName/sortingOrder）已分离为通用组件 <see cref="XeptGame.World.RendererSorting"/>（组合挂载，不在此）。</summary>
        private void Refresh()
        {
            var target = depthTestEnabled ? _sharedMaterialDepth : _sharedMaterialNoDepth;
            if (target != null && _renderer.sharedMaterial != target)
            {
                _renderer.sharedMaterial = target;
            }

            ApplyState();
        }

        /// <summary>显隐（业务驱动；淡入淡出经 <see cref="SetAlpha"/> 由业务动画）。</summary>
        public void SetVisible(bool visible)
        {
            if (_renderer != null)
            {
                _renderer.enabled = visible;
            }
        }

        /// <summary>图标（运行期可换；下帧 ApplyState 提交）。</summary>
        public void SetIcon(Texture texture)
        {
            defaultIcon = texture;
        }

        /// <summary>染色（MPB，配合透明图标/白色回退纹理上色）。</summary>
        public void SetTint(Color color)
        {
            _tint = color;
        }

        /// <summary>透明度（MPB alpha；业务驱动淡入淡出）。</summary>
        public void SetAlpha(float alpha)
        {
            _alpha = alpha;
        }

        private void EnsureRefs()
        {
            if (_meshFilter == null)
            {
                _meshFilter = GetComponent<MeshFilter>();
            }
            if (_renderer == null)
            {
                _renderer = GetComponent<MeshRenderer>();
            }
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }
        }

        /// <summary>渲染资源自举：Quad 网格 + 双共享材质（WorldMarker.shader——ZTest LEqual / Off 各一）。</summary>
        private void EnsureRenderResources()
        {
            if (_meshFilter == null || _renderer == null)
            {
                return;
            }

            if (_meshFilter.sharedMesh == null)
            {
                _meshFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            }

            if (_sharedMaterialDepth == null)
            {
                var shader = Shader.Find("XeptGame/UI/WorldMarker");
                if (shader == null)
                {
                    Log.Error("[WorldBillboard] 未找到 shader 'XeptGame/UI/WorldMarker'——广告牌不可见。");
                    return;
                }

                // 深度测试是渲染状态（材质级），MPB 无法驱动 → 双共享材质：LEqual（参与，shader 默认）与 Off（顶层常显）
                _sharedMaterialDepth = new Material(shader); // 默认 _ZTest = 4（LEqual）
                _sharedMaterialNoDepth = new Material(shader);
                _sharedMaterialNoDepth.SetFloat(ZTestId, 0f); // ZTest Off
            }

            if (_renderer.sharedMaterial != _sharedMaterialDepth)
            {
                _renderer.sharedMaterial = _sharedMaterialDepth;
            }
        }

        /// <summary>提交 MPB（字段直给：宿主单点写全部 uniform 效果属性，每帧幂等；ZTest 渲染状态经双材质切换，不在此）。</summary>
        private void ApplyState()
        {
            _renderer.GetPropertyBlock(_block);
            // MPB.SetTexture 不接受 null（与 material 不同，会抛 ArgumentNullException）——
            // icon 为空时回退内置白纹理（对齐 shader _IconTex 默认 white；业务经 SetTint 上色）
            _block.SetTexture(IconId, defaultIcon != null ? defaultIcon : Texture2D.whiteTexture);
            _block.SetColor(TintId, _tint);
            _block.SetFloat(AlphaId, _alpha);
            _block.SetFloat(WorldSizeId, worldSize);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
