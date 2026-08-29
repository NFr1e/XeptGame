using System;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using XeptKit.Core;

namespace XeptKit.Scenes
{
    /// <summary>
    /// 基于全屏黑场淡入淡出的默认场景过渡实现。
    /// 不依赖任何第三方动画库——纯 CanvasGroup 透明度插值。
    /// 依赖内置 UnityEngine.UI（ugui 包）；不使用 uGUI 的项目请自实现 <see cref="ISceneTransition"/>。
    /// </summary>
    /// <remarks>
    /// 首次使用时自动创建覆盖全屏的遮罩 Canvas（DontDestroyOnLoad）。
    /// EnterAsync / ExitAsync 抛异常时自动销毁已创建的遮罩（兑现"过渡失败自清理"接口约定）。
    /// </remarks>
    public sealed class FadeTransition : ISceneTransition, IDisposable
    {
        private readonly float _fadeDuration;
        private CanvasGroup _canvasGroup;
        private GameObject _overlayObject;
        private float _currentProgress;

        /// <summary>淡入淡出持续时间（秒）。</summary>
        public float FadeDuration => _fadeDuration;

        /// <inheritdoc />
        public float Progress => _currentProgress;

        /// <summary>
        /// 创建淡入淡出过渡。
        /// </summary>
        /// <param name="fadeDuration">单次淡入/淡出持续时间（秒）。</param>
        public FadeTransition(float fadeDuration = 0.5f)
        {
            Guard.InRange(fadeDuration, 0.01f, float.MaxValue, nameof(fadeDuration));
            _fadeDuration = fadeDuration;
        }

        /// <inheritdoc />
        public async UniTask EnterAsync()
        {
            EnsureOverlayCreated();

            _currentProgress = 0f;
            _canvasGroup.alpha = 0f;
            _overlayObject.SetActive(true);

            try
            {
                float elapsed = 0f;
                while (elapsed < _fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    _currentProgress = Mathf.Clamp01(elapsed / _fadeDuration);
                    _canvasGroup.alpha = _currentProgress;
                    await UniTask.Yield();
                }

                _canvasGroup.alpha = 1f;
                _currentProgress = 1f;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <inheritdoc />
        public async UniTask ExitAsync()
        {
            EnsureOverlayCreated();

            _currentProgress = 1f;
            _canvasGroup.alpha = 1f;

            try
            {
                float elapsed = 0f;
                while (elapsed < _fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    _currentProgress = 1f - Mathf.Clamp01(elapsed / _fadeDuration);
                    _canvasGroup.alpha = _currentProgress;
                    await UniTask.Yield();
                }

                _canvasGroup.alpha = 0f;
                _currentProgress = 0f;
                _overlayObject.SetActive(false);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>销毁遮罩 GameObject。</summary>
        public void Dispose()
        {
            if (_overlayObject != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_overlayObject);
                else
                    UnityEngine.Object.DestroyImmediate(_overlayObject);

                _overlayObject = null;
                _canvasGroup = null;
            }
        }

        private void EnsureOverlayCreated()
        {
            if (_overlayObject != null) return;

            // 创建全屏遮罩
            _overlayObject = new GameObject("[FadeTransition] Overlay");

            var canvas = _overlayObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767; // 最顶层
            canvas.vertexColorAlwaysGammaSpace = true;

            _overlayObject.AddComponent<CanvasScaler>();
            _overlayObject.AddComponent<GraphicRaycaster>();

            var imageGo = new GameObject("FadeImage");
            imageGo.transform.SetParent(_overlayObject.transform, worldPositionStays: false);

            var image = imageGo.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false; // 不阻挡点击

            var imageRect = imageGo.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.sizeDelta = Vector2.zero;
            imageRect.anchoredPosition = Vector2.zero;

            _canvasGroup = _overlayObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;

            // 确保切换场景时不销毁
            UnityEngine.Object.DontDestroyOnLoad(_overlayObject);
        }
    }
}
