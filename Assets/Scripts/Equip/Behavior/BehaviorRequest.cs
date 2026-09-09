namespace XeptGame.Equip
{
    /// <summary>行为请求即时结果，不等同于动作终局。</summary>
    public enum BehaviorRequest
    {
        Started,
        AlreadyInProgress,
        AlreadySatisfied,
        Rejected
    }
}
