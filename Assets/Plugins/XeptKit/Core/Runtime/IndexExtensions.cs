namespace XeptKit.Core
{
    /// <summary>环形索引工具：在 [0, count) 范围内循环前进/后退。</summary>
    public static class IndexExtensions
    {
        /// <summary>下一个索引，末尾回绕到 0。</summary>
        public static int NextIndex(this int index, int count)
        {
            Guard.InRange(count, 1, int.MaxValue, nameof(count));
            return (index + 1) % count;
        }

        /// <summary>上一个索引，0 回绕到 count - 1。</summary>
        public static int PrevIndex(this int index, int count)
        {
            Guard.InRange(count, 1, int.MaxValue, nameof(count));
            return (index - 1 + count) % count;
        }
    }
}
