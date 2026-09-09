namespace XeptGame.Equip
{
    /// <summary>一次行为的终局语义，按 ActionId 区分被替代的旧动作。</summary>
    public enum EquipActionOutcome
    {
        Completed,
        Superseded,
        Aborted
    }
}
