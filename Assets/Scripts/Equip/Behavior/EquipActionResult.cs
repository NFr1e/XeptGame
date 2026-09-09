namespace XeptGame.Equip
{
    /// <summary>一次装备行为的终局通知；与容器提交、拾取获得事件分别计数。</summary>
    public readonly struct EquipActionResult
    {
        public readonly long ActionId;
        public readonly EquipActionOutcome Outcome;

        public EquipActionResult(long id, EquipActionOutcome outcome)
        {
            ActionId = id;
            Outcome = outcome;
        }
    }
}
