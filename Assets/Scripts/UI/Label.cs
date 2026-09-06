using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;
using System;
using XeptKit.Core;

namespace XeptGame.UI
{
    public enum LabelType
    {
        Legacy,
        TextMeshPro
    }

    public class Label : MonoBehaviour
    {
        [SerializeField] private LabelType labelType = LabelType.TextMeshPro;

        [SerializeField, ShowIf(nameof(labelType), LabelType.Legacy)] private Text legacyText;
        [SerializeField, ShowIf(nameof(labelType), LabelType.TextMeshPro)] private TextMeshProUGUI textMeshPro;

        public event Action<string> OnTextChanged;

        public void SetText(string text)
        {
            Guard.NotNull(text, nameof(text));

            switch (labelType)
            {
                case LabelType.Legacy:
                    if (legacyText != null)
                    {
                        legacyText.text = text;
                    }
                    break;

                case LabelType.TextMeshPro:
                    if (textMeshPro != null)
                    {
                        textMeshPro.text = text;
                    }
                    break;
            }

            OnTextChanged?.Invoke(text);
        }
    }
}
