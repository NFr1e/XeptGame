using UnityEngine;
using UnityEngine.UI;
using XeptKit.UI.Manager;
using Cysharp.Threading.Tasks;

namespace XeptGame
{
    [RequireComponent(typeof(Button))]
    public class CloseFormButton : MonoBehaviour
    {
        [SerializeField] private UIForm form;

        private Button _button;
        private IUIManager _uiManager;

        private void OnEnable()
        {
            GetButton().onClick.AddListener(CloseForm);
        }
        private void OnDisable()
        {
            GetButton().onClick.RemoveListener(CloseForm);
        }

        private Button GetButton()
        {
            if (!_button)
                _button = GetComponent<Button>();

            return _button;
        }

        private void CloseForm()
        {
            _uiManager ??= AppEntry.UIManager;
            _uiManager.CloseAsync(form.Handle).Forget();
        }
    }
}
