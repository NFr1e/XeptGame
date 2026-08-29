using UnityEngine;

namespace XeptGame
{
    /// <summary>
    /// 全局应用生命周期桥（DontDestroyOnLoad，由 AppEntry 在首个场景加载后创建）：
    /// 接收 Unity 应用级回调并翻译为 AppFSM 语义，业务不依赖零散 OnApplicationXXX；
    /// Update 驱动全局 Tick（AppFSM 与业务流程状态机）。
    /// 职责：OnApplicationPause → AppManager 暂停转移；Update → 全局 Tick。
    /// 退出收尾不挂本组件（OnApplicationQuit 依赖组件存活，不可靠）——由 AppEntry 订阅
    /// Application.quitting 执行 AppEntry.Shutdown()（见设计决议 §4）。
    /// OnApplicationFocus 暂不建模（桌面失焦语义留待真实业务需求，不混入应用暂停态）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AppLifecycleBridge : MonoBehaviour
    {
        private AppManager _manager;

        public void Bind(AppManager manager) => _manager = manager;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            _manager?.Tick(Time.deltaTime);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            _manager?.OnApplicationPause(pauseStatus);
        }
    }
}
