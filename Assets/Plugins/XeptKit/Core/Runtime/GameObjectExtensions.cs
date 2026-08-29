using UnityEngine;

namespace XeptKit.Core
{
    public static class GameObjectExtensions
    {
        /// <summary>获取已有组件，不存在则添加并返回。</summary>
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            Guard.NotNull(gameObject, nameof(gameObject));
            var component = gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
            return component;
        }
    }
}
