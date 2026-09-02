namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互探测源抽象：提供交互判定的射线来源。
    /// 这是交互系统的变化点——玩家从相机中心探测、AI 从自身朝向探测、
    /// 脚本/过场可指定任意起点方向；探测后的解析逻辑（物理探测 → 角度过滤 → 契约门控）
    /// 由 <see cref="InteractionResolver"/> 统一执行，不随探测源重复。
    /// <para>消费方（如 <c>InteractionDetector</c>）依赖本接口而非具体来源；
    /// 输入绑定与交互执行是消费方职责，不属于本接口。</para>
    /// </summary>
    public interface IInteractionProbeSource
    {
        /// <summary>当前探测射线（每帧由消费方调用；应返回世界空间的起点 + 单位方向）。</summary>
        InteractionProbeRay GetProbeRay();
    }
}
