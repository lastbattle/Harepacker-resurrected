using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly ReviveOwnerRuntime _reviveOwnerRuntime = new();
        private ReviveOwnerTransferRequest? _pendingReviveOwnerTransferRequest;
        private int _pendingReviveOwnerTransferTick = int.MinValue;

        private void WireReviveConfirmationWindow()
        {
            if (_playerManager?.Player != null)
            {
                Action<PlayerCharacter> deathHandler = _playerManager.Player.OnDeath;
                if (deathHandler == null || Array.IndexOf(deathHandler.GetInvocationList(), (Action<PlayerCharacter>)HandlePlayerDeathOpenReviveOwner) < 0)
                {
                    _playerManager.Player.OnDeath = deathHandler + HandlePlayerDeathOpenReviveOwner;
                }
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Revive) is not ReviveConfirmationWindow reviveWindow)
            {
                return;
            }

            reviveWindow.SetFont(_fontChat);
            reviveWindow.SetSnapshotProvider(() => _reviveOwnerRuntime.BuildSnapshot(Environment.TickCount));
            reviveWindow.SetActionHandlers(
                () => ResolveReviveOwnerChoice(premium: true, Environment.TickCount),
                () => ResolveReviveOwnerChoice(premium: false, Environment.TickCount),
                ShowUtilityFeedbackMessage);

            if (!_reviveOwnerRuntime.IsOpen)
            {
                reviveWindow.Hide();
            }
        }

        private void HandlePlayerDeathOpenReviveOwner(PlayerCharacter player)
        {
            if (player == null || _reviveOwnerRuntime.IsOpen)
            {
                return;
            }

            int currentTick = Environment.TickCount;
            Vector2 spawnPoint = _playerManager?.GetSpawnPoint() ?? new Vector2(player?.DeathX ?? 0f, player?.DeathY ?? 0f);
            Vector2 deathPoint = new(player?.DeathX ?? spawnPoint.X, player?.DeathY ?? spawnPoint.Y);
            ReviveOwnerVariant variant = ResolveReviveOwnerVariant();
            bool hasPremiumChoice = ReviveOwnerRuntime.HasPremiumChoiceForVariant(variant);
            string ownerLabel = ReviveOwnerRuntime.GetOwnerLabel(variant);

            string normalDetail = $"Default branch returns to the simulator respawn seam at ({spawnPoint.X:0}, {spawnPoint.Y:0}).";
            string premiumDetail = hasPremiumChoice
                ? BuildPremiumReviveDetail(variant, ownerLabel, deathPoint)
                : string.Empty;

            _reviveOwnerRuntime.Open(
                GetCurrentMapTransferDisplayName(),
                normalDetail,
                premiumDetail,
                variant,
                currentTick);

            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.Revive);
        }

        private void UpdateReviveOwnerState(int currentTick)
        {
            if (_playerManager?.Player?.IsAlive != false)
            {
                if (_reviveOwnerRuntime.IsOpen)
                {
                    _reviveOwnerRuntime.Close();
                    uiWindowManager?.HideWindow(MapSimulatorWindowNames.Revive);
                }

                _pendingReviveOwnerTransferRequest = null;
                _pendingReviveOwnerTransferTick = int.MinValue;
                return;
            }

            ReviveOwnerResolution resolution = _reviveOwnerRuntime.Update(currentTick);
            if (resolution.Handled)
            {
                QueueReviveOwnerTransfer(resolution, currentTick);
                ShowUtilityFeedbackMessage(resolution.Summary);
            }

            ApplyPendingReviveOwnerTransfer(currentTick);
        }

        private bool TryHandleReviveShortcut(KeyboardState keyboardState)
        {
            if (_playerManager?.Player?.IsAlive != false)
            {
                return false;
            }

            if (!_reviveOwnerRuntime.IsOpen)
            {
                HandlePlayerDeathOpenReviveOwner(_playerManager.Player);
                return true;
            }

            if (_pendingReviveOwnerTransferRequest.HasValue)
            {
                return true;
            }

            bool premiumRequested = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            string message = ResolveReviveOwnerChoice(premiumRequested, Environment.TickCount);
            if (!string.IsNullOrWhiteSpace(message))
            {
                ShowUtilityFeedbackMessage(message);
            }

            return true;
        }

        private string ResolveReviveOwnerChoice(bool premium, int currentTick)
        {
            ReviveOwnerResolution resolution = _reviveOwnerRuntime.Resolve(premium);
            if (!resolution.Handled)
            {
                return "Revive owner is not active.";
            }

            QueueReviveOwnerTransfer(resolution, currentTick);
            return resolution.Summary;
        }

        private void QueueReviveOwnerTransfer(ReviveOwnerResolution resolution, int currentTick)
        {
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.Revive);
            _pendingReviveOwnerTransferRequest = ReviveOwnerRuntime.CreateTransferRequest(resolution);
            _pendingReviveOwnerTransferTick = currentTick;
        }

        private void ApplyPendingReviveOwnerTransfer(int currentTick)
        {
            if (!_pendingReviveOwnerTransferRequest.HasValue
                || unchecked(currentTick - _pendingReviveOwnerTransferTick) <= 0)
            {
                return;
            }

            ReviveOwnerTransferRequest request = _pendingReviveOwnerTransferRequest.Value;
            _pendingReviveOwnerTransferRequest = null;
            _pendingReviveOwnerTransferTick = int.MinValue;

            if (request.Premium && _playerManager?.Player != null)
            {
                if (!TryConsumeReviveOwnerPremiumItem(request))
                {
                    _playerManager?.Respawn();
                    return;
                }

                _playerManager.RespawnAt(_playerManager.Player.DeathX, _playerManager.Player.DeathY);
                return;
            }

            _playerManager?.Respawn();
        }

        private ReviveOwnerVariant ResolveReviveOwnerVariant()
        {
            int premiumSafetyCharmCount = GetInventoryWindowItemCount(5131000);
            int safetyCharmCount = GetInventoryWindowItemCount(5130000);
            int wheelOfFortuneCount = GetInventoryWindowItemCount(5510000);

            // Client evidence:
            // - CUIRevive::OnCreate checks soul-stone state first.
            // - It then gates the upgrade-tomb branch on a field helper plus Wheel of Fortune ownership.
            // - It falls through to the premium/default revive-owner branch otherwise.
            // The simulator can currently back only the inventory-owned item cases directly.
            return ReviveOwnerRuntime.ResolveClientVariant(
                hasSoulStone: false,
                hasUpgradeTombChoice: wheelOfFortuneCount > 0,
                hasPremiumSafetyCharm: premiumSafetyCharmCount > 0,
                hasSafetyCharm: safetyCharmCount > 0);
        }

        private static string BuildPremiumReviveDetail(ReviveOwnerVariant variant, string ownerLabel, Vector2 deathPoint)
        {
            int consumableItemId = ReviveOwnerRuntime.GetConsumableCashItemId(variant);
            string itemText = consumableItemId > 0
                ? $"{ownerLabel} ({consumableItemId})"
                : ownerLabel;

            return $"{itemText} revives in the current field near the death point at ({deathPoint.X:0}, {deathPoint.Y:0}).";
        }

        private bool TryConsumeReviveOwnerPremiumItem(ReviveOwnerTransferRequest request)
        {
            int cashItemId = ReviveOwnerRuntime.GetConsumableCashItemId(request.Variant);
            if (cashItemId <= 0)
            {
                return true;
            }

            if (uiWindowManager?.InventoryWindow is not IInventoryRuntime inventoryWindow)
            {
                return true;
            }

            if (inventoryWindow.TryConsumeItem(InventoryType.CASH, cashItemId, 1))
            {
                return true;
            }

            ShowUtilityFeedbackMessage($"{ReviveOwnerRuntime.GetOwnerLabel(request.Variant)} was no longer available, so the revive owner fell back to the default branch.");
            return false;
        }
    }
}
