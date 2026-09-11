using UnityEngine;
using XeptGame.Inv;
using XeptGame.Items;

namespace XeptGame.World
{
    /// <summary>
    /// 世界掉落工厂（Item_Instance_Design.md §5.3）：把"要落到世界的东西"变成<b>一条记录</b>——
    /// 视图由 <see cref="WorldViewSpawner"/> 订阅记录表生成，本类不碰场景（"记录层是真相"，不变量 I3）。
    /// <list type="bullet">
    /// <item>实例（背包等）→ <b>整包落地</b>：记录携带同一个实例对象，内容不动（I7）；</item>
    /// <item>无状态堆叠 → 记数量；</item>
    /// <item>返回 null = 记录被拒绝（分组超上限等），调用方按失败处理。</item>
    /// </list>
    /// </summary>
    public sealed class WorldDropFactory
    {
        private readonly WorldRecordStore _records;

        public WorldDropFactory(WorldRecordStore records)
        {
            _records = records ?? throw new System.ArgumentNullException(nameof(records));
        }

        /// <summary>掉落一个背包实例（整包，内容不动）。</summary>
        public WorldRecord DropCarrier(ContainerInstance carrier, Vector3 position, string groupId = null)
        {
            if (carrier == null)
            {
                return null;
            }

            return _records.Add(carrier.Definition, 1, position, groupId ?? WorldRecordStore.DefaultGroup, carrier);
        }

        /// <summary>掉落一堆无状态物品。</summary>
        public WorldRecord DropStack(ItemDefinition definition, int count, Vector3 position, string groupId = null)
        {
            if (definition == null || count <= 0)
            {
                return null;
            }

            return _records.Add(definition, count, position, groupId ?? WorldRecordStore.DefaultGroup);
        }
    }
}
