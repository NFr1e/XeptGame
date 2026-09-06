using UnityEngine;

namespace XeptGame.UI
{
    /// <summary>
    /// 输入提示行视图（通用展示组件，纯展示不碰交互契约）：
    /// 显示一行"输入键 + 动作文案"，可用性灰态由外部驱动（<see cref="SetAvailable"/>）。
    /// Keyboard 键名经 Label 显示；Gamepad 键位以图标显示（后期做，Label 预留）。
    /// </summary>
    public sealed class InputPromptView : MonoBehaviour
    {
        [SerializeField] private Label keyLabel;
        [SerializeField] private Label promptLabel;

        private CanvasGroup _group;

        /// <summary>绑定展示内容（键名 + 动作文案），并复位可用态。</summary>
        public void Bind(string keyText, string promptText)
        {
            keyLabel?.SetText(keyText);
            promptLabel?.SetText(promptText);
            SetAvailable(true);
        }

        /// <summary>可用性灰态（alpha 表达；不可用 = 半透明）。</summary>
        public void SetAvailable(bool available)
        {
            if (_group == null)
            {
                _group = GetComponent<CanvasGroup>();
                if (_group == null)
                {
                    _group = gameObject.AddComponent<CanvasGroup>();
                }
            }

            _group.alpha = available ? 1f : 0.4f;
        }
    }
}
