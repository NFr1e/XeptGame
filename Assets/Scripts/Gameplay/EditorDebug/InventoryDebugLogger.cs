using UnityEngine;
using XeptGame.Container;
using XeptGame.Equip;
using XeptGame.Game.Flow;
using XeptGame.Inv;
using XeptKit.Core;

namespace XeptGame.Game
{
    /// <summary>
    /// 调试用背包变更日志（EditorDebug，场景中默认失活；WorldItem_Design.md W7；Item_Instance_Design.md §3 更新）：
    /// 订阅<b>当前背包</b>的 <see cref="Inventory.Changed"/>，把变更行打到日志——
    /// 背包 UI 未建前的运行时验证手段（"拾取进包/合并/移除"可见）；背包 UI 决议落地后退役。
    /// <list type="bullet">
    /// <item>"当前背包"现在<b>可空</b>（背槽为空 = 无包），且随换包变化 → 订阅身体槽事件重新绑定；</item>
    /// <item>事件驱动，不轮询。</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryDebugLogger : MonoBehaviour
    {
        private Inventory _inventory;
        private Equipment _body;

        private void OnEnable()
        {
            if (!GameplaySessionEntry.TryGetInstance(out var entry))
            {
                Log.Error("[InventoryDebugLogger] 一轮会话未初始化（GameplaySessionEntry），无法订阅背包变更。");
                enabled = false;
                return;
            }

            _body = entry.Context.Equipment;
            _body.SlotChanged += OnSlotChanged;
            Rebind(entry.Context.Inventory);
        }

        private void OnDisable()
        {
            if (_body != null)
            {
                _body.SlotChanged -= OnSlotChanged;
                _body = null;
            }

            Rebind(null);
        }

        private void OnSlotChanged(SlotChangeArgs change)
        {
            if (change.Slot != new SlotId((int)BodySlotType.Back))
            {
                return;
            }

            if (GameplaySessionEntry.TryGetInstance(out var entry))
            {
                Rebind(entry.Context.Inventory);
            }
        }

        private void Rebind(Inventory inventory)
        {
            if (ReferenceEquals(_inventory, inventory))
            {
                return;
            }

            if (_inventory != null)
            {
                _inventory.Changed -= OnChanged;
            }

            _inventory = inventory;
            if (_inventory == null)
            {
                Log.Info("[InventoryDebugLogger] 当前无背包（背槽为空）——换包后自动重新绑定。");
                return;
            }

            _inventory.Changed += OnChanged;
            Log.Info($"[InventoryDebugLogger] 已绑定当前背包（{_inventory.Capacity} 格）。");
        }

        private void OnChanged(ContainerChangeArgs args)
            => Log.Info($"[Inventory] {args.Item.Id} × {args.NewCount}（{args.OldCount}→{args.NewCount}）");
    }
}
