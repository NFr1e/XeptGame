using System;
using UnityEngine;
using XeptGame.Inv;
using XeptGame.Items;
using XeptGame.Items.Operations;

namespace XeptGame.World
{
    /// <summary>
    /// 旧包去向的当前实现（Item_Instance_Design.md §4/§5.3）：把换下的背包<b>整包落进世界记录</b>。
    /// <list type="bullet">
    /// <item>落点由装配点给的 <see cref="Func{Vector3}"/> 提供（"身上 → 脚下"）；领域层不认识玩家位置；</item>
    /// <item>记录创建失败 = 接收失败（编排器会把新包摘回、旧包放回背槽，不留半换状态）；</item>
    /// <item>⏳ 未来：脚本没收 / 营地仓库 —— 同一端口的新实现。</item>
    /// </list>
    /// </summary>
    public sealed class WorldDropDestination : IWorldDropPort
    {
        private readonly WorldDropFactory _factory;
        private readonly Func<Vector3> _positionProvider;
        private readonly string _groupId;

        public WorldDropDestination(WorldDropFactory factory, Func<Vector3> positionProvider, string groupId = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _positionProvider = positionProvider;
            _groupId = groupId;
        }

        /// <inheritdoc />
        public bool TryAccept(ContainerInstance carrier, out string reason)
        {
            var position = _positionProvider != null ? _positionProvider() : Vector3.zero;
            var record = _factory.DropCarrier(carrier, position, _groupId);
            if (record == null)
            {
                reason = "DropRejected";
                return false;
            }

            reason = null;
            return true;
        }

        /// <inheritdoc />
        public bool TryAcceptStack(ItemDefinition definition, int count, out string reason)
        {
            var position = _positionProvider != null ? _positionProvider() : Vector3.zero;
            var record = _factory.DropStack(definition, count, position, _groupId);
            if (record == null)
            {
                reason = "DropRejected";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
