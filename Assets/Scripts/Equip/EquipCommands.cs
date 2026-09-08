using System;
using XeptGame.Inv;
using XeptGame.Items;

namespace XeptGame.Equip
{
    /// <summary>
    /// 拾取路由模式（Equip_FPV_Design.md §2.4，T3）：tap（点按 E）= 手空则上手否则入包；
    /// ForceHold（长按 E）= 强制上手（手满先回包）。
    /// </summary>
    public enum PickupMode
    {
        /// <summary>点按：手空且可持 → 1 上手（不収包）、余数入包；否则全入包。</summary>
        Tap = 0,

        /// <summary>长按：强制上手（手满先把当前物收回背包）；不可持/已持同物 → 全入包。</summary>
        ForceHold = 1,
    }

    /// <summary>
    /// 装备命令层（行为轴唯一写入口，Equip_FPV_Design.md §2.3/§2.4/§3.3，T3）：
    /// 守卫 + 编排收口于此——WorldItem 动作、G 输入桥等发起方<b>只发命令</b>，不直接读写容器
    /// （DP3 守卫纪律：守卫查询归行为轴）。
    /// <list type="bullet">
    /// <item>依赖注入：背包（IItemContainer，行容器）/ 身体（<see cref="Equipment"/>，槽容器）/
    /// 一次性播报回调（onAcquired，装配点接 Gameplay 域总线，命令层不依赖总线基础设施）；</item>
    /// <item><b>意图与容器操作分离</b>：容器间搬运经 <see cref="ContainerTransfer.Move"/>（哑原语）；
    /// 本层只表达"拾取该怎么路由 / 收起 = 转移 + 意图终止"的意图；</item>
    /// <item>纯 C# 可单测（路由矩阵见 Equip_FPV_Design.md §8 / Equip_FPV_Implement.md T3）。</item>
    /// </list>
    /// </summary>
    public sealed class EquipCommands
    {
        private readonly IItemContainer _bag;
        private readonly Equipment _body;
        private readonly Action<ItemAcquiredEvent> _onAcquired;

        public EquipCommands(IItemContainer bag, Equipment body, Action<ItemAcquiredEvent> onAcquired = null)
        {
            _bag = bag;
            _body = body;
            _onAcquired = onAcquired;
        }

        /// <summary>
        /// 拾取路由（tap/hold 同走一命令，mode 区分）：
        /// <list type="bullet">
        /// <item>Tap：手空且可持 → 1 上手（不収包），count−1 入包（count=1 即整份上手）；否则全入包；</item>
        /// <item>ForceHold：强制上手——手满先当前物回包再上手，余数入包；不可持 / 已持同物 → 全入包。</item>
        /// </list>
        /// 成功后一次性播报（获得总量 N，不分落点）。守卫（def/count 非法）返回 false 且零副作用。
        /// </summary>
        public bool TryPickupRoute(ItemDefinition definition, int count, PickupMode mode)
        {
            if (definition == null || count <= 0)
            {
                return false;
            }

            if (mode == PickupMode.Tap)
            {
                if (_body.TryAdd(definition, 1))
                {
                    DepositRemainder(definition, count - 1);
                    PublishAcquired(definition, count);
                    return true;
                }

                return AddAllToBag(definition, count);
            }

            // ForceHold
            var current = _body.Get(BodySlotType.Hand);
            if (current != null)
            {
                if (ReferenceEquals(current, definition))
                {
                    return AddAllToBag(definition, count); // 已持同物：不可叠手，全入包
                }

                if (!ContainerTransfer.Move(_body, _bag, current, 1))
                {
                    return false; // 当前物回包失败（异常路径：包不应拒绝），调用方按失败处理
                }
            }

            if (_body.TryAdd(definition, 1))
            {
                DepositRemainder(definition, count - 1);
                PublishAcquired(definition, count);
                return true;
            }

            return AddAllToBag(definition, count);
        }

        /// <summary>
        /// 收起（G）：手槽 → 背包转移 + 意图终止——v1 无活跃搬运源，转移后手槽回闲置且<b>不补位</b>。
        /// 空手无可收起 → 返回 false（无副作用）。
        /// </summary>
        public bool PutAway()
        {
            var held = _body.Get(BodySlotType.Hand);
            if (held == null)
            {
                return false;
            }

            return ContainerTransfer.Move(_body, _bag, held, 1);
        }

        private bool AddAllToBag(ItemDefinition definition, int count)
        {
            if (!_bag.TryAdd(definition, count))
            {
                return false;
            }

            PublishAcquired(definition, count);
            return true;
        }

        private void DepositRemainder(ItemDefinition definition, int remainder)
        {
            if (remainder > 0)
            {
                _bag.TryAdd(definition, remainder);
            }
        }

        private void PublishAcquired(ItemDefinition definition, int count)
            => _onAcquired?.Invoke(new ItemAcquiredEvent(definition, count));
    }
}
