using UnityEngine;
using UnityEngine.UI;

namespace XeptGame.UI
{
    /// <summary>
    /// 提示图标视图（双通道：Sprite → Image，Texture → RawImage；按内容自动切换）：
    /// 支持 Item 的 IconKind 双形态（uGUI Image / RawImage）与任意宿主直给素材。
    /// 纯展示组件：只负责"设 Sprite/设 Texture/清空"，不解释内容。
    /// </summary>
    public sealed class PromptIconView : MonoBehaviour
    {
        [Tooltip("Sprite 通道（uGUI Image）；激活的图标类型只用一个通道")]
        [SerializeField] private Image image;

        [Tooltip("纹理通道（uGUI RawImage）")]
        [SerializeField] private RawImage rawImage;

        /// <summary>显示 Sprite 图标（关闭纹理通道）。</summary>
        public void ShowSprite(Sprite sprite)
        {
            if (image != null)
            {
                image.sprite = sprite;
                image.enabled = sprite != null;
            }

            if (rawImage != null)
            {
                rawImage.enabled = false;
            }
        }

        /// <summary>显示纹理图标（关闭 Sprite 通道）。</summary>
        public void ShowTexture(Texture texture)
        {
            if (image != null)
            {
                image.enabled = false;
            }

            if (rawImage != null)
            {
                rawImage.texture = texture;
                rawImage.enabled = texture != null;
            }
        }

        /// <summary>清空并隐藏图标（无图标宿主用）。</summary>
        public void Clear()
        {
            if (image != null)
            {
                image.enabled = false;
            }

            if (rawImage != null)
            {
                rawImage.enabled = false;
            }
        }
    }
}
