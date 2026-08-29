using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using XeptGame;

namespace XeptKit.UI.Manager
{
    [RequireComponent(typeof(Button))]
    public class OpenFormButton : MonoBehaviour
    {
        [SerializeField] private FormEntry formEntry;

        private Button _button;
        private IUIManager _uiManager;

        private void OnEnable()
        {
            GetButton().onClick.AddListener(OpenForm);
        }
        private void OnDisable()
        {
            GetButton().onClick.RemoveListener(OpenForm);
        }

        private Button GetButton()
        {
            if(!_button)
                _button = GetComponent<Button>();

            return _button;
        }

        private void OpenForm()
        {
            _uiManager ??= AppEntry.UIManager;

            _uiManager.OpenAsync(formEntry).Forget();
        }
    }
}