using UnityEngine;

namespace XeptKit.Core
{
    public static class TransformExtensions
    {
        /// <summary>
        /// 重置本地位置为零、旋转为单位、缩放为 1。
        /// 命名用 ResetLocal 以避开编辑器 Reset 消息的语义混淆。
        /// </summary>
        public static void ResetLocal(this Transform transform)
        {
            Guard.NotNull(transform, nameof(transform));
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
    }
}
