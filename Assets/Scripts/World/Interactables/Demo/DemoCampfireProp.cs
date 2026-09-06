using System;
using System.Collections.Generic;
using UnityEngine;
using XeptGame.Interaction;
using XeptKit.Core;

namespace XeptGame.World.Interactables.Demo
{
    /// <summary>
    /// 演示用双态交互物（火堆占位，仅验证"动作清单/宿主状态翻转"，非生存内容）：
    /// **宿主（ISelectable + IInteractionActionsHost）**，动作 = 宿主内部纯 C# 列表（构造注入，无 Unity 纠缠）。
    /// 状态 = { 灭/燃 } × fuel(1..<see cref="MaxFuel"/>)；**成员 = 列表增删**（灭 → 仅 IgniteAction；
    /// 燃 → ExtinguishAction + AddFuelAction），状态迁移时重建列表并触发 <see cref="ActionsChanged"/>（DP3 事件轨）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoCampfireProp : MonoBehaviour, ISelectable, IInteractionActionsHost, IInteractionDisplayInfo
    {
        public const int MaxFuel = 3;

        [SerializeField] private bool startLit;

        [Header("提示头部（火堆无 Item 定义，直配字段）")]
        [Tooltip("交互物名字（直显文案；空 = 提示隐藏名字）")]
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

        // ---- IInteractionDisplayInfo（提示头部；直配字段，无本地化键）----

        /// <inheritdoc />
        public string DisplayNameKey => string.Empty;

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

        /// <summary>按状态重建动作集（成员 = 列表存在）并通知（离散状态迁移，DP3 事件轨）。</summary>
        private void RebuildActions()
        {
            _actions.Clear();

            if (IsLit)
            {
                _actions.Add(new ExtinguishAction(this));
                _actions.Add(new AddFuelAction(this));
            }
            else
            {
                _actions.Add(new IgniteAction(this));
            }

            ActionsChanged?.Invoke();
        }
    }
}
