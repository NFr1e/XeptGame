using UnityEngine;

namespace XeptGame.Game.Flow
{
    [DisallowMultipleComponent]
    public class GameplayLifecycleBridge : MonoBehaviour
    {
        private GameplayEntry _gameplayEntry;

        public GameplayLifecycleBridge Bind(GameplayEntry entry)
        {
            _gameplayEntry = entry;
            return this;
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            _gameplayEntry?.Update(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            _gameplayEntry?.LateUpdate(Time.deltaTime,Time.unscaledDeltaTime);
        }
    }
}
