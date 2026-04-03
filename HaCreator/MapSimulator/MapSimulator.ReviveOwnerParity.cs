using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly ReviveOwnerRuntime _reviveOwnerRuntime = new();

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
            int currentTick = Environment.TickCount;
            Vector2 spawnPoint = _playerManager?.GetSpawnPoint() ?? new Vector2(player?.DeathX ?? 0f, player?.DeathY ?? 0f);
            Vector2 deathPoint = new(player?.DeathX ?? spawnPoint.X, player?.DeathY ?? spawnPoint.Y);
            bool hasPremiumChoice = ReviveOwnerRuntime.ShouldOfferPremiumChoice(deathPoint, spawnPoint);

            string normalDetail = $"Default branch returns to the simulator respawn seam at ({spawnPoint.X:0}, {spawnPoint.Y:0}).";
            string premiumDetail = hasPremiumChoice
                ? $"Premium branch revives in the current field near the death point at ({deathPoint.X:0}, {deathPoint.Y:0})."
                : string.Empty;

            _reviveOwnerRuntime.Open(
                GetCurrentMapTransferDisplayName(),
                hasPremiumChoice,
                normalDetail,
                premiumDetail,
                hasPremiumChoice ? ReviveOwnerVariant.PremiumChoice : ReviveOwnerVariant.DefaultOnly,
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

                return;
            }

            ReviveOwnerResolution resolution = _reviveOwnerRuntime.Update(currentTick);
            if (resolution.Handled)
            {
                ApplyReviveOwnerResolution(resolution, currentTick);
            }
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

            ApplyReviveOwnerResolution(resolution, currentTick);
            return resolution.Summary;
        }

        private void ApplyReviveOwnerResolution(ReviveOwnerResolution resolution, int currentTick)
        {
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.Revive);

            if (resolution.Premium && _playerManager?.Player != null)
            {
                _playerManager.RespawnAt(_playerManager.Player.DeathX, _playerManager.Player.DeathY);
                return;
            }

            _playerManager?.Respawn();
        }
    }
}
