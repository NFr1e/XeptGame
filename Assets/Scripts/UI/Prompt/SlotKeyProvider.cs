using UnityEngine.InputSystem;
using XeptGame.Core.Input;
using XeptGame.Interaction;

namespace XeptGame.UI
{
    /// <summary>
    /// 语义槽 → 键名显示（真源 = 实际输入绑定）：从对应 InputAction 取首个绑定的 ToDisplayString——
    /// 改键后提示自动跟随（与"槽不持物理键、键绑定集中输入层"的语义一致）。
    /// </summary>
    public static class SlotKeyProvider
    {
        public static string ToDisplayText(InputSlot slot)
        {
            if (AppEntry.GlobalInput == null)
            {
                return string.Empty;
            }

            InputAction action = slot switch
            {
                InputSlot.Primary => AppEntry.GlobalInput.Gameplay.Interact,
                InputSlot.Secondary => AppEntry.GlobalInput.Gameplay.InteractSecondary,
                _ => null,
            };

            if (action == null || action.bindings.Count == 0)
            {
                // Hold 等未绑定的槽给可读占位
                return slot == InputSlot.Hold ? "按住" : string.Empty;
            }

            return action.bindings[0].ToDisplayString();
        }
    }
}
