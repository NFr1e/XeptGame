namespace XeptGame.Equip
{
    /// <summary>装备行为阶段；占有状态由身体容器独立维护。</summary>
    public enum EquipPhase
    {
        Empty,
        Stowed,
        Drawing,
        Ready,
        Stowing
    }
}
