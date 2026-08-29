using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XeptKit.Core;

namespace XeptKit.UI.Manager
{
    /// <summary>
    /// 框架自有的视图演员组件（用户不继承；罕见自定义动画场景可子类化逃生舱）。
    /// 职责：入场/出场动画编排（awaitable，经 <see cref="formTransition"/> 序列化引用的转场组件解析）、
    /// GameObject 激活、应用仲裁下发的层级/输入指令、实例终结、持有会话身份（<see cref="Handle"/> 注入）。
    /// 不自毁、不自算层级（决策权上收、执行权下放）。
    /// 手动挂载于表单根节点（缺失 fail-fast）；配置面：转场组件（挂 IFormTransition 子类组件并配置参数）、
    /// <see cref="cameraSpaceCanvas"/>（相机注入目标）。
    /// 属性 camelCase：组件为引擎侧类型，遵循 uGUI 原生惯例。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class UIForm : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour formTransition;

        /// <summary>
        /// 相机注入目标 Canvas（显式声明制）：非空时逐项**无条件**写入 <c>worldCamera = 管理器推送的 UI 相机</c>；空 = 不注入。
        /// 为何显式声明：未指定相机的 ScreenSpaceCamera 画布被 Unity 当作 Overlay 渲染——运行时 <see cref="Canvas.renderMode"/>
        /// 返回的是**生效模式**（ScreenSpaceOverlay）而非配置值（引擎警告：*A ScreenSpaceCanvas with no specified camera acts
        /// like an Overlay Canvas*），运行时 API 无法区分「退化的相机空间画布」与「真正的 Overlay 画布」，启发式过滤不可行。
        /// 支持「表单根即 Canvas」与「表单下多 Canvas（BackgroundCanvas/ContentCanvas）」两种架构：声明顶层相机空间画布即可。
        /// </summary>
        [Tooltip("相机注入目标 Canvas（显式声明制：非空即逐项注入管理器推送的相机，空则不注入）。")]
        public Canvas[] cameraSpaceCanvas;

        /// <summary>
        /// 本会话句柄（框架在会话开始时注入；休眠复用重开时重新注入新 ID）。视图会话身份——
        /// 外部持视图引用可经句柄关闭（<c>CloseAsync(Handle)</c>）；业务逻辑经 <see cref="FormLogicBase.View"/> 触达。
        /// 视图不提供关闭意图 API（纯被动演员）——关闭由业务表达（Manager.Close(View.Handle)）。
        /// </summary>
        public FormHandle Handle { get; internal set; }

        private CanvasGroup _canvasGroup;
        private CancellationTokenSource _coverCts;

        /// <summary>
        /// 播放入场；返回 = 完全可见、可交互。**激活 GameObject**（休眠复用与全新打开共用）。
        /// 转场组件经 <see cref="formTransition"/> 序列化引用解析（接口经其 TryGetComponent）；未配置 = 即时可见。
        /// token 供「中止开启」截断入场。
        /// </summary>
        public virtual UniTask EnterAsync(CancellationToken cancellationToken = default)
        {
            gameObject.SetActive(true);
            var group = GetCanvasGroup();

            if (formTransition != null && formTransition.TryGetComponent<IFormTransition>(out var t))
            {
                return t.EnterAsync(group, cancellationToken);
            }

            return UniTask.CompletedTask;
        }

        /// <summary>播放出场；返回 = 视觉终结（不自毁——终结由管理器 Terminate 执行）。未配置转场 = 即时（激活状态由 Terminate 管理）。</summary>
        public virtual UniTask ExitAsync(CancellationToken cancellationToken = default)
        {
            var group = GetCanvasGroup();

            if (formTransition != null && formTransition.TryGetComponent<IFormTransition>(out var t))
            {
                return t.ExitAsync(group, cancellationToken);
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 遮挡视图过渡（能力接口探测，fire-and-forget）：被遮/恢复时播放 <see cref="IFormCoverTransition.CoverAsync"/>/
        /// <see cref="IFormCoverTransition.RevealAsync"/>（如移动端 push 旧页让位/复位）。
        /// 快速翻转（开即关）时取消上一段（跳终态）→ 新动画从当前状态继续；无能力接口 = 视觉不动。
        /// </summary>
        internal void PlayCoverAnimation(bool covered)
        {
            var old = _coverCts;
            _coverCts = null;

            if (old != null)
            {
                old.Cancel();
                old.Dispose();
            }

            if (formTransition == null || !formTransition.TryGetComponent<IFormCoverTransition>(out var t))
            {
                return;
            }

            var cts = new CancellationTokenSource();

            _coverCts = cts;
            _ = RunCoverAnimationAsync(t, covered, cts);
        }

        private async UniTask RunCoverAnimationAsync(IFormCoverTransition t, bool covered, CancellationTokenSource cts)
        {
            try
            {
                if (covered)
                {
                    await t.CoverAsync(GetCanvasGroup(), cts.Token);
                }
                else
                {
                    await t.RevealAsync(GetCanvasGroup(), cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // 状态翻转中断——正常流程，静默（转场已按取消语义跳至最终状态）
            }
            catch (Exception ex)
            {
                Log.Exception(ex); // 转场异常隔离（通知型）
            }
            finally
            {
                if (ReferenceEquals(_coverCts, cts))
                {
                    _coverCts = null;
                    cts.Dispose();
                }
            }
        }

        /// <summary>
        /// 应用仲裁下发的渲染位置（组内 sibling 序——Group Root 模型下组根内只有表单，无索引冲突；
        /// 跨组顺序由组根 sibling 序（Depth）保证）。自带 Canvas 的表单视为用户显式组合，sortingOrder 由用户管理。
        /// </summary>
        internal void ApplyOrder(int order)
        {
            var parent = transform.parent;
            if (parent != null)
            {
                transform.SetSiblingIndex(order);
            }
        }

        /// <summary>应用仲裁下发的输入可达性（模态为界）：经 CanvasGroup.blocksRaycasts 开关。</summary>
        internal void ApplyInputEnabled(bool enabled)
        {
            GetCanvasGroup().blocksRaycasts = enabled;
        }

        /// <summary>
        /// 应用相机注入（显式声明制）：对 <see cref="cameraSpaceCanvas"/> 声明的每个画布写入管理器推送的 UI 相机。
        /// 无条件注入——声明即意图（幂等：已指向推送相机则无操作）；未声明 = 不注入（引擎无法区分退化与 Overlay，见字段注释）。
        /// 调用方（管理器）保证 camera 非空：降级（未推送相机）由管理器短路，不触达此处。
        /// </summary>
        internal void ApplyUICamera(Camera camera)
        {
            Guard.NotNullObject(camera, nameof(camera));

            if (cameraSpaceCanvas == null || cameraSpaceCanvas.Length == 0)
            {
                return; // 显式声明制：未声明相机空间画布即不注入
            }

            for (int i = 0; i < cameraSpaceCanvas.Length; i++)
            {
                var canvas = cameraSpaceCanvas[i];
                if (canvas == null)
                {
                    Log.Warning($"[XeptKit.UI.Manager] '{name}': 相机注入目标 [{i}] 为 null（数组引用失效）。");
                    continue;
                }

                canvas.worldCamera = camera;
            }
        }

        /// <summary>终结实例（销毁）。休眠（缓存）路径由管理器直接 SetActive(false) 完成，不走本方法。</summary>
        internal void Terminate()
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private CanvasGroup GetCanvasGroup()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            return _canvasGroup;
        }
    }
}
