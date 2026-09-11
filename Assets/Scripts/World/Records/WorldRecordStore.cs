using System;
using System.Collections.Generic;
using UnityEngine;
using XeptGame.Core;
using XeptGame.Items;
using XeptKit.Core;
using XeptKit.Event;

namespace XeptGame.World
{
    /// <summary>
    /// 世界记录表（Item_Instance_Design.md §5.1）——<b>世界记录是真相，场景物体是视图</b>（不变量 I3）。
    /// <list type="bullet">
    /// <item><b>只由本类发起增删</b>：视图的 <c>OnDestroy</c>/场景卸载<b>绝不触碰</b>记录层；</item>
    /// <item><b>id 与实例 id 共用签发器</b>（DP3）：堆叠记录也有主键；</item>
    /// <item><b>按分组有界</b>（不变量 I8）：组内条目数超上限 → <b>拒绝写入并告警</b>，
    /// 不做无界追加（Valheim 的 ZDO 无界增长 → 存档损坏 → 只能清库，是反面教材）；</item>
    /// <item><b>分组当前 = 关卡 id</b>：chunk 字段等流式实现（T7）再加，不预置；</item>
    /// <item>纯 C#（唯一引擎耦合 = <see cref="Vector3"/> 位置与定义引用），可 EditMode 全覆盖。</item>
    /// </list>
    /// </summary>
    public sealed class WorldRecordStore
    {
        /// <summary>未显式指定分组时的默认组（场景直摆内容等）。</summary>
        public const string DefaultGroup = "default";

        private readonly Dictionary<long, WorldRecord> _byId = new();
        private readonly Dictionary<string, List<long>> _byGroup = new();
        private readonly SafeEvent<WorldRecordChangeArgs> _changed = new();
        private readonly InstanceIdAllocator _ids;
        private readonly int _maxPerGroup;
        private readonly Action<string> _warn;

        public WorldRecordStore(InstanceIdAllocator ids, int maxRecordsPerGroup = 0, Action<string> warn = null)
        {
            Guard.NotNull(ids, nameof(ids));
            _ids = ids;
            _maxPerGroup = maxRecordsPerGroup > 0 ? maxRecordsPerGroup : XeptGameConsts.World.MaxRecordsPerGroup;
            _warn = warn;
        }

        /// <summary>记录变更轨（增/删/改）——视图层的唯一订阅点。</summary>
        public event Action<WorldRecordChangeArgs> Changed
        {
            add => _changed.Add(value);
            remove => _changed.Remove(value);
        }

        /// <summary>记录总数。</summary>
        public int Count => _byId.Count;

        /// <summary>某分组内的记录条数。</summary>
        public int CountIn(string groupId) => groupId != null && _byGroup.TryGetValue(groupId, out var list) ? list.Count : 0;

        /// <summary>组内条目上限（告警/诊断用）。</summary>
        public int MaxRecordsPerGroup => _maxPerGroup;

        /// <summary>按 id 查记录（无 = false）。</summary>
        public bool TryGet(long id, out WorldRecord record) => _byId.TryGetValue(id, out record);

        /// <summary>
        /// 新增一条记录（返回新记录；<b>组内超上限 → 返回 null 并告警</b>）。
        /// 实例记录（<paramref name="instance"/> 非 null）数量必须为 1（不变量 I2）。
        /// </summary>
        public WorldRecord Add(
            ItemDefinition definition, int count, Vector3 position,
            string groupId = DefaultGroup, ItemInstance instance = null)
        {
            Guard.NotNullObject(definition, nameof(definition));
            Guard.True(count > 0, "世界记录数量必须为正。");

            if (instance != null && count != 1)
            {
                throw new ArgumentException("实例记录数量必须为 1（有状态 ⇒ 不可堆叠，Item_Instance_Design.md §2.2）。");
            }

            var group = string.IsNullOrEmpty(groupId) ? DefaultGroup : groupId;
            if (CountIn(group) >= _maxPerGroup)
            {
                // 已处理的业务拒绝（返回 null），不是错误状态 → Warn + 结构化回调（不变量 I8：有界 + 告警）
                _warn?.Invoke($"[WorldRecordStore] 分组 {group} 已达条目上限 {_maxPerGroup}，拒绝新增（{definition.Id} × {count}）——记录表必须有界。");
                Log.Warning($"[WorldRecordStore] 分组 {group} 达到条目上限 {_maxPerGroup}，记录未创建：{definition.Id}。");
                return null;
            }

            var record = new WorldRecord(_ids.Allocate(), definition, count, position, group, instance);
            _byId.Add(record.Id, record);
            if (!_byGroup.TryGetValue(group, out var list))
            {
                list = new List<long>();
                _byGroup.Add(group, list);
            }

            list.Add(record.Id);
            _changed.Invoke(new WorldRecordChangeArgs(WorldRecordChangeKind.Added, record));
            return record;
        }

        /// <summary>删除记录（原子的"先删记录、再回收视图"，不变量 I3）；无该 id = false。</summary>
        public bool TryRemove(long id)
        {
            if (!_byId.TryGetValue(id, out var record))
            {
                return false;
            }

            _byId.Remove(id);
            if (_byGroup.TryGetValue(record.GroupId, out var list))
            {
                list.Remove(id);
            }

            _changed.Invoke(new WorldRecordChangeArgs(WorldRecordChangeKind.Removed, record));
            return true;
        }

        /// <summary>改写数量（实例记录拒绝：身份不可按定义合并/拆分）。</summary>
        public bool TrySetCount(long id, int count)
        {
            if (count <= 0 || !_byId.TryGetValue(id, out var record) || record.IsInstanceRecord)
            {
                return false;
            }

            record.Count = count;
            _changed.Invoke(new WorldRecordChangeArgs(WorldRecordChangeKind.Updated, record));
            return true;
        }

        /// <summary>改写位置（数量不变；视图据此移动）。</summary>
        public bool TrySetPosition(long id, Vector3 position)
        {
            if (!_byId.TryGetValue(id, out var record))
            {
                return false;
            }

            record.Position = position;
            _changed.Invoke(new WorldRecordChangeArgs(WorldRecordChangeKind.Updated, record));
            return true;
        }

        /// <summary>某分组的记录快照（调用方按需遍历；不暴露内部表）。</summary>
        public IReadOnlyList<WorldRecord> RecordsIn(string groupId)
        {
            if (groupId == null || !_byGroup.TryGetValue(groupId, out var list) || list.Count == 0)
            {
                return Array.Empty<WorldRecord>();
            }

            var snapshot = new WorldRecord[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                snapshot[i] = _byId[list[i]];
            }

            return snapshot;
        }
    }
}
