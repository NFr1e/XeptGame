using System;
using System.Collections.Generic;
using UnityEngine;
using XeptGame.Game.Flow;
using XeptGame.Inv;
using XeptGame.Items;
using XeptGame.World.Interactables;
using XeptKit.Core;

namespace XeptGame.World
{
    /// <summary>
    /// 世界视图生成器（Item_Instance_Design.md §5.2）：订阅记录表，按记录<b>生成/回收</b>场景视图。
    /// <list type="bullet">
    /// <item><b>记录是真相、视图是表现</b>（不变量 I3）：Added 生成、Removed 回收；<b>视图绝不删记录</b>；</item>
    /// <item>prefab 取物品的 <see cref="WorldFacet"/> 配置；<b>未配置则退化为可见占位方块</b> + 告警——
    /// 保证"掉落物看得见、拾得回"，不因内容缺配置而静默失灵（I6 的运行时一面）；</item>
    /// <item>同时承担<b>世界层装配缝</b>：把 <see cref="WorldDropDestination"/> 注入会话（DP6 的"旧包去向"）。</item>
    /// </list>
    /// 前提：随玩法基座场景激活（晚于会话创建）；会话不存在时失活并报错，不轮询。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldViewSpawner : MonoBehaviour
    {
        [Tooltip("世界稳定的视图根（掉落物挂这里，绝不挂到跟随玩家的锚点下）；留空 = 运行期自动建一个场景根")]
        [SerializeField] private Transform viewRoot;

        [Tooltip("掉落落点来源（跟随视角的锚点）；留空 = 本物体位置")]
        [SerializeField] private WorldDropAnchor dropAnchor;

        [Tooltip("记录分组（当前 = 关卡 id）；留空 = 默认组")]
        [SerializeField] private string groupId;

        private readonly Dictionary<long, GameObject> _views = new();
        private WorldRecordStore _store;
        private WorldDropDestination _destination;
        private Transform _runtimeRoot;

        private void OnEnable()
        {
            // 基座场景对象可能**先于**会话创建（Editor 直启路径已实测）：订阅"会话创建铃"，
            // 会话已建则立即装配——两种顺序都成立，且不轮询。
            GameplaySessionEntry.Created += TrySetup;
            TrySetup();
        }

        private void OnDisable()
        {
            GameplaySessionEntry.Created -= TrySetup;

            if (_store != null)
            {
                _store.Changed -= OnRecordChanged;
                _store = null;
            }

            if (GameplaySessionEntry.TryGetInstance(out var entry) && ReferenceEquals(entry.Context.WorldDrop, _destination))
            {
                entry.Context.WorldDrop = null;
            }

            _destination = null;
        }

        /// <summary>装配（幂等）：会话未就绪时安静等待"铃"，就绪后一次性接上记录表与旧包去向。</summary>
        private void TrySetup()
        {
            if (_store != null || !GameplaySessionEntry.TryGetInstance(out var entry))
            {
                return;
            }

            _store = entry.Context.WorldRecords;
            _store.Changed += OnRecordChanged;

            // 旧包去向：落点交给掉落锚（它才知道"脚下/面前"的地面在哪）；没有锚就退回本物体位置
            Func<Vector3> positionProvider = dropAnchor != null
                ? (Func<Vector3>)dropAnchor.ResolveDropPoint
                : () => transform.position;
            _destination = new WorldDropDestination(entry.Context.WorldDropFactory, positionProvider, groupId);
            entry.Context.WorldDrop = _destination;

            // 已在记录表里的（会话先于本模块装配时产生）补生成视图
            var existing = _store.RecordsIn(string.IsNullOrEmpty(groupId) ? WorldRecordStore.DefaultGroup : groupId);
            for (int i = 0; i < existing.Count; i++)
            {
                CreateView(existing[i]);
            }
        }

        private void OnRecordChanged(WorldRecordChangeArgs args)
        {
            if (args.Kind == WorldRecordChangeKind.Added)
            {
                CreateView(args.Record);
            }
            else if (args.Kind == WorldRecordChangeKind.Removed)
            {
                DestroyView(args.Record.Id);
            }
            else
            {
                MoveView(args.Record);
            }
        }

        private void CreateView(WorldRecord record)
        {
            if (record == null || _views.ContainsKey(record.Id))
            {
                return;
            }

            var prefab = record.Definition != null
                ? record.Definition.GetFacet<WorldFacet>()?.profile?.worldPrefab
                : null;

            GameObject view;
            if (prefab != null)
            {
                view = Instantiate(prefab);
            }
            else
            {
                view = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Log.Warning($"[WorldViewSpawner] {record.Definition?.Id} 未配置 WorldFacetProfile.worldPrefab → 用占位方块表现（内容待补）。");
            }

            // 先挂到"世界稳定的视图根"，再写**世界**位置——顺序反了会把记录坐标当成父节点下的局部坐标，
            // 于是掉落物看起来"留在锚点旁边"而不是落在锚点算出的世界坐标上（T5a 的 bug）。
            view.transform.SetParent(ViewRoot, false);
            view.transform.SetPositionAndRotation(record.Position, Quaternion.identity);
            view.name = "WorldView_" + record.Id;
            var item = view.GetComponent<WorldItem>();
            if (item == null)
            {
                item = view.AddComponent<WorldItem>();
            }

            item.Initialize(BuildSource(record, view));
            _views.Add(record.Id, view);
        }

        /// <summary>按记录类型造数据面：实例记录（背包）走实例源，堆叠记录走堆叠源。</summary>
        private static IWorldSource BuildSource(WorldRecord record, GameObject view)
        {
            IWorldSource source = null;
            Func<bool> available = () => view != null && view.activeInHierarchy;
            Action refresh = () =>
            {
                if (view != null && source != null && !source.HasContent)
                {
                    view.SetActive(false);
                }
            };

            source = record.Instance is ContainerInstance carrier
                ? new WorldInstanceSource(carrier, record.Id, available, refresh)
                : new WorldStackSource(record.Definition, record.Count, available, refresh, record.Id);
            return source;
        }

        private void MoveView(WorldRecord record)
        {
            if (record != null && _views.TryGetValue(record.Id, out var view) && view != null)
            {
                view.transform.position = record.Position;
            }
        }

        /// <summary>
        /// 视图根：世界稳定，掉落物<b>绝不</b>挂到跟随玩家的锚点下（否则玩家一动，地上的东西跟着飞）。
        /// 未显式配置时运行期自建一个场景根容器。
        /// </summary>
        private Transform ViewRoot
        {
            get
            {
                if (viewRoot != null)
                {
                    return viewRoot;
                }

                if (_runtimeRoot == null)
                {
                    var go = new GameObject("[WorldViews]");
                    _runtimeRoot = go.transform;
                }

                return _runtimeRoot;
            }
        }

        private void DestroyView(long recordId)
        {
            if (!_views.TryGetValue(recordId, out var view))
            {
                return;
            }

            _views.Remove(recordId);
            if (view != null)
            {
                Destroy(view);
            }
        }
    }
}
