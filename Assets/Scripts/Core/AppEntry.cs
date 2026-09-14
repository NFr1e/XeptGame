using System;
using UnityEngine;

using XeptKit.Core;
using XeptKit.Event;
using XeptKit.Asset;
using XeptKit.Input;
using XeptKit.Scenes;
using XeptKit.Localization;
using XeptKit.UI.Manager;
using XeptGame.Core.Input;

namespace XeptGame
{
    /// <summary>
    /// 组合根：装配框架服务 + AppManager（AppFSM）+ 全局生命周期桥，然后启动应用状态机。
    /// 职责边界（见 AppFSM 设计决议）：AppEntry 只做装配与框架收尾（Shutdown）；
    /// 应用状态语义在 AppFSM（AppManager 持有）；业务关闭在 QuittingState。
    /// </summary>
    public static class AppEntry
    {
        public static AppManager AppManager { get; private set; }

        public static IEventBus EventBus { get; private set; }
        public static IAsyncEventBus AsyncEventBus { get; private set; }
        public static IAssetLoader AssetLoader { get; private set; }
        public static IScenesManager ScenesManager { get; private set; }
        public static IInputManager InputManager { get; private set; }
        public static ILocalizationManager LocalizationManager { get; private set; }
        public static IUIManager UIManager { get; private set; }
        public static CameraManager CameraManager { get; private set; }

        /// <summary>
        /// 时间权威（App 域服务，Time_Authority_Design.md T3）：暂停请求口 + 全工程唯一写 <c>Time.timeScale</c> 的地方。
        /// <b>注意名字遮蔽</b>：本属性遮蔽同名的 <c>UnityEngine.Time</c>，因此本类内不得再引用引擎时钟
        /// （引擎时钟的读写收口在 <see cref="EnginePauseEffect"/>）。误用会直接编译失败，不会静默出错。
        /// </summary>
        public static TimeAuthority Time { get; private set; }

#if UNITY_EDITOR
        private static IAssetLoader _editorLoader;
#endif
        public static GameInput GlobalInput { get; private set; }

        private static bool _shutdown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Bootstrap()
        {
            _shutdown = false;

            KitLifecycle.Initialize();

            #region Instantiate Services

            EventBus = new EventBus();
            AsyncEventBus = new AsyncEventBus();
#if UNITY_EDITOR
            // EditorAssetLoader 由 EditorEntry 通过 Editor 下 [InitializeOnLoad] 引导注入。
            if (_editorLoader == null)
            {
                throw new InvalidOperationException("[AppEntry] 编辑器环境未注入 EditorAssetLoader：请确认 EditorEntry 存在且编译正常。");
            }

            AssetLoader = _editorLoader;
#else
            AssetLoader = new AddressablesAssetLoader();
#endif
            ScenesManager = new ScenesManager(EventBus);
            InputManager = new InputManager();
            LocalizationManager = new LocalizationManager(EventBus, AssetLoader);
            UIManager = new UIManager(EventBus);
            GlobalInput = new GameInput();
            CameraManager = new CameraManager(); // 跨场景相机栈仲裁（CameraNotifier 自报，见 CameraManager）
            Time = new TimeAuthority(new EnginePauseEffect(InputManager)); // 暂停请求口（timeScale 唯一写者）

            #endregion

            AppManager = new AppManager();
            var context = new AppContext(EventBus);
            AppManager.Initialize(context);

            Application.quitting -= Shutdown;
            Application.quitting += Shutdown;

            AppManager.LaunchApp();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateLifecycleBridge()
        {
            new GameObject("[AppLifecycleBridge]")
            {
                hideFlags = HideFlags.NotEditable
            }
            .AddComponent<AppLifecycleBridge>()
            .Bind(AppManager);

            AppManager.StartApp();
        }

#if UNITY_EDITOR
        public static void RegisterEditorAssetLoader(IAssetLoader loader)
        {
            Guard.NotNull(loader, nameof(loader));
            _editorLoader = loader;
        }
#endif

        /// <summary>
        /// 框架收尾（幂等）：取消全局令牌 → 清事件流 → 逆序 Clear 各服务 → 终止 AppFSM。
        /// 由 Application.quitting 触发（Bootstrap 订阅；两段式退出的第二段，见设计决议 §4）；
        /// 不依赖任何 MonoBehaviour 存活。用户直接关窗口时同样会走到这里（业务关闭被跳过，进程将死，可接受）。
        /// </summary>
        public static void Shutdown()
        {
            if (_shutdown)
            {
                return;
            }

            _shutdown = true;

            Application.quitting -= Shutdown;

            if (EventBus is ISubscriptionClearable busClearable)
                busClearable.Clear();

            if (AsyncEventBus is ISubscriptionClearable asyncClearable)
                asyncClearable.Clear();

            UIManager?.Clear();
            LocalizationManager?.Clear();
            InputManager?.Clear();
            ScenesManager?.Clear();
            CameraManager?.Clear();
            Time?.ForceResume(); // 退出前还原全局时基与输入层（否则编辑器会停在 timeScale = 0）
            AppManager?.Dispose();

            KitLifecycle.Shutdown();
        }
    }
}
