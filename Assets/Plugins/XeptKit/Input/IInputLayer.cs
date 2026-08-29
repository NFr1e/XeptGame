namespace XeptKit.Input
{
    /// <summary>
    /// 输入层级配置抽象：名称 + 优先级 + 是否阻断更低优先级层。
    /// 由业务定义轻量类型实现，属性返回常量；
    /// 层实例视为只读配置，运行时修改属性会破坏排序与阻断判断。
    /// 层激活状态为分发器运行态概念，见 <see cref="IInputManager"/>。
    /// </summary>
    public interface IInputLayer
    {
        /// <summary>层名称，调试/日志标识。</summary>
        string Name { get; }

        /// <summary>优先级，值越大越先（对齐全库惯例，与 Event/Scenes 一致）。</summary>
        int Priority { get; }

        /// <summary>是否阻断更低优先级层。失活层的阻断标志自然失效（见 IInputManager 分发语义）。</summary>
        bool BlockLowerLayers { get; }
    }
}
