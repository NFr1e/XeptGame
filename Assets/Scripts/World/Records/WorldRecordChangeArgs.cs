namespace XeptGame.World
{
    /// <summary>世界记录变更种类。</summary>
    public enum WorldRecordChangeKind
    {
        /// <summary>新增记录（视图应生成）。</summary>
        Added,

        /// <summary>删除记录（视图应回收；<b>删除只由记录层发起</b>，不变量 I3）。</summary>
        Removed,

        /// <summary>记录内容变化（数量/位置；视图就地刷新）。</summary>
        Updated
    }

    /// <summary>世界记录变更负载（记录层 → 视图层的唯一通道）。</summary>
    public readonly struct WorldRecordChangeArgs
    {
        public readonly WorldRecordChangeKind Kind;

        /// <summary>发生变化的记录（Removed 时仍是同一个对象，供视图按 id 回收）。</summary>
        public readonly WorldRecord Record;

        public WorldRecordChangeArgs(WorldRecordChangeKind kind, WorldRecord record)
        {
            Kind = kind;
            Record = record;
        }
    }
}
