using System;
using UnityEngine;

namespace XeptGame.Game.Flow
{
    /// <summary>
    /// 一轮游戏会话入口点（组合根），于 GameplayLoadState 加载、GameplayUnloadState 卸载，
    /// 生命周期由 GameplayLifecycleBridge 驱动（Start/Update/LateUpdate 心跳钩子）。
    /// 职责边界（ItemLoop_Design.md §2.4，H1/H2）：
    /// <list type="bullet">
    /// <item><b>一轮组合根</b>：创建并持有 <see cref="GameplaySession"/>（Instantiate 生 / Dispose 弃），
    /// 场景模块经 <see cref="Instance"/> 访问 <see cref="Session"/>（显式装配，对齐 AppEntry 静态门面先例）；</item>
    /// <item><b>LifecycleDriver 钩子只做驱动/转发</b>，不放业务实现（防变杂物筐）；</item>
    /// <item>域级对象（服务/FSM/GameContext）不归本类；FSM 状态不得持有 Session（状态实例缓存复用约定）。</item>
    /// </list>
    /// </summary>
    public class GameplayEntry
    {
        private static GameplayEntry _instance;
        private GameplaySession _session;

        /// <summary>当前一轮入口（未 Instantiate 访问抛异常，提示初始化顺序错误）。</summary>
        public static GameplayEntry Instance
        {
            get
            {
                if (_instance is not null)
                {
                    return _instance;
                }
                else
                {
                    throw new InvalidOperationException("GameplayEntry is not initialized. Call Instantiate() first.");
                }
            }
        }

        /// <summary>
        /// 非抛出式取当前一轮入口（未初始化/已卸载返回 false）——场景侧组件（WorldItem 等）的无异常防御访问口，
        /// 避免把"启动时序错误"做成异常控制流。
        /// </summary>
        public static bool TryGetInstance(out GameplayEntry entry)
        {
            entry = _instance;
            return _instance != null;
        }

        /// <summary>一轮游戏会话的业务状态容器（Inventory 起步；与入口同生共死，Dispose 后不可访问）。</summary>
        public GameplaySession Session
        {
            get
            {
                if (_session is not null)
                {
                    return _session;
                }
                else
                {
                    throw new InvalidOperationException("GameplaySession is not initialized. Call Instantiate() first.");
                }
            }
        }

        /// <summary>创建一轮入口（幂等）：首次创建并装配 <see cref="GameplaySession"/>。</summary>
        public static GameplayEntry Instantiate()
        {
            if (_instance == null)
            {
                _instance = new GameplayEntry();
                _instance._session = new GameplaySession();
            }

            return _instance;
        }

        /// <summary>卸载一轮会话（幂等）：弃 Session 并置空入口引用。</summary>
        public void Dispose()
        {
            _session = null;
            _instance = null;
        }

        #region LifecycleDriver
        public void Start()
        {

        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {

        }

        public void LateUpdate(float deltaTime, float unscaledDeltaTime)
        {

        }
        #endregion
    }
}