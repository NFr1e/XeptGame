using System;
using UnityEngine;
using UnityEngine.InputSystem;
using XeptGame.Equip;
using XeptGame.Game.Flow;
using XeptGame.Items;

namespace XeptGame.Game
{
    /// <summary>
    /// 装备/占有调试 HUD（EditorDebug，OnGUI 轻实现；MotorDebugHud 先例）：
    /// 显示一轮会话的<b>身体槽占用</b>（手槽等，遍历 <see cref="BodySlotType"/>——加槽自动显示）与
    /// <b>背包行快照</b>（定义 id × 数量），供拾取路由（tap 上手不収包）等运行验证。
    /// <list type="bullet">
    /// <item>挂载：GameplayCore 的 Debug 组；当前样例启用，F9 开关显示；</item>
    /// <item>数据源：GameplaySessionEntry.Instance.Context 的数据与服务（状态轴只读，不写、不发命令）；</item>
    /// <item>每帧直读（调试工具，量小；无状态轨缓存逻辑——天然随拾取/换手/收起实时）；</item>
    /// <item>会话未初始化时显示占位提示（不 disable：模块可能先于会话建立激活，OnGUI 每帧重试）。</item>
    /// </list>
    /// 定位：开发期调试工具，随游戏运行（非 Editor-only）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PossessionDebugHud : MonoBehaviour
    {
        [SerializeField] private bool show = true;

        [SerializeField] private Key toggleKey = Key.F9;

        /// <summary>槽位遍历缓存（BodySlot 枚举一次取值，避免 OnGUI 每帧分配）。</summary>
        private static readonly BodySlotType[] BodySlots = (BodySlotType[])Enum.GetValues(typeof(BodySlotType));

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                show = !show;
            }
        }

        private void OnGUI()
        {
            if (!show)
            {
                return;
            }

            // MotorDebugHud 高 260：装备面板放在其下，避免状态与操作行叠在一起。
            GUILayout.BeginArea(new Rect(10f, 280f, 460f, 380f), GUI.skin.box);
            GUILayout.Label("[Possession Debug]  F9 开关");

            if (!GameplaySessionEntry.TryGetInstance(out var entry))
            {
                GUILayout.Label("一轮会话未初始化（GameplaySessionEntry 未建立）");
                GUILayout.EndArea();
                return;
            }

            var bag = entry.Context.Inventory;
            var body = entry.Context.Equipment;
            var equip = entry.Context.EquipBehaviour.Snapshot;
            GUILayout.Label($"行为：{equip.Phase}  动作：{equip.ActionId}  占用版本：{equip.OccupancyVersion}");
            GUILayout.Label($"进度：{equip.Progress:P0}  暂停：{equip.Paused}");
            var operation = entry.Context.Operations.Current ?? entry.Context.Operations.LastResult;
            if (operation != null)
            {
                GUILayout.Label($"操作：{operation.Id} {operation.Status} {operation.Reason}");
            }

            GUILayout.Label("-- 身体槽（装备） --");
            foreach (var slot in BodySlots)
            {
                var def = body.Get(slot);
                GUILayout.Label(def == null
                    ? $"槽 [{slot}]：空"
                    : $"槽 [{slot}]：{FormatId(def)}（1 单位）");
            }

            var occupiedCells = 0;
            for (int i = 0; i < bag.Slots.Count; i++)
            {
                if (!bag.Slots[i].IsEmpty)
                {
                    occupiedCells++;
                }
            }

            GUILayout.Label($"-- 背包（{bag.Capacity} 格，占用 {occupiedCells} 格） --");
            var stacks = bag.Stacks;
            if (stacks.Count == 0)
            {
                GUILayout.Label("(空)");
            }
            else
            {
                for (int i = 0; i < stacks.Count; i++)
                {
                    GUILayout.Label($"  {FormatId(stacks[i].Definition)} × {stacks[i].Count}");
                }
            }

            GUILayout.EndArea();
        }

        private static string FormatId(ItemDefinition definition)
            => string.IsNullOrEmpty(definition.Id) ? "(无 id)" : definition.Id;
    }
}
