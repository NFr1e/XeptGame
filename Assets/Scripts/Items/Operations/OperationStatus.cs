namespace XeptGame.Items.Operations
{
    /// <summary>操作回执状态；终局描述操作结果，不回滚此前成功提交的步骤。</summary>
    public enum OperationStatus
    {
        Pending,
        Completed,
        Cancelled,
        Failed,
        Rejected
    }
}
