using System.Collections.Generic;
using UnityEngine;
using XeptGame.Equip;
using XeptGame.Game.Flow;
using XeptGame.Interaction;
using XeptGame.Items;
using XeptKit.Core;

namespace XeptGame.World.Interactables
{
    /// <summary>
    /// 【测试用】世界可拾取物（宿主模型 v3.1+；WorldItem_Design.md + Equip_FPV 决议 DP5）：场景直摆可拾取载体——
    /// 序列化持有 ItemDefinition，扮演宿主（ISelectable + IInteractionActionsHost + IInteractionInfoContext）。
    /// <list type="bullet">
    /// <item><b>双动作（成员恒定）</b>：Primary = 拾取（tap → 路由：手空且可持则 1 上手 N−1 入包，否则全入包）；
    /// Hold = 拿取（长按 E → 强制上手，手满先回包）——由 InteractionExecutor 的 E 手势判别分派；</item>
    /// <item><b>拾取逻辑收口命令层</b>：经 <c>GameplayEntry.Instance.Commands.TryPickupRoute</c>（DP3 守卫纪律——
    /// 宿主只发命令，不直接读写容器）；入包/上手状态轨与播报（事件轨）都由命令层统一处理；</item>
    /// <item>拾取成功隐藏自身（防重复拾取、不 Destroy——可复位/重生系统接管）。</item>
    /// </list>
    /// </summary>
    public sealed class WorldItem : MonoBehaviour, ISelectable, IInteractionActionsHost, IInteractionInfoContext
    {
        [Tooltip("对应的物品定义（WorldFacet 等条目数据源）；必填——缺失时不可交互")]
        [SerializeField] private ItemDefinition definition;

        [Tooltip("每次拾取数量（世界堆叠 per-instance，不进 Definition）")]
        [SerializeField] private int pickupCount = 1;

        private readonly List<IInteractionAction> _actions = new();
        private PickupAction _pickup;
        private TakeInHandAction _takeInHand;
        private bool _isSelected;

        /// <summary>绑定的物品定义。</summary>
        public ItemDefinition Definition => definition;

        /// <inheritdoc />
        public bool IsSelected => _isSelected;

        /// <inheritdoc />
        public IReadOnlyList<IInteractionAction> Actions => _actions;

        // ---- IInteractionInfoContext（交互信息：名字 = Definition 本地化键；图标 = Definition，IconKind 感知）----

        /// <inheritdoc />
        public string DisplayNameKey => definition != null ? definition.DisplayNameKey : string.Empty;

        /// <inheritdoc />
        public string DisplayName => string.Empty; // 无直显文案（走键解析，未注册即隐名）

        /// <inheritdoc />
        public Sprite IconSprite => definition != null ? definition.IconSprite : null;

        /// <inheritdoc />
        public Texture IconTexture => definition != null ? definition.IconTexture : null;

        private void Awake()
        {
            if (definition == null)
            {
                Log.Error($"[WorldItem] {name}: definition 未接线（应在 Inspector 指定 ItemDefinition），不可交互。");
            }

            _pickup = new PickupAction(this);
            _takeInHand = new TakeInHandAction(this);
            _actions.Add(_pickup);
            _actions.Add(_takeInHand);
        }

        /// <summary>是否可拾取（动作门控数据源）。</summary>
        public bool CanPickup => definition != null && pickupCount > 0;

        /// <summary>
        /// 执行拾取（动作 Interact 收口）：把目标/模式交给命令层路由（状态轨 + 播报由命令层统一处理），
        /// 成功后隐藏自身。
        /// </summary>
        public bool TryPickup(PickupMode mode)
        {
            if (definition == null)
            {
                Log.Error($"[WorldItem] {name}: definition 未接线，无法拾取。");
                return false;
            }

            if (pickupCount <= 0)
            {
                Log.Error($"[WorldItem] {name}: pickupCount 必须为正（当前 {pickupCount}）。");
                return false;
            }

            if (!GameplayEntry.TryGetInstance(out var entry))
            {
                Log.Error("[WorldItem] 一轮会话未初始化（GameplayEntry），无法拾取。");
                return false;
            }

            if (!entry.Commands.TryPickupRoute(definition, pickupCount, mode))
            {
                Log.Error($"[WorldItem] 拾取路由失败 {definition.Id} × {pickupCount}（{mode}，{name}）。");
                return false;
            }

            Log.Info($"[WorldItem] 拾取 {definition.Id} × {pickupCount}（{mode}，{name}）");

            gameObject.SetActive(false); // 拾取后隐藏：防重复拾取；不 Destroy（可复位/重生系统接管）
            return true;
        }

        /// <inheritdoc />
        public bool CanSelect(InteractionContext context) => definition != null;

        /// <inheritdoc />
        public void OnSelected(InteractionContext context) => _isSelected = true;

        /// <inheritdoc />
        public void OnDeselected() => _isSelected = false;

        /// <summary>拾取动作（tap，Primary；纯 C#，构造注入宿主）。</summary>
        private sealed class PickupAction : IInteractionAction
        {
            private readonly WorldItem _host;

            public PickupAction(WorldItem host)
            {
                _host = host;
            }

            public InputSlot Slot => InputSlot.Primary;

            public string PromptText => "拾取";

            public bool CanInteract(InteractionContext context) => _host.CanPickup;

            public void Interact(InteractionContext context) => _host.TryPickup(PickupMode.Tap);
        }

        /// <summary>拿取动作（长按，Hold；纯 C#，构造注入宿主）。</summary>
        private sealed class TakeInHandAction : IInteractionAction
        {
            private readonly WorldItem _host;

            public TakeInHandAction(WorldItem host)
            {
                _host = host;
            }

            public InputSlot Slot => InputSlot.Hold;

            public string PromptText => "拿取";

            public bool CanInteract(InteractionContext context) => _host.CanPickup;

            public void Interact(InteractionContext context) => _host.TryPickup(PickupMode.ForceHold);
        }
    }
}
