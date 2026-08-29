namespace XeptKit.UI.Manager
{
    /// <summary>
    /// 表单已完全打开（含入场动画与 OnOpened）后广播。供非发起方感知与句柄分发
    /// （fire-and-forget 打开后取句柄）。经构造注入的 IEventBus 同步广播。
    /// </summary>
    public readonly struct FormOpenedEvent
    {
        public FormHandle Handle { get; }

        public FormOpenedEvent(FormHandle handle)
        {
            Handle = handle;
        }
    }

    /// <summary>
    /// 表单关闭流程完成（含出场动画与终结）后广播。句柄已在 Close 请求时失效，此处仅作识别用
    /// （纯数据，仍可比较）。
    /// </summary>
    public readonly struct FormClosedEvent
    {
        public FormHandle Handle { get; }

        public FormClosedEvent(FormHandle handle)
        {
            Handle = handle;
        }
    }

    /// <summary>全局焦点顶变化后广播（old → new；无焦点顶为 default）。供业务订阅（HUD 自禁用、音效等）。</summary>
    public readonly struct FocusChangedEvent
    {
        public FormHandle Previous { get; }

        public FormHandle Current { get; }

        public FocusChangedEvent(FormHandle previous, FormHandle current)
        {
            Previous = previous;
            Current = current;
        }
    }
}
