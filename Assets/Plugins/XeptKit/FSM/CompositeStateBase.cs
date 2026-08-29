using System;

namespace XeptKit.FSM
{
    /// <summary>
    /// 层级（组合）状态基类：父状态携带子状态机。
    /// 语义：
    /// <list type="bullet">
    /// <item>进入父状态时创建子机器并进入 <see cref="ResolveInitialSubState"/> 指定的初始子状态；</item>
    /// <item>父 <c>Update</c> 执行共享逻辑（可读当前子状态参数），随后由 Fsm 转发子机器 <c>Tick</c>；</item>
    /// <item>子状态切换仅在子机器内部进行，不触发父级 Enter/Exit；</item>
    /// <item>子状态需要切父级状态时经 <see cref="StateBase.RootFsm"/> 发起（跨层级转移冒泡到根）。</item>
    /// </list>
    /// 父级退出（优雅路径）时先优雅退出子机器，再退出自身。
    /// 层级透明通知：子机器任何层级的状态进入都会触发父机器 <see cref="Fsm.StateChanged"/>
    /// （最深层先触发）；<see cref="OnSubStateChanged"/> 沿祖先链逐级回调（初始子状态进入除外）。
    /// </summary>
    public abstract class CompositeStateBase : StateBase
    {
        /// <summary>
        /// 子状态机（由 Fsm 在进入本状态时创建注入；退出时销毁）。
        /// 子类可读当前子状态（<c>SubMachine.CurrentState</c>）；internal set 供 Fsm 管理。
        /// </summary>
        protected internal Fsm SubMachine { get; internal set; }

        /// <summary>进入父状态时的初始子状态类型。子类声明（protected override，避免跨程序集访问修饰符问题）。</summary>
        protected abstract Type ResolveInitialSubState();

        /// <summary>供 Fsm 调用的内部入口（internal 包装 protected 抽象，同程序集合法）。</summary>
        internal Type ResolveInitialSubStateInternal() => ResolveInitialSubState();

        /// <summary>
        /// 子状态切换后回调（可选重写）：父状态可感知子状态变化（如按子状态应用参数/高度）。
        /// 在子机器新状态完全进入后调用；嵌套时每一级祖先都会收到后代切换回调（层级透明）。
        /// 初始子状态进入不回调（初始状态由父自己决定，非"切换"）。
        /// </summary>
        protected internal virtual void OnSubStateChanged(StateBase subState)
        {
        }
    }
}
