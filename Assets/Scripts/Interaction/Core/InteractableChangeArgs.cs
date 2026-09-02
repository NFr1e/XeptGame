namespace XeptGame.Interaction
{
    /// <summary>
    /// 交互者变化负载（只读结构体）：镜像 <see cref="SelectionChangeArgs"/> 风格（To 在前、From 在后）。
    /// </summary>
    public readonly struct InteractableChangeArgs
    {
        /// <summary>新交互者（null = 无）。</summary>
        public readonly IInteractable To;

        /// <summary>旧交互者（null = 之前无）。</summary>
        public readonly IInteractable From;

        public InteractableChangeArgs(IInteractable to, IInteractable from)
        {
            To = to;
            From = from;
        }
    }
}
