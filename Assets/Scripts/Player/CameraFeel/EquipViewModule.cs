using UnityEngine;
using XeptGame.Equip;
using XeptGame.Game.Flow;
using XeptGame.Items;
using XeptKit.Core;

namespace XeptGame.Player
{
    /// <summary>
    /// 第一人称持物呈现（EB-05）：消费装备行为快照，绑定模型并合成拿出/收回姿态。
    /// 只写自己创建的过渡挂点，不写相机或物品容器；动作打断从当前姿态继续。
    /// 会话晚于组件初始化时逐帧尝试绑定，重新启用时按完整快照恢复，不重播获得事件。
    /// </summary>
    public sealed class EquipViewModule : MonoBehaviour
    {
        [Tooltip("FirstPersonCamera 下的持物基准挂点")]
        [SerializeField] private Transform viewRoot;

        [Tooltip("持物视图专用层，须与 Overlay 相机渲染掩码一致")]
        [SerializeField] private int fpvLayer = 12;
        [SerializeField] private bool applyLayerGuard = true;

        [Tooltip("相对 ViewRoot 的就绪位置")]
        [SerializeField] private Vector3 heldPosition = new(0.25f, -0.25f, 0.55f);

        [Tooltip("收回位置相对就绪位置的偏移")]
        [SerializeField] private Vector3 stowedOffset = new(0, -0.65f, 0);
        [SerializeField] private Vector3 stowedEuler = new(45, 0, 15);

        private EquipController _behavior;
        private EquipSnapshot _snapshot;
        private Transform _transition;
        private GameObject _view;
        private long _version = -1;
        private long _action = -1;
        private Vector3 _fromPosition;
        private Quaternion _fromRotation;

        /// <summary>当前只用于显示的模型实例，便于运行检查；不代表另一个物品单位。</summary>
        public GameObject CurrentView => _view;

        /// <summary>本模块独占写入的局部过渡节点。</summary>
        public Transform TransitionRoot => _transition;

        private void OnEnable()
        {
            BindIfReady();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void BindIfReady()
        {
            var next = GameplaySessionEntry.TryGetInstance(out var entry) ? entry.Context?.EquipBehaviour : null;
            if (ReferenceEquals(next, _behavior))
            {
                return;
            }

            Unbind();
            if (next == null || viewRoot == null)
            {
                return;
            }

            _behavior = next;
            _transition = new GameObject("EquipTransition").transform;
            _transition.SetParent(viewRoot, false);
            _transition.localPosition = heldPosition + stowedOffset;
            _transition.localRotation = Quaternion.Euler(stowedEuler);
            _behavior.SnapshotChanged += Receive;
            Receive(_behavior.Snapshot);
        }

        private void Receive(EquipSnapshot snapshot)
        {
            if (_transition == null)
            {
                return;
            }

            if (_version != snapshot.OccupancyVersion)
            {
                if (_view != null)
                {
                    _view.SetActive(false);
                    Destroy(_view);
                }

                _view = null;
                _version = snapshot.OccupancyVersion;
                _transition.localPosition = heldPosition + stowedOffset;
                _transition.localRotation = Quaternion.Euler(stowedEuler);
                if (snapshot.Item != null)
                {
                    var prefab = snapshot.Item.TryGetFacet(out HoldableFacet hold) ? hold.profile?.viewPrefab : null;
                    if (prefab != null)
                    {
                        _view = Instantiate(prefab, _transition);
                        _view.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                        if (applyLayerGuard)
                        {
                            SetLayer(_view.transform);
                        }
                    }
                    else
                    {
                        Log.Warning($"[EquipView] {snapshot.Item.Id} 缺少持物 Prefab。");
                    }
                }

                _action = -1;
            }

            if (_action != snapshot.ActionId)
            {
                _action = snapshot.ActionId;
                _fromPosition = _transition.localPosition;
                _fromRotation = _transition.localRotation;
            }

            _snapshot = snapshot;
            ApplyPose();
        }

        private void LateUpdate()
        {
            BindIfReady();
            ApplyPose();
        }

        private void ApplyPose()
        {
            if (_transition == null)
            {
                return;
            }

            var visible = _snapshot.Phase != EquipPhase.Empty && _snapshot.Phase != EquipPhase.Stowed;
            if (_view != null)
            {
                _view.SetActive(visible);
            }

            bool raised = _snapshot.Phase == EquipPhase.Ready || _snapshot.Phase == EquipPhase.Drawing;
            var position = raised ? heldPosition : heldPosition + stowedOffset;
            var rotation = raised ? Quaternion.identity : Quaternion.Euler(stowedEuler);
            var moving = _snapshot.Phase == EquipPhase.Drawing || _snapshot.Phase == EquipPhase.Stowing;
            var t = moving ? Mathf.SmoothStep(0, 1, _snapshot.Progress) : 1;
            _transition.SetLocalPositionAndRotation(Vector3.Lerp(_fromPosition, position, t), Quaternion.Slerp(_fromRotation, rotation, t));
        }

        private void SetLayer(Transform node)
        {
            node.gameObject.layer = Mathf.Clamp(fpvLayer, 0, 31);
            foreach (Transform child in node)
            {
                SetLayer(child);
            }
        }

        private void Unbind()
        {
            if (_behavior != null)
            {
                _behavior.SnapshotChanged -= Receive;
            }

            _behavior = null;
            if (_transition != null)
            {
                _transition.gameObject.SetActive(false);
                Destroy(_transition.gameObject);
            }

            _transition = null;
            _view = null;
            _version = -1;
            _action = -1;
        }
    }
}
