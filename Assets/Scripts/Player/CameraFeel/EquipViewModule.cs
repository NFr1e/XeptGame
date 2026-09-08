using UnityEngine;
using XeptGame.Equip;
using XeptGame.Game.Flow;
using XeptGame.Items;
using XeptKit.Core;

namespace XeptGame.Player
{
    /// <summary>
    /// FPV 持物呈现模块（呈现轴；Equip_FPV_Design.md §2.5，T6）：
    /// 订阅身体容器手槽占用变化（SlotChanged）→ 装配 <c>HoldableFacet → HoldProfile.viewPrefab</c> 到 ViewRoot。
    /// <list type="bullet">
    /// <item><b>零业务规则</b>：只做"状态 → 视觉"翻译（状态轨消费者），不读写容器、不发命令；</item>
    /// <item><b>不写相机</b>：ViewRoot 为 FirstPersonCamera（Overlay）子级，随 CameraRig 合成结果自动携带——
    /// 模块不引用/不写任何相机 Transform（单写者不扩散 R1）；</item>
    /// <item><b>生命周期</b>：场景模块自生命周期（OnEnable 订阅 + 同步当前；OnDisable 退订清视图，
    /// InteractPromptModule 先例）；会话未初始化（TryGetInstance 失败）时静默跳过，不阻塞流程；</item>
    /// <item><b>防呆可选</b>：<see cref="applyLayerGuard"/> 开启时实例化后强制置 FPV 层（防漏配穿墙可见；
    /// 低优先，现状单相机不易错漏）。</item>
    /// </list>
    /// </summary>
    public sealed class EquipViewModule : MonoBehaviour
    {
        [Tooltip("手里模型挂点（ViewRoot：FirstPersonCamera(Overlay) 的子级空节点；随相机携带，模块不写相机）")]
        [SerializeField] private Transform viewRoot;

        [Tooltip("防呆：viewPrefab 实例化后强制设置的层索引（需与 FPV Overlay 相机掩码一致，如 12）")]
        [SerializeField] private int fpvLayer = 12;

        [Tooltip("是否启用层防呆（默认关——现状单相机不易错漏；开 = 防漏配穿墙可见）")]
        [SerializeField] private bool applyLayerGuard;

        private Equipment _body;
        private GameObject _view;

        private void OnEnable()
        {
            if (!GameplayEntry.TryGetInstance(out var entry))
            {
                Log.Warning("[EquipViewModule] 一轮会话未初始化（GameplayEntry），持物呈现不可用（场景时序？）。");
                return;
            }

            _body = entry.Session.Equipment;
            _body.SlotChanged += OnSlotChanged;
            ApplyCurrent(); // 迟到订阅：同步当前占用，防错过会话建立前的变化
        }

        private void OnDisable()
        {
            if (_body != null)
            {
                _body.SlotChanged -= OnSlotChanged;
                _body = null;
            }

            DestroyView();
        }

        private void OnSlotChanged(SlotChangeArgs args)
        {
            if (args.Slot == BodySlotType.Hand)
            {
                Apply(args.New);
            }
        }

        private void ApplyCurrent() => Apply(_body != null ? _body.Get(BodySlotType.Hand) : null);

        private void Apply(ItemDefinition definition)
        {
            DestroyView();

            if (definition == null)
            {
                return; // 空手
            }

            if (viewRoot == null)
            {
                Log.Warning($"[EquipViewModule] viewRoot 未接线（应指向 FirstPersonCamera 下的持物挂点）——{definition.Id} 不呈现。");
                return;
            }

            var viewPrefab = definition.TryGetFacet(out HoldableFacet holdable) && holdable.profile != null
                ? holdable.profile.viewPrefab
                : null;
            if (viewPrefab == null)
            {
                Log.Warning($"[EquipViewModule] {definition.Id} 无 viewPrefab（HoldableFacet.profile 未接视图模型）——暂以空手呈现。");
                return;
            }

            _view = Instantiate(viewPrefab, viewRoot);
            _view.transform.localPosition = Vector3.zero;
            _view.transform.localRotation = Quaternion.identity;
            _view.transform.localScale = Vector3.one;

            if (applyLayerGuard && fpvLayer >= 0)
            {
                SetLayerRecursively(_view.transform, fpvLayer);
            }
        }

        private void DestroyView()
        {
            if (_view != null)
            {
                Destroy(_view.gameObject);
                _view = null;
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }
    }
}
