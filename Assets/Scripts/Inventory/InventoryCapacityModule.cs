using UnityEngine;
using XeptGame.Core;
using XeptGame.Game.Flow;
using XeptKit.Core;

namespace XeptGame.Inv
{
    /// <summary>
    /// 背包容量装配模块（挂 GameplayCore 基座场景）：把序列化的 <see cref="InventoryProfile"/> 注入会话背包。
    /// <list type="bullet">
    /// <item><b>OnEnable 自举</b>：基座场景由 <c>GameplayLoadState.EnterAsync</c> 按 key 加载，**晚于**会话创建
    /// （<c>InitAsync</c>）→ 本模块激活时会话必然已存在，直接注入即可（与 <c>InteractPromptModule</c>、
    /// <c>InventoryDebugLogger</c> 同形；不轮询）；</item>
    /// <item><b>Editor 直接 Play 的入口约定</b>：只能从**带 <c>LevelBoot</c> 的关卡场景**发起
    /// （LevelBoot 发开始游戏请求 → 会话建立 → 基座组加载），**基座场景不会先于会话存在**；</item>
    /// <item><b>纯装配、零业务</b>：配置留空 = 用常量默认，并记一条 Info（装配缺失可见，不静默）；
    /// 会话未就绪属装配时序异常 → Log.Error（不进业务降级）；</item>
    /// <item><b>注入幂等</b>：<c>Inventory.ApplyProfile</c> 同值不动作。</item>
    /// </list>
    /// 后续扩容者接线（订阅 <c>Equipment.SlotChanged</c> → <c>SetCapacitySource(槽位, 加成)</c>、
    /// 并在 OnDisable 退订）也落在这里。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryCapacityModule : MonoBehaviour
    {
        [Tooltip("背包侧配置（基础格数）；留空 = 用 XeptGameConsts.Inventory.DefaultCapacity")]
        [SerializeField] private InventoryProfile profile;

        private void OnEnable()
        {
            if (!GameplaySessionEntry.TryGetInstance(out var entry))
            {
                Log.Error("[InventoryCapacityModule] 一轮会话未初始化（GameplaySessionEntry）——装配时序异常：本模块应随基座场景激活，晚于会话创建。");
                return;
            }

            if (profile == null)
            {
                Log.Info($"[InventoryCapacityModule] 未配置 InventoryProfile → 使用常量默认格数（{XeptGameConsts.Inventory.DefaultCapacity}）。");
            }

            entry.Context.Inventory.ApplyProfile(profile);
        }
    }
}
