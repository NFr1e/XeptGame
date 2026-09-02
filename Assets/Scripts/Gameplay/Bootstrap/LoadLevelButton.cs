using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using XeptKit.Core;
using XeptKit.Scenes;

namespace XeptGame.Gameplay
{
    /// <summary>
    /// 进入关卡按钮（测试/验证组件，GameplayFlow_Design.md §4.1）：点击发起开始游戏请求
    /// （<see cref="GameLoadingManager.RequestStart"/>——只发请求，逻辑完全由状态机/加载编排负责）。
    /// 典型用途：**Editor 直接 Play 基座场景**（无 AppEntryBoot/LevelBoot 自动请求源）时，Boot 停驻后
    /// 点击本按钮模拟"进入关卡"动作，验证加载链条完整性；未来 MainMenu "开始游戏"按钮即此形态。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class LoadLevelButton : MonoBehaviour
    {
        [Tooltip("进入的关卡组（SceneGroup 资产；null = 只加载基座不加载关卡）")]
        [SerializeField] private SceneGroup levelGroup;

        private Button _button;

        private void OnEnable()
        {
            GetButton().onClick.AddListener(OnClick);
        }

        private void OnDisable()
        {
            GetButton().onClick.RemoveListener(OnClick);
        }

        private Button GetButton()
        {
            if (!_button)
            {
                _button = GetComponent<Button>();
            }
            return _button;
        }

        private void OnClick()
        {
            var app = AppManager.Context;
            if (app != null && app.StartRequested.Task.Status != UniTaskStatus.Pending)
            {
                Log.Warning("[LoadLevelButton] 启动请求已发出（流程已开始）——本按钮为一次性触发，再次点击无效。");
                return;
            }

            if (levelGroup == null)
            {
                Log.Warning("[LoadLevelButton] levelGroup 未配置——请求将只加载基座、不加载关卡（如需进入关卡请接线 SceneGroup 资产）。");
            }

            GameLoadingManager.RequestStart(new(levelGroup));
        }
    }
}
