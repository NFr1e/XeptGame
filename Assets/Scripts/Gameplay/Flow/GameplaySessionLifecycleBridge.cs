using UnityEngine;

namespace XeptGame.Game.Flow
{
    /// <summary>
    /// 一轮 GameplaySession 域心跳桥（MonoBehaviour，DDOL；更名自 GameplayLifecycleBridge）：
    /// 把 Unity 生命周期回调转发给 <see cref="GameplaySessionEntry"/>（纯转发、零逻辑）。
    /// 由 GameplayLoadState 动态创建（new GameObject + AddComponent + Bind），GameplayUnloadState 销毁。
    /// </summary>
    [DisallowMultipleComponent]
    public class GameplaySessionLifecycleBridge : MonoBehaviour
    {
        private GameplaySessionEntry _entry;

        public GameplaySessionLifecycleBridge Bind(GameplaySessionEntry entry)
        {
            _entry = entry;
            return this;
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            _entry?.Update(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            _entry?.LateUpdate(Time.deltaTime, Time.unscaledDeltaTime);
        }
    }
}
