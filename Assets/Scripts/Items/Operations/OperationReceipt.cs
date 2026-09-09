namespace XeptGame.Items.Operations
{
    /// <summary>同一次请求返回同一回执。Accepted 不等于物品已经提交。</summary>
    public sealed class OperationReceipt
    {
        public long Id { get; internal set; }

        /// <summary>Pending 只表示请求已接纳；终局并不撤销此前提交的容器操作。</summary>
        public OperationStatus Status { get; internal set; }

        /// <summary>拒绝或失败原因，仅用于诊断，不作为状态机分派键。</summary>
        public string Reason { get; internal set; }

        /// <summary>本次实际从世界提交到玩家容器的数量；可能小于请求数量。</summary>
        public int AcquiredCount { get; internal set; }

        /// <summary>换物时旧物是否已经转出，用于说明部分提交后的最终事实。</summary>
        public bool OldItemTransferred { get; internal set; }

        public bool Accepted => Status != OperationStatus.Rejected;
        public bool IsTerminal => Status != OperationStatus.Pending;
    }
}
