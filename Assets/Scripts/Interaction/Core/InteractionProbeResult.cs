namespace XeptGame.Interaction
{
    /// <summary>
    /// 一次交互探测的解析结果（只读结构体）：最近解析的可选中者 + 距离 + 探测快照。
    /// 动作不在结果内（契约模型 v3）：宿主（ISelectable）推送后由执行器沿宿主链收集动作集
    /// （IInteractionAction）；可用性（CanInteract）由消费方在动作上每帧查询。
    /// </summary>
    public readonly struct InteractionProbeResult
    {
        /// <summary>最近解析的可选中者（null = 无目标/被拒）。</summary>
        public readonly ISelectable Target;

        /// <summary>目标距离（m；无目标为 0）。</summary>
        public readonly float Distance;

        /// <summary>本次实际探测快照（调试数据源）。</summary>
        public readonly ProbeInfo Probe;

        public InteractionProbeResult(ISelectable target, float distance, ProbeInfo probe)
        {
            Target = target;
            Distance = distance;
            Probe = probe;
        }
    }
}
