using System;
using System.Collections.Generic;
using UnityEngine;
using XeptGame.Interaction;
using XeptGame.Interaction.Behaviours;
using XeptKit.Core;

namespace XeptGame.World.Interactables.Demo
{
    /// <summary>
    /// 演示用双态交互物（火堆占位，仅验证"动作清单/宿主状态翻转"，非生存内容）：
    /// <b>可交互目标（<see cref="IFireTarget"/>）</b> + **宿主（ISelectable + IInteractionActionsHost）** +
    /// **成员变化通知（IInteractionActionsNotifier）** + 信息上下文（名字/图标）。
    /// <list type="bullet">
    /// <item>状态 = { 灭/燃 } × fuel(1..<see cref="MaxFuel"/>)；</item>
    /// <item><b>成员从总名单筛出来</b>（v5）：灭 → 只有点燃；燃 → 熄灭 + 添柴。燃料满**不改成员**，
    /// 只让添柴变灰（可用性 = 行为自己的 <c>CanAct</c>）；</item>
    /// <item>对象名走本地化键（<c>campfire.name</c>），不再是代码中文；序列化字段仅作无键回退。</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoCampfireProp : MonoBehaviour,
        ISelectable, IInteractionActionsHost, IInteractionActionsNotifier, IInteractionInfoContext, IFireTarget
    {
        public const int MaxFuel = 3;

        /// <summary>对象名本地化键（值在 CSV：火堆 / Campfire）。</summary>
        public const string NameKey = "campfire.name";

        [SerializeField] private bool startLit;

        [Header("提示头部（火堆无 Item 定义，直配字段）")]
        [Tooltip("交互物名字（无键回退直显；有键时以键为准）")]
        [SerializeField] private string displayName = "火堆";

        [Tooltip("Sprite 图标（可选）")]
        [SerializeField] private Sprite iconSprite;

        [Tooltip("纹理图标（可选；与 Sprite 二选一）")]
        [SerializeField] private Texture iconTexture;

        private readonly List<IInteractionAction> _actions = new();

        /// <summary>是否燃烧。</summary>
        public bool IsLit { get; private set; }

        /// <summary>当前燃料量（1..<see cref="MaxFuel"/>）。</summary>
        public int Fuel { get; private set; }

        /// <inheritdoc />
        public event Action ActionsChanged;

        /// <inheritdoc />
        public bool IsSelected { get; private set; }

        /// <inheritdoc />
        public IReadOnlyList<IInteractionAction> Actions => _actions;

        /// <summary>是否可添柴（燃且未满）。</summary>
        public bool CanAddFuel => IsLit && Fuel < MaxFuel;

        // ---- IInteractionInfoContext（名字走本地化键；图标直配）----

        /// <inheritdoc />
        public string DisplayNameKey => NameKey;

        /// <inheritdoc />
        public string DisplayName => displayName;

        /// <inheritdoc />
        public Sprite IconSprite => iconSprite;

        /// <inheritdoc />
        public Texture IconTexture => iconTexture;

        private void Awake()
        {
            if (startLit)
            {
                IgniteInternal();
            }
            else
            {
                Fuel = 1;
            }

            RebuildActions();
        }

        /// <inheritdoc />
        public bool CanSelect(InteractionContext context) => true;

        /// <inheritdoc />
        public void OnSelected(InteractionContext context) => IsSelected = true;

        /// <inheritdoc />
        public void OnDeselected() => IsSelected = false;

        /// <inheritdoc />
        public bool TryIgnite()
        {
            if (IsLit)
            {
                return false;
            }

            IgniteInternal();
            RebuildActions();
            Log.Info("[DemoCampfire] 点燃。");
            return true;
        }

        /// <inheritdoc />
        public bool TryExtinguish()
        {
            if (!IsLit)
            {
                return false;
            }

            IsLit = false;
            Fuel = 0;
            RebuildActions();
            Log.Info("[DemoCampfire] 熄灭。");
            return true;
        }

        /// <inheritdoc />
        public bool TryAddFuel()
        {
            if (!CanAddFuel)
            {
                return false;
            }

            Fuel++;
            Log.Info($"[DemoCampfire] 添柴（燃料 {Fuel}/{MaxFuel}）。");
            return true;
        }

        private void IgniteInternal()
        {
            IsLit = true;
            Fuel = Mathf.Max(1, Fuel);
        }

        /// <summary>按状态从总名单重建动作集（成员 = 列表存在）并通知（离散状态迁移，DP3 事件轨）。</summary>
        private void RebuildActions()
        {
            var next = new List<BoundAction>(InteractionBehaviourCatalog.All.Count);
            for (int i = 0; i < InteractionBehaviourCatalog.All.Count; i++)
            {
                var behaviour = InteractionBehaviourCatalog.All[i];
                if (behaviour.CanBuildOn(this, null))
                {
                    next.Add(new BoundAction(behaviour, this));
                }
            }

            if (!MembersChanged(next))
            {
                return;
            }

            _actions.Clear();
            for (int i = 0; i < next.Count; i++)
            {
                _actions.Add(next[i]);
            }

            ActionsChanged?.Invoke();
        }

        private bool MembersChanged(List<BoundAction> next)
        {
            if (next.Count != _actions.Count)
            {
                return true;
            }

            for (int i = 0; i < next.Count; i++)
            {
                var current = _actions[i] as BoundAction;
                if (current == null || !ReferenceEquals(next[i].Behaviour, current.Behaviour))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
