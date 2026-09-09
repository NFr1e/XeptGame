using System;
using UnityEngine;
using XeptGame.Core.Input;
using XeptGame.Game.Flow;

namespace XeptGame.Items.Operations
{
    /// <summary>玩家自身收回意图；背包去向只在操作层指定，不进入装备行为。</summary>
    public sealed class EquipInputBridge : MonoBehaviour
    {
        private IDisposable _binding;
        private void OnEnable()
        {
            TryBind();
        }

        private void Update()
        {
            TryBind();
        }

        private void TryBind()
        {
            if (_binding != null || AppEntry.GlobalInput == null || AppEntry.InputManager == null)
            {
                return;
            }

            _binding = AppEntry.InputManager.Bind<GameplayInputLayer>(AppEntry.GlobalInput.Gameplay.PutAway, ctx =>
            {
                if (ctx.performed && GameplaySessionEntry.TryGetInstance(out var entry) && entry.IsPlaying)
                {
                    entry.Context.Operations.RequestUnequip(entry.Context.Inventory);
                }
            });
        }

        private void OnDisable()
        {
            _binding?.Dispose();
            _binding = null;
        }
    }
}
