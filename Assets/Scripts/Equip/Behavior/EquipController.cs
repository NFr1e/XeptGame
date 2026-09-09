using System;
using UnityEngine;
using XeptGame.Items;
using XeptKit.Event;
using XeptKit.FSM;

namespace XeptGame.Equip
{
    /// <summary>
    /// 装备行为控制器（EB-01）：管理拿出、就绪、收回与打断，不执行容器转移。
    /// <list type="bullet">
    /// <item><b>相位唯一权威 = FSM 状态</b>：Context 不存 Phase；对外快照与转移表使用 EquipPhase 枚举视图
    /// （由 Fsm 层级查询推导，见 <see cref="CurrentPhase"/>）——无双份相位写者；</item>
    /// <item><b>规则 = 表驱动</b>：相位 × 请求的答复/目标在 <see cref="EquipRequestTable"/>（对齐行为文档 §3.2），
    /// 本类只做守卫短路、执行转移（<see cref="RequestState"/>）与事件发布；</item>
    /// <item><b>实现前提（同步收敛不变量）</b>：请求/自转触发的转移在本调用栈内同步收敛（状态钩子全同步）——
    /// 故请求返回后读取 Fsm 即得目标态；若未来引入真异步钩子或跨帧 pending，需重估本前提并回到
    /// "按请求语义推导/等收敛事件发布"（见文档 Q1 讨论）；</item>
    /// <item>状态实例由 XeptKit.FSM 缓存复用，会话数据（占用/计时/暂停/时长）统一保存在 <see cref="EquipContext"/>；
    /// 操作编排在提交容器变化后 Reconcile 占用，呈现经快照读取相位与进度；
    /// 与 Motor HFSM 互不查询，游戏暂停由组合根显式门控。</item>
    /// </list>
    /// </summary>
    public sealed class EquipController : IDisposable
    {
        private readonly EquipContext _context = new();
        private readonly Fsm _fsm;

        /// <summary>快照事件（SafeEvent 对象事件原语：异常隔离 + 订阅去重——替代 EventBus 的错配用法）。</summary>
        private readonly SafeEvent<EquipSnapshot> _snapshotChanged = new();

        /// <summary>动作终局事件（SafeEvent）。</summary>
        private readonly SafeEvent<EquipActionResult> _actionFinished = new();

        private bool _disposed;
        private bool _notifying;

        /// <summary>当前只读行为快照（Phase 由 Fsm 推导）；不构成另一份可修改的物品占用。</summary>
        public EquipSnapshot Snapshot => new(_context, CurrentPhase);

        /// <summary>正常转出门：已收回且未暂停；目标容器接纳仍由协调器处理。</summary>
        public bool CanTransferOut => !_disposed && !_context.Paused && IsIn(EquipPhase.Stowed);

        /// <summary>阶段、进度或暂停变化（SafeEvent 发布：异常隔离 + 订阅去重）；观察者不得在通知中重入行为请求。</summary>
        public event Action<EquipSnapshot> SnapshotChanged
        {
            add => _snapshotChanged.Add(value);
            remove => _snapshotChanged.Remove(value);
        }

        /// <summary>动作终局：正常完成、被后继动作替代或占用失效中止（SafeEvent 发布）。</summary>
        public event Action<EquipActionResult> ActionFinished
        {
            add => _actionFinished.Add(value);
            remove => _actionFinished.Remove(value);
        }

        /// <summary>时长随占用携带（<see cref="EquipContext.Timing"/>，编排在 Reconcile 时注入），本控制器无全局时长配置。</summary>
        public EquipController()
        {
            _fsm = new Fsm(_context);
            _fsm.RequestChange<EmptyState>();
        }

        /// <summary>暂停/恢复行为：暂停时冻结时钟、拒绝新请求与转出，不撤销已提交的物品占用。</summary>
        public void SetPaused(bool paused)
        {
            if (_disposed || _context.Paused == paused)
            {
                return;
            }

            _context.Paused = paused;
            Publish();
        }

        /// <summary>仅编排器在同步转移结束后调用：占用 + 该物品拿放时长一起携带；失败回滚不得制造新身份。</summary>
        public void ReconcileOccupancy(ItemDefinition item, long version, EquipTiming timing)
        {
            if (_disposed || _notifying)
            {
                return;
            }

            if (_context.Version == version && ReferenceEquals(_context.Item, item))
            {
                return;
            }

            ValidateTiming(timing);
            var active = IsMoving;
            var previous = _context.ActionId;
            _context.Item = item;
            _context.Version = version;
            _context.Timing = item != null ? timing : EquipTiming.Default; // 空手时长无意义，保持缺省
            _context.Elapsed = 0;
            _context.Duration = 0;
            RequestState(item == null ? EquipPhase.Empty : EquipPhase.Stowed); // 幂等同态跳过；同步收敛
            Publish(active ? new EquipActionResult(previous, EquipActionOutcome.Aborted) : null);
        }

        /// <summary>从 Stowed 请求拿出；重复请求幂等，收回中不能反向打断（规则见 EquipRequestTable）。</summary>
        public BehaviorRequest RequestDraw()
        {
            if (_disposed || _notifying || _context.Paused || _context.Item == null)
            {
                return BehaviorRequest.Rejected;
            }

            return Apply(EquipRequestTable.Evaluate(CurrentPhase, EquipRequest.Draw), stowSide: false);
        }

        /// <summary>请求收回；可以打断 Drawing，收回完成后仍保持槽位占用（规则见 EquipRequestTable）。</summary>
        public BehaviorRequest RequestStow()
        {
            if (_disposed || _notifying || _context.Paused)
            {
                return BehaviorRequest.Rejected;
            }

            return Apply(EquipRequestTable.Evaluate(CurrentPhase, EquipRequest.Stow), stowSide: true);
        }

        /// <summary>由操作编排驱动；一次 Tick 最多完成当前动作，不透支下一动作的时间。</summary>
        public void Tick(float gameplayDelta)
        {
            // Q4：热路径不做抛校验——非法 delta 属编程错误：开发态 Debug.Assert 提示（Release 剥离），
            // 另加无抛兜底忽略坏帧（防 Release 下 NaN 卡死状态机）。
            Debug.Assert(
                gameplayDelta >= 0f && !float.IsNaN(gameplayDelta) && !float.IsInfinity(gameplayDelta),
                "[EquipController] Tick 收到非法 deltaTime（NaN/Inf/负）。");
            if (gameplayDelta < 0f || float.IsNaN(gameplayDelta) || float.IsInfinity(gameplayDelta))
            {
                return;
            }

            if (_disposed || _notifying || _context.Paused)
            {
                return;
            }

            var moving = IsMoving;
            _fsm.Tick(gameplayDelta);
            if (moving && !IsMoving)
            {
                Publish(new EquipActionResult(_context.ActionId, EquipActionOutcome.Completed));
            }
            else if (moving)
            {
                Publish();
            }
        }

        // ---- 内部 ----

        private BehaviorRequest Apply(EquipRequestOutcome outcome, bool stowSide)
        {
            if (outcome.Reply != BehaviorRequest.Started || outcome.Target == null)
            {
                return outcome.Reply;
            }

            // 打断语义：收回请求打断"拿出中" → 旧动作 Superseded（在 Start 自增前取当前动作号）。
            EquipActionResult? ended = null;
            if (stowSide && CurrentPhase == EquipPhase.Drawing)
            {
                ended = new EquipActionResult(_context.ActionId, EquipActionOutcome.Superseded);
            }

            Start(outcome.Target.Value,
                stowSide ? _context.Timing.StowSeconds : _context.Timing.DrawSeconds,
                ended);
            return BehaviorRequest.Started;
        }

        private void Start(EquipPhase target, float duration, EquipActionResult? ended = null)
        {
            _context.ActionId++;
            _context.Elapsed = 0;
            _context.Duration = duration;
            RequestState(target);
            Publish(ended);
        }

        /// <summary>相位 → 状态类型转移请求（同态幂等跳过；同步收敛不变量下调用后即达目标态；类型由 EquipPhaseMap 单表维护）。</summary>
        private void RequestState(EquipPhase phase)
        {
            _fsm.RequestChange(EquipPhaseMap.ToState(phase));
        }

        private bool IsMoving => IsIn(EquipPhase.Drawing) || IsIn(EquipPhase.Stowing);

        /// <summary>相位查询：Fsm 层级查询（相位 → 状态类型由 <see cref="EquipPhaseMap"/> 单表维护）。</summary>
        private bool IsIn(EquipPhase phase)
            => _fsm.IsInHierarchy(EquipPhaseMap.ToState(phase));

        /// <summary>当前相位枚举视图（由 Fsm 状态推导；Empty 为兜底）。</summary>
        private EquipPhase CurrentPhase
        {
            get
            {
                if (IsIn(EquipPhase.Stowed))
                {
                    return EquipPhase.Stowed;
                }

                if (IsIn(EquipPhase.Drawing))
                {
                    return EquipPhase.Drawing;
                }

                if (IsIn(EquipPhase.Ready))
                {
                    return EquipPhase.Ready;
                }

                if (IsIn(EquipPhase.Stowing))
                {
                    return EquipPhase.Stowing;
                }

                return EquipPhase.Empty;
            }
        }

        private void Publish(EquipActionResult? ended = null)
        {
            _notifying = true;
            try
            {
                _snapshotChanged.Invoke(Snapshot);
                if (ended.HasValue)
                {
                    _actionFinished.Invoke(ended.Value);
                }
            }
            finally
            {
                _notifying = false;
            }
        }

        /// <summary>数值校验（抛）：仅供装配/配置期（Reconcile 的 timing、初始化参数）；热路径勿调用（见 Tick 的 Debug.Assert + 忽略策略，Q4）。</summary>
        private static void ValidateTime(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private static void ValidateTiming(EquipTiming timing)
        {
            ValidateTime(timing.drawSeconds);
            ValidateTime(timing.stowSeconds);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            var active = IsMoving;
            _disposed = true;
            if (active)
            {
                // Q5 顺序：先广播中止（FSM 仍有效，快照/终局可安全构造；监听者如编排/呈现先收终局清状态）
                // → 再释放 FSM → 最后清事件（终局已送达，不再需要订阅者）。
                Publish(new EquipActionResult(_context.ActionId, EquipActionOutcome.Aborted));
            }

            _fsm.Dispose();
            _snapshotChanged.Clear();
            _actionFinished.Clear();
        }
    }
}
