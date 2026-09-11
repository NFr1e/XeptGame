using UnityEngine;
using XeptGame.Items;

namespace XeptGame.World
{
    /// <summary>
    /// 一条世界记录（Item_Instance_Design.md §5.1）——<b>记录层是真相</b>，场景物体只是它的视图。
    /// <list type="bullet">
    /// <item><b>身份</b>：<see cref="Id"/> 与实例 id <b>共用同一个签发器</b>（DP3）——堆叠记录也有主键，
    /// 删除/更新才有地址；</item>
    /// <item><b>只存定义引用</b>：存档层只写定义 key，定义缺失时可逆（不变量 I6）；</item>
    /// <item><b>两种记录</b>：无状态堆叠（<see cref="Instance"/> 为 null，记数量）与有状态实例
    /// （<see cref="Instance"/> 非 null，数量恒 1，不变量 I2）；</item>
    /// <item>位置与数量可被记录层改写（<c>internal set</c>，只有 <see cref="WorldRecordStore"/> 能改）；
    /// rotation / 清理策略等字段等真实消费者出现再加。</item>
    /// </list>
    /// </summary>
    public sealed class WorldRecord
    {
        internal WorldRecord(long id, ItemDefinition definition, int count, Vector3 position, string groupId, ItemInstance instance)
        {
            Id = id;
            Definition = definition;
            Count = count;
            Position = position;
            GroupId = groupId;
            Instance = instance;
        }

        /// <summary>记录身份（与实例 id 同空间；0 = 无记录，哨兵不用于记录）。</summary>
        public long Id { get; }

        /// <summary>内容定义（静态内容层引用；运行时只读）。</summary>
        public ItemDefinition Definition { get; }

        /// <summary>数量（实例记录恒 1）。</summary>
        public int Count { get; internal set; }

        /// <summary>世界位置（视图据此摆放；rotation 暂不入档）。</summary>
        public Vector3 Position { get; internal set; }

        /// <summary>所属分组（当前 = 关卡 id；chunk 化留到流式实现）。</summary>
        public string GroupId { get; }

        /// <summary>有状态实例（背包等）；null = 无状态堆叠记录。</summary>
        public ItemInstance Instance { get; }

        /// <summary>是否是有状态实例记录（数量恒 1，不参与堆叠合并）。</summary>
        public bool IsInstanceRecord => Instance != null;

        public override string ToString()
            => $"WorldRecord({Id}:{Definition?.name} × {Count} @ {GroupId})";
    }
}
