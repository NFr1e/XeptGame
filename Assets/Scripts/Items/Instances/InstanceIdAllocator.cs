namespace XeptGame.Items
{
    /// <summary>
    /// 实例身份签发器（Item_Instance_Design.md §2.1 / DP7）：会话内<b>单调自增、永不复用</b>已发出的号。
    /// <list type="bullet">
    /// <item><b>0 预留为"无实例"哨兵</b>，首发号为 1；构造参数小于 1 时回落到 1；</item>
    /// <item>存档层（T6）持久化 <see cref="Next"/>，加载时以"已用最大值 + 1"续发——由会话持有并注入实例工厂；</item>
    /// <item><b>为什么不用 GUID string</b>：单机单会话用不上"免中心签发"这个唯一优势，而 GUID 会让每条实例记录
    /// 多写 28+ 字节、不可排序、且破坏测试与存档 diff 的确定性（调研 §8 / R1）。</item>
    /// </list>
    /// </summary>
    public sealed class InstanceIdAllocator
    {
        private long _next;

        public InstanceIdAllocator(long next = 1) => _next = next < 1 ? 1 : next;

        /// <summary>下一个将签发的号（存档层读取用；<b>不消耗</b>号）。</summary>
        public long Next => _next;

        /// <summary>签发一个新号（单调递增、永不复用）。</summary>
        public long Allocate() => _next++;
    }
}
