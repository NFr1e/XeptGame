using System;

namespace XeptKit.Core
{
    /// <summary>
    /// 参数校验与前置条件。始终执行（Release 下同样生效）；
    /// 可剥离断言（性能 Debug 用）请使用 Unity 的 Debug.Assert，二者职责分离。
    /// 注意：NotNull 使用引用判空，无法捕获 Destroy 后的 Unity 对象（假 null），
    /// 需要该语义时使用 NotNullObject。
    /// </summary>
    public static class Guard
    {
        public static void NotNull<T>(T value, string paramName = null) where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }
        }

        /// <summary>针对 UnityEngine.Object 假 null 的判空：依赖 Unity 重载的 == 运算符，可捕获 Destroy 后的对象。</summary>
        public static void NotNullObject<T>(T value, string paramName = null) where T : UnityEngine.Object
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }
        }

        public static void NotNullOrEmpty(string value, string paramName = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("参数不能为 null 或空字符串。", paramName);
            }
        }

        public static void NotNullOrWhiteSpace(string value, string paramName = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("参数不能为 null 或纯空白。", paramName);
            }
        }

        public static void InRange(int value, int min, int max, string paramName = null)
        {
            if (value < min || value > max)
            {
                throw new ArgumentOutOfRangeException(
                    paramName, $"参数必须在 [{min}, {max}] 范围内，当前为 {value}。");
            }
        }

        public static void InRange(float value, float min, float max, string paramName = null)
        {
            if (value < min || value > max)
            {
                throw new ArgumentOutOfRangeException(
                    paramName, $"参数必须在 [{min}, {max}] 范围内，当前为 {value}。");
            }
        }

        public static void True(bool condition, string message = null)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message ?? "前置条件不满足。");
            }
        }
    }
}
