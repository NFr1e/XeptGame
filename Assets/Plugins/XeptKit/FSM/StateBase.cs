using System.Threading;
using Cysharp.Threading.Tasks;

namespace XeptKit.FSM
{
    /// <summary>
    /// 状态抽象基类。业务继承并重写需要参与的钩子；默认实现为空操作，避免被迫实现不关心的钩子。
    /// 钩子按转移时序排列：初始化 <see cref="OnInit"/>/<see cref="InitAsync"/>（在上一状态退出前执行）、
    /// 进入 <see cref="OnEnter"/>/<see cref="EnterAsync"/>（在上一状态退出后执行）、退出 <see cref="OnExit"/>/<see cref="ExitAsync"/>、每帧 <see cref="Update"/>。
    /// 同步钩子面向最常见场景（无需接触 UniTask）；异步钩子仅需异步（等待资源/动画等）时重写；
    /// 同一阶段两者并存时同步钩子先执行、异步钩子随后。
    /// 状态实例由状态机按类型缓存复用：禁止用字段承载跨进入会话的可变数据（会话数据放 <see cref="Context"/>），
    /// 字段仅可承载类型化配置/只读数据（常量、字段初始化默认值）。
    /// </summary>
    public abstract class StateBase
    {
        /// <summary>所属状态机，进入前由机器注入，业务只读。</summary>
        public Fsm Fsm { get; internal set; }

        /// <summary>
        /// 根状态机（跨层级冒泡转移用）：沿父机器链回溯到最顶层机器。
        /// 子状态需切换父级/顶层状态时经它发起（如跳跃转空中等顶层转移）。
        /// </summary>
        public Fsm RootFsm
        {
            get
            {
                var fsm = Fsm;
                while (fsm != null && fsm.ParentMachine != null)
                {
                    fsm = fsm.ParentMachine;
                }

                return fsm;
            }
        }

        /// <summary>机器注入的共享上下文，可为 null；业务只读，按需自行强转。</summary>
        public object Context { get; internal set; }

        /// <summary>同步初始化钩子。在上一状态退出前执行；默认空实现；与 <see cref="InitAsync"/> 并存时先于其执行。</summary>
        public virtual void OnInit()
        {
        }

        /// <summary>异步初始化钩子。在上一状态退出前执行；默认返回已完成任务；取消令牌由转移透传。</summary>
        public virtual UniTask InitAsync(CancellationToken cancellationToken = default) => UniTask.CompletedTask;

        /// <summary>同步进入钩子。在上一状态退出后执行；默认空实现；与 <see cref="EnterAsync"/> 并存时先于其执行。</summary>
        public virtual void OnEnter()
        {
        }

        /// <summary>异步进入钩子。在上一状态退出后执行；默认返回已完成任务；取消令牌由转移透传。</summary>
        public virtual UniTask EnterAsync(CancellationToken cancellationToken = default) => UniTask.CompletedTask;

        /// <summary>同步退出钩子。默认空实现；与 <see cref="ExitAsync"/> 并存时先于其执行。</summary>
        public virtual void OnExit()
        {
        }

        /// <summary>异步退出钩子。默认返回已完成任务；取消令牌由转移透传。</summary>
        public virtual UniTask ExitAsync(CancellationToken cancellationToken = default) => UniTask.CompletedTask;

        /// <summary>每帧更新。由业务在 <see cref="Fsm.Tick"/> 中驱动，传递 deltaTime。</summary>
        public virtual void Update(float deltaTime)
        {
        }

        public override string ToString()
        {
            return GetType().Name;
        }
    }
}
