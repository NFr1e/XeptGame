using UnityEngine;
using XeptGame.Game.Flow;
using XeptGame.Inv;
using XeptKit.Core;

namespace XeptGame.Game
{
    /// <summary>
    /// 调试用背包变更日志（EditorDebug，场景中默认失活；WorldItem_Design.md W7）：
    /// 订阅一轮会话背包的 <see cref="Inventory.Changed"/>，把变更行打到日志——
    /// 背包 UI 未建前的运行时验证手段（"拾取进包/合并/移除"可见）；背包 UI 决议落地后退役。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryDebugLogger : MonoBehaviour
    {
        private Inventory _inventory;

        private void OnEnable()
        {
            if (GameplayEntry.TryGetInstance(out var entry))
            {
                _inventory = entry.Session.Inventory;
                _inventory.Changed += OnChanged;
            }
            else
            {
                Log.Error("[InventoryDebugLogger] 一轮会话未初始化（GameplayEntry），无法订阅背包变更。");
                enabled = false;
            }
        }

        private void OnDisable()
        {
            if (_inventory != null)
            {
                _inventory.Changed -= OnChanged;
                _inventory = null;
            }
        }

        private void OnChanged(InventoryChangeArgs args)
            => Log.Info($"[Inventory] {args.Item.Id} × {args.NewCount}（{args.OldCount}→{args.NewCount}）");
    }
}
