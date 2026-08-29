using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using XeptKit.Core;

namespace XeptKit.FSM
{
    /// <summary>
    /// 纯逻辑有限状态机（支持层级：<see cref="CompositeStateBase"/> 父状态携带子机器）。
    /// 主线程 only、无锁，不绑定 MonoBehaviour。
    /// 状态实例按类型缓存复用；转移仅显式驱动。
    /// 层级语义：父 Update 执行共享逻辑后转发子机器 Tick；子切换不触发父 Enter/Exit；
    /// <see cref="StateChanged"/> 为任一层级进入的统一通知（最深层先触发）；子状态切父级状态经 RootFsm 冒泡。
    /// 取消语义：取消只中断业务钩子，不中断状态机收敛——Init 取消保持当前；
    /// Exit 取消强制完成进入新状态（杜绝"半退出态仍为当前、下次转移双退出"）；
    /// Enter 取消采纳新状态并补齐子机器与 StateChanged。
    /// 双释放接口：<see cref="Dispose"/> 同步标记终止（非优雅路径，不清引用）；
    /// <see cref="DisposeAsync()"/> 优雅退出（先子机器后父状态）并清空引用；
    /// <see cref="DisposeAsync(CancellationToken)"/> 允许业务以自身令牌约束优雅退出（默认 None）。
    /// </summary>
    public sealed class Fsm : IDisposable, IDisposableAsync
    {
        private readonly Dictionary<Type, StateBase> _states = new Dictionary<Type, StateBase>();
        private readonly object _context;
        private StateBase _current;
        private bool _isTransitioning;
        private PendingRequest? _pending;
        private bool _terminated;
        private bool _disposed;
        private bool _initialSubEntry;   // 初始子状态进入中：OnSubStateChanged 不回调（初始非"切换"）

        /// <summary>父机器（本机器作为某父状态的子机器时注入）；根机器为 null。</summary>
        internal Fsm ParentMachine { get; set; }

        /// <summary>上下文构造注入，只读，可为 null。</summary>
        public Fsm(object context = null)
        {
            _context = context;
        }

        /// <summary>
        /// 当前状态（最深层叶子）：若当前为父状态，下钻到其子机器当前状态。
        /// 仅新状态完全进入后更新；转移期间仍为旧状态；无状态时为 null。
        /// </summary>
        public StateBase CurrentState
        {
            get
            {
                var current = _current;
                while (current is CompositeStateBase composite && composite.SubMachine != null)
                {
                    current = composite.SubMachine.CurrentState;
                }

                return current;
            }
        }

        /// <summary>当前叶子状态类型，无状态时为 null。</summary>
        public Type CurrentStateType => CurrentState?.GetType();

        /// <summary>当前顶层状态（不包含子机器下钻）。无状态时为 null。</summary>
        public StateBase RootState => _current;

        /// <summary>当前顶层状态类型，无状态时为 null。</summary>
        public Type RootStateType => _current?.GetType();

        /// <summary>是否正在进行异步转移。</summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>转移异常上报钩子；Type 为抛出异常的状态类型。订阅者自身抛异常会被隔离。</summary>
        public event Action<Type, Exception> TransitionException;

        /// <summary>
        /// 状态变更通知（在状态**进入**完成后触发；参数 = 变更的 from/to，**首次进入 From 为 null**）。
        /// 任一层级进入都会触发（含子机器切换与初始子状态进入）；多级嵌套时最深层先触发、逐级上浮；
        /// 子机器切换透传最深层叶子的 from/to。
        /// 注意：仅进入时触发，状态退出本身不触发事件（需要"离开某状态"通知的场景应订阅本事件自行跟踪）。
        /// 订阅者自身抛异常会被隔离。
        /// </summary>
        public event Action<StateChangeArgs> StateChanged;

        /// <summary>
        /// 外部确定性异步转移入口。业务 await 返回即目标状态已完全进入。
        /// 转移中重入、已终止均抛 <see cref="InvalidOperationException"/>（fail-fast）。
        /// 目标为父状态时一并进入其初始子状态。目标为当前分支内类型（含祖先/抽象基类）时幂等跳过。
        /// </summary>
        public async UniTask ChangeStateAsync<TState>(CancellationToken cancellationToken = default) where TState : StateBase, new()
        {
            ThrowIfTerminated();

            if (_isTransitioning)
            {
                throw new InvalidOperationException(
                    "状态机正在进行异步转移，禁止再次发起转移。请先 await 前一转移完成。");
            }

            if (IsCurrent(typeof(TState)))
            {
                return;
            }

            await TransitionAsync(GetOrCreateState<TState>(() => new TState()), cancellationToken, fireAndForget: false);
        }

        /// <summary>
        /// 状态钩子内（Enter/Exit/Update）安全触发转移的同步入口。
        /// 转移中登记 pending（仅保留最新）；稳定态幂等跳过或立即发起受控转移。
        /// 内部 fire-and-forget，无未观察异常（异常统一 Log + TransitionException）。
        /// </summary>
        public void RequestChange<TState>(CancellationToken cancellationToken = default) where TState : StateBase, new()
        {
            ThrowIfTerminated();

            if (_isTransitioning)
            {
                _pending = new PendingRequest(typeof(TState), () => GetOrCreateState<TState>(() => new TState()), cancellationToken);
                return;
            }

            if (IsCurrent(typeof(TState)))
            {
                return;
            }

            StartFireAndForget(typeof(TState), () => GetOrCreateState<TState>(() => new TState()), cancellationToken);
        }

        /// <summary>
        /// 非泛型转移入口（动态目标类型，低频）：支撑"入口状态类由上下文/配置决定"的场景
        /// （如 UI 流程、玩法阶段机的入口状态由配置装配）。目标类型须有无参构造
        /// （经 <see cref="Activator.CreateInstance(Type)"/> 创建）；显式传入类型，不做自动扫描/注册表。
        /// 语义与 <see cref="RequestChange{TState}"/> 一致。
        /// </summary>
        public void RequestChange(Type stateType, CancellationToken cancellationToken = default)
        {
            ThrowIfTerminated();

            if (_isTransitioning)
            {
                _pending = new PendingRequest(stateType, () => GetOrCreateState(stateType), cancellationToken);
                return;
            }

            if (IsCurrent(stateType))
            {
                return;
            }

            StartFireAndForget(stateType, () => GetOrCreateState(stateType), cancellationToken);
        }

        /// <summary>
        /// 驱动当前状态 Update（父状态执行共享逻辑后转发子机器 Tick）。
        /// 无状态或转移中为 no-op。
        /// </summary>
        public void Tick(float deltaTime)
        {
            ThrowIfTerminated();

            if (_isTransitioning || _current == null)
            {
                return;
            }

            // 父 Update：共享逻辑（可读当前子状态参数）
            _current.Update(deltaTime);

            // 转发子机器 Tick：子状态 Update（参数/转移判断）
            if (_current is CompositeStateBase composite && composite.SubMachine != null)
            {
                composite.SubMachine.Tick(deltaTime);
            }
        }

        /// <summary>
        /// 当前是否处于目标状态或其分支内（层级查询；含父级/抽象基类/接口 is 匹配）。
        /// 亦承担转移幂等判定（<see cref="ChangeStateAsync{TState}"/> / <see cref="RequestChange{TState}"/>
        /// 对"当前分支内类型"的转移请求幂等跳过，含祖先类型）。
        /// </summary>
        public bool IsInHierarchy(Type stateType)
        {
            if (stateType == null || _current == null)
            {
                return false;
            }

            if (stateType.IsInstanceOfType(_current))
            {
                return true;
            }

            if (_current is CompositeStateBase composite && composite.SubMachine != null)
            {
                return composite.SubMachine.IsInHierarchy(stateType);
            }

            return false;
        }

        /// <summary>同步释放：递归终止子机器并标记本机终止，立即拒绝后续操作，不清空引用（非优雅路径）。</summary>
        public void Dispose()
        {
            _terminated = true;

            if (_current is CompositeStateBase composite && composite.SubMachine != null)
            {
                composite.SubMachine.Dispose();
            }
        }

        /// <summary>
        /// 优雅释放（默认令牌）：等价于以 <see cref="CancellationToken.None"/> 调用
        /// <see cref="DisposeAsync(CancellationToken)"/>——退出不受任何令牌约束，业务 `ExitAsync`
        /// 永不完成时可能永久挂起（业务应自行保证退出可完成，或用带令牌的重载约束）。
        /// </summary>
        public UniTask DisposeAsync() => DisposeAsync(CancellationToken.None);

        /// <summary>
        /// 优雅释放：先置终止标记 → 优雅退出子机器 → 退出当前状态（异常隔离记日志）→ 清空缓存与引用。
        /// 幂等（重复调用 no-op）；转移进行中抛 <see cref="InvalidOperationException"/>。
        /// 取消令牌约束退出钩子（含子机器）：取消仅中断业务退出，机器仍收敛到终止态（引用清空）。
        /// </summary>
        public async UniTask DisposeAsync(CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                return;
            }

            if (_isTransitioning)
            {
                throw new InvalidOperationException(
                    "状态机正在进行异步转移，无法优雅释放。请先 await 转移完成。");
            }

            // 先置终止标记：杜绝当前状态在自身 ExitAsync 内 RequestChange/ChangeStateAsync 复活机器
            _terminated = true;
            _disposed = true;

            if (_current != null)
            {
                // 先子机器后父状态（优雅路径）
                try
                {
                    await DisposeSubMachineAsync(_current, cancellationToken);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }

                try
                {
                    _current.OnExit();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }

                try
                {
                    await _current.ExitAsync(cancellationToken).AttachExternalCancellation(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // 取消中断业务退出：机器已终止，仍收敛（清空引用）
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
            }

            _states.Clear();
            _current = null;
            _pending = null;
        }

        /// <summary>内部非泛型转移入口（父状态创建子机器/初始子状态用；低频，显式类型 + Activator 创建）。</summary>
        internal async UniTask ChangeStateAsyncInternal(Type stateType, CancellationToken cancellationToken)
        {
            ThrowIfTerminated();

            if (_isTransitioning)
            {
                throw new InvalidOperationException(
                    "状态机正在进行异步转移，禁止再次发起转移。请先 await 前一转移完成。");
            }

            if (IsCurrent(stateType))
            {
                return;
            }

            await TransitionAsync(GetOrCreateState(stateType), cancellationToken, fireAndForget: false);
        }

        /// <summary>
        /// 幂等判定：目标状态已在当前层级分支内（含祖先/抽象基类匹配，与
        /// <see cref="IsInHierarchy"/> 同一语义）即视为"已在该分支内，重进无意义"。
        /// 修复：原实现只比较"叶子/顶层"两精确类型，深度 ≥2 嵌套时对中间复合类型
        /// 重进不幂等，且与 IsInHierarchy 的 is 匹配语义分叉。
        /// </summary>
        private bool IsCurrent(Type stateType)
            => stateType != null && IsInHierarchy(stateType);

        private StateBase GetOrCreateState(Type stateType)
        {
            return _states.GetOrAdd(stateType, () => (StateBase)Activator.CreateInstance(stateType));
        }

        private StateBase GetOrCreateState<TState>(Func<StateBase> factory) where TState : StateBase
        {
            return _states.GetOrAdd(typeof(TState), factory);
        }

        private void ThrowIfTerminated()
        {
            if (_terminated)
            {
                throw new InvalidOperationException("Fsm 已终止，无法再执行操作。");
            }
        }

        private async UniTask TransitionAsync(StateBase next, CancellationToken ct, bool fireAndForget)
        {
            _isTransitioning = true;
            var previous = _current; // 转移起点状态：StateChanged 的 From（必须在 _current 变更前捕获）
            int phase = 0; // 0=Init 阶段，1=Exit 阶段，2=Enter 阶段（取消路径据此分派）

            try
            {
                // 注入先于 Init：初始化阶段即可读上下文/触发转移
                next.Fsm = this;
                next.Context = _context;

                await InitStateAsync(next, ct);   // 初始化：在上一状态退出前执行

                if (_current != null)
                {
                    // 退出旧状态：先优雅退出其子机器（若为父状态），再退出自身
                    phase = 1;
                    await DisposeSubMachineAsync(_current);
                    await ExitStateAsync(_current, ct);
                }

                phase = 2;

                await EnterStateAsync(next, ct);
                _current = next;

                // 父状态：创建子机器并进入初始子状态（最深层先通知 StateChanged）
                if (next is CompositeStateBase composite)
                {
                    await EnterSubMachineAsync(composite, ct);
                }

                RaiseStateChanged(new StateChangeArgs(next, previous));
            }
            catch (OperationCanceledException)
            {
                // 取消收敛：任何阶段取消都不允许状态机停留在"半进入/半退出"态——
                // Init 取消：旧状态未动，保持当前；
                // Exit 取消：旧状态已开始退出（OnExit/ExitAsync 部分执行），强制完成进入新状态，
                //           避免"半退出态仍是 _current、下次转移双退出"；
                // Enter 取消：新状态 OnEnter 已执行，采纳为新当前并补齐子机器与 StateChanged，
                //           避免"复合态无子机器、CurrentState 无法下钻、事件缺失"。
                // 补全路径用 CancellationToken.None：取消只中断业务钩子，不中断状态机收敛。
                if (phase == 2)
                {
                    _current = next;
                    await CompleteEntryAsync(next, previous);
                }
                else if (phase == 1 && _current != null)
                {
                    try
                    {
                        await EnterStateAsync(next, CancellationToken.None);
                        _current = next;
                        await CompleteEntryAsync(next, previous);
                    }
                    catch (Exception ex)
                    {
                        // 补全进入失败（业务钩子抛异常）：置为新当前但记录，保证 _current 一致
                        _current = next;
                        Log.Exception(ex);
                        RaiseTransitionException(next.GetType(), ex);
                    }
                }

                if (!fireAndForget)
                {
                    throw;
                }
            }
            finally
            {
                _isTransitioning = false;
                ConsumePending();
            }
        }

        /// <summary>
        /// 取消收敛辅助：补齐复合态子机器创建/初始子状态与 StateChanged 通知（None token，不受取消影响）。
        /// 覆盖两类残留：子机器未创建（Enter 阶段取消）；子机器已创建但初始子状态进入被取消
        /// （EnterSubMachineAsync 内取消——子机器存在却无当前状态，CurrentState 无法下钻）。
        /// </summary>
        private async UniTask CompleteEntryAsync(StateBase state, StateBase previous)
        {
            try
            {
                if (state is CompositeStateBase composite)
                {
                    if (composite.SubMachine == null)
                    {
                        await EnterSubMachineAsync(composite, CancellationToken.None);
                    }
                    else if (composite.SubMachine.CurrentState == null)
                    {
                        var initialType = composite.ResolveInitialSubStateInternal();
                        if (initialType != null)
                        {
                            _initialSubEntry = true;
                            try
                            {
                                await composite.SubMachine.ChangeStateAsyncInternal(initialType, CancellationToken.None);
                            }
                            finally
                            {
                                _initialSubEntry = false;
                            }
                        }
                    }
                }

                RaiseStateChanged(new StateChangeArgs(state, previous));
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                RaiseTransitionException(state.GetType(), ex);
            }
        }

        /// <summary>进入父状态时创建子机器并进入初始子状态；子机器 StateChanged 转发为层级事件。</summary>
        private async UniTask EnterSubMachineAsync(CompositeStateBase composite, CancellationToken ct)
        {
            var subMachine = new Fsm(_context) { ParentMachine = this };
            subMachine.StateChanged += OnSubStateEntered;
            composite.SubMachine = subMachine;

            var initialType = composite.ResolveInitialSubStateInternal();
            if (initialType == null)
            {
                throw new InvalidOperationException(
                    $"[Fsm] 父状态 {composite.GetType().Name} 的 ResolveInitialSubState 返回 null，无法进入子状态。");
            }

            // 初始进入不触发 OnSubStateChanged（初始状态由父自己决定，非"切换"）
            _initialSubEntry = true;
            try
            {
                await subMachine.ChangeStateAsyncInternal(initialType, ct);
            }
            finally
            {
                _initialSubEntry = false;
            }
        }

        /// <summary>退出父状态前优雅退出其子机器（退订转发 + DisposeAsync，异常隔离；转移中回退同步 Dispose 防僵尸）。</summary>
        private async UniTask DisposeSubMachineAsync(StateBase exiting, CancellationToken ct = default)
        {
            if (exiting is CompositeStateBase composite && composite.SubMachine != null)
            {
                var subMachine = composite.SubMachine;
                subMachine.StateChanged -= OnSubStateEntered;
                composite.SubMachine = null;

                try
                {
                    await subMachine.DisposeAsync(ct);
                }
                catch (InvalidOperationException)
                {
                    // 子机器转移进行中无法优雅退出：回退同步 Dispose（强制置 _terminated），
                    // 杜绝"异常被吞 → 子机器从未终止 → 僵尸子机器仍可被 Tick"。
                    subMachine.Dispose();
                }
                catch (OperationCanceledException)
                {
                    // 取消中断子机器优雅退出：强制终止收敛
                    subMachine.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                    subMachine.Dispose();
                }
            }
        }

        /// <summary>子机器状态变更转发：父状态钩子（切换时）+ 层级 StateChanged 通知（透传子机器的 from/to）。</summary>
        private void OnSubStateEntered(StateChangeArgs args)
        {
            var subState = args.To;

            if (!_initialSubEntry && _current is CompositeStateBase composite && composite.SubMachine != null)
            {
                try
                {
                    composite.OnSubStateChanged(subState);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
            }

            RaiseStateChanged(args);
        }

        private async UniTask InitStateAsync(StateBase state, CancellationToken ct)
        {
            try
            {
                state.OnInit();
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                RaiseTransitionException(state.GetType(), ex);
            }

            try
            {
                await state.InitAsync(ct).AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                RaiseTransitionException(state.GetType(), ex);
            }
        }

        private async UniTask ExitStateAsync(StateBase state, CancellationToken ct)
        {
            // 同步 OnExit 先于可取消的异步退出执行：同步拆除不被取消跳过
            try
            {
                state.OnExit();
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                RaiseTransitionException(state.GetType(), ex);
            }

            try
            {
                await state.ExitAsync(ct).AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                RaiseTransitionException(state.GetType(), ex);
            }
        }

        private async UniTask EnterStateAsync(StateBase state, CancellationToken ct)
        {
            try
            {
                state.OnEnter();
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                RaiseTransitionException(state.GetType(), ex);
            }

            try
            {
                await state.EnterAsync(ct).AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                RaiseTransitionException(state.GetType(), ex);
            }
        }

        private void StartFireAndForget(Type targetType, Func<StateBase> factory, CancellationToken ct)
        {
            RunFireAndForgetAsync(targetType, factory, ct).Forget();
        }

        private async UniTaskVoid RunFireAndForgetAsync(Type targetType, Func<StateBase> factory, CancellationToken ct)
        {
            try
            {
                await TransitionAsync(factory(), ct, fireAndForget: true);
            }
            catch (Exception ex)
            {
                // 兜底：业务异常与 OCE 已在 TransitionAsync 内处理；此处捕获构造/注入等残余异常，避免未观察异常
                Log.Exception(ex);
                RaiseTransitionException(targetType, ex);
            }
        }

        private void ConsumePending()
        {
            if (_terminated || !_pending.HasValue)
            {
                return;
            }

            var pending = _pending.Value;
            _pending = null;

            // 幂等重查：目标在当前分支内（叶子或顶层/祖先）→ 跳过
            if (IsCurrent(pending.Type))
            {
                return;
            }

            StartFireAndForget(pending.Type, pending.Factory, pending.Token);
        }

        private void RaiseTransitionException(Type stateType, Exception ex)
        {
            try
            {
                TransitionException?.Invoke(stateType, ex);
            }
            catch (Exception hookEx)
            {
                Log.Exception(hookEx);
            }
        }

        /// <summary>触发状态变更事件（携带 from/to）；订阅者异常隔离，不中断转移流程。</summary>
        private void RaiseStateChanged(StateChangeArgs args)
        {
            try
            {
                StateChanged?.Invoke(args);
            }
            catch (Exception hookEx)
            {
                Log.Exception(hookEx);
            }
        }

        /// <summary>挂起转移请求：目标类型 + 状态工厂 + 取消令牌，仅保留最新。</summary>
        private readonly struct PendingRequest
        {
            public readonly Type Type;
            public readonly Func<StateBase> Factory;
            public readonly CancellationToken Token;

            public PendingRequest(Type type, Func<StateBase> factory, CancellationToken token)
            {
                Type = type;
                Factory = factory;
                Token = token;
            }
        }
        public readonly struct StateChangeArgs
        {
            public readonly StateBase To;
            public readonly StateBase From;

            public StateChangeArgs(StateBase to, StateBase from)
            {
                To = to;
                From = from;
            }
        }
    }
}
