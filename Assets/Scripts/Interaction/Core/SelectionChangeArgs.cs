namespace XeptGame.Interaction
{
    /// <summary>
    /// 选中变化负载（只读结构体）：镜像 <c>Fsm.StateChangeArgs</c> 命名风格（To 在前、From 在后）。
    /// 判断（有无目标/是否变化）收敛进结构体——订阅方写 <c>if (args.HasTarget)</c> 而非裸 null 判断。
    /// </summary>
    public readonly struct SelectionChangeArgs
    {
        /// <summary>新选中（null = 当前无目标）。</summary>
        public readonly ISelectable To;

        /// <summary>旧选中（null = 之前无目标）。</summary>
        public readonly ISelectable From;

        public SelectionChangeArgs(ISelectable to, ISelectable from)
        {
            To = to;
            From = from;
        }

        /// <summary>当前是否有目标。</summary>
        public bool HasTarget => To != null;

        /// <summary>目标是否变化（含 无→有 / 有→无）。</summary>
        public bool Changed => From != To;
    }
}
