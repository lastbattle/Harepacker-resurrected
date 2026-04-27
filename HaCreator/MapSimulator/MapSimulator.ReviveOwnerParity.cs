using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BinaryWriter = MapleLib.PacketLib.PacketWriter;
namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int ReviveOwnerSoulStoneSkillId = 22181003;
        private const int ReviveOwnerTransferFieldRequestOpcode = 41;
        private const int ReviveOwnerUpgradeTombEffectOpcode = 58;
        private const int ReviveOwnerUpgradeTombItemId = 5510000;
        private const int ReviveOwnerPremiumSafetyCharmContextSlot = 2073;
        private const byte ReviveOwnerSyntheticFieldKey = 0;
        private static readonly string[][] ReviveOwnerMapInfoPropertyAliases =
        {
            new[] { "noResurection", "noResurrection" },
            new[] { "reviveCurField" },
            new[] { "ReviveCurFieldOfNoTransfer" },
            new[] { "ReviveCurFieldOfNoTransferPoint" },
            new[] { "forceReturnOnDead" },
        };

        private readonly ReviveOwnerRuntime _reviveOwnerRuntime = new();
        private ReviveOwnerPendingOpen? _pendingReviveOwnerOpen;
        private ReviveOwnerTransferRequest? _pendingReviveOwnerTransferRequest;
        private int _pendingReviveOwnerTransferTick = int.MinValue;
        private bool _packetOwnedRevivePremiumSafetyCharmLastObservedOfficialSessionConnected;

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
                ShowUtilityFeedbackMessage,
                () => ResolveReviveOwnerClientButtonClick(ReviveOwnerRuntime.ClientYesButtonId, Environment.TickCount));

            if (!_reviveOwnerRuntime.IsOpen)
            {
                reviveWindow.Hide();
            }
        }

        private void HandlePlayerDeathOpenReviveOwner(PlayerCharacter player)
        {
            if (player == null || _reviveOwnerRuntime.IsOpen || _pendingReviveOwnerOpen.HasValue)
            {
                return;
            }

            int currentTick = Environment.TickCount;
            SyncRevivePremiumSafetyCharmOfficialSessionLifecycle(currentTick);
            Vector2 spawnPoint = _playerManager?.GetSpawnPoint() ?? new Vector2(player?.DeathX ?? 0f, player?.DeathY ?? 0f);
            Vector2 deathPoint = new(player?.DeathX ?? spawnPoint.X, player?.DeathY ?? spawnPoint.Y);
            EnsureRevivePremiumSafetyCharmContextInitializedFromInventory("revive-owner-open-sync", currentTick);
            ReviveOwnerVariant variant = ResolveReviveOwnerVariant();
            bool hasPremiumChoice = ReviveOwnerRuntime.HasPremiumChoiceForVariant(variant);
            string ownerLabel = ReviveOwnerRuntime.GetOwnerLabel(variant);
            ReviveOwnerRespawnPointResolution premiumRespawnResolution = ResolveCurrentFieldReviveRespawnPointResolution(variant, deathPoint);
            string normalDetail = BuildDefaultReviveDetail(variant, ownerLabel, spawnPoint);
            string premiumDetail = hasPremiumChoice
                ? BuildPremiumReviveDetail(variant, ownerLabel, premiumRespawnResolution)
                : string.Empty;

            _pendingReviveOwnerOpen = new ReviveOwnerPendingOpen(
                GetCurrentMapTransferDisplayName(),
                normalDetail,
                premiumDetail,
                variant,
                premiumRespawnResolution.Point,
                currentTick);
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.Revive);
        }

        private void UpdateReviveOwnerState(int currentTick)
        {
            SyncRevivePremiumSafetyCharmOfficialSessionLifecycle(currentTick);
            if (_playerManager?.Player?.IsAlive != false)
            {
                if (_reviveOwnerRuntime.IsOpen)
                {
                    _reviveOwnerRuntime.Close();
                    uiWindowManager?.HideWindow(MapSimulatorWindowNames.Revive);
                }

                _pendingReviveOwnerOpen = null;
                _pendingReviveOwnerTransferRequest = null;
                _pendingReviveOwnerTransferTick = int.MinValue;
                return;
            }

            TryOpenPendingReviveOwner(currentTick);

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
                if (_pendingReviveOwnerOpen.HasValue)
                {
                    TryOpenPendingReviveOwner(Environment.TickCount);
                    return true;
                }

                HandlePlayerDeathOpenReviveOwner(_playerManager.Player);
                return true;
            }

            if (_pendingReviveOwnerTransferRequest.HasValue)
            {
                return true;
            }

            bool premiumRequested = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            int clientButtonId = premiumRequested || !_reviveOwnerRuntime.HasPremiumChoice
                ? ReviveOwnerRuntime.ClientYesButtonId
                : ReviveOwnerRuntime.ClientNoButtonId;
            string message = ResolveReviveOwnerClientButtonClick(clientButtonId, Environment.TickCount);
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

        private string ResolveReviveOwnerClientButtonClick(int buttonId, int currentTick)
        {
            ReviveOwnerResolution resolution = _reviveOwnerRuntime.ResolveClientButtonClick(buttonId);
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
            _pendingReviveOwnerOpen = null;
            _pendingReviveOwnerTransferRequest = ReviveOwnerRuntime.CreateTransferRequest(resolution);
            _pendingReviveOwnerTransferTick = currentTick;
        }

        private bool TryOpenPendingReviveOwner(int currentTick)
        {
            if (!_pendingReviveOwnerOpen.HasValue)
            {
                return false;
            }

            ReviveOwnerPendingOpen pendingOpen = _pendingReviveOwnerOpen.Value;
            if (!ReviveOwnerRuntime.ShouldCreateDialog(pendingOpen.ArmedAtTick, currentTick))
            {
                return false;
            }

            _pendingReviveOwnerOpen = null;
            if (pendingOpen.Variant == ReviveOwnerVariant.UpgradeTombChoice)
            {
                Debug.WriteLine(DispatchReviveOwnerUpgradeTombEffectRequest(pendingOpen.PremiumRespawnPoint, currentTick));
            }

            _reviveOwnerRuntime.Open(
                pendingOpen.MapName,
                pendingOpen.NormalDetail,
                pendingOpen.PremiumDetail,
                pendingOpen.Variant,
                currentTick);

            ApplyReviveOwnerWindowPlacement();
            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.Revive);
            return true;
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

            if (_playerManager?.Player != null
                && ReviveOwnerRuntime.ShouldConsumeCashItemForLocalResolution(request))
            {
                if (!TryConsumeReviveOwnerPremiumItem(request))
                {
                    if (request.Variant == ReviveOwnerVariant.PremiumSafetyCharmChoice)
                    {
                        SetRevivePremiumSafetyCharmContextFromInventory("revive-owner-consume-failed-fallback", currentTick);
                    }

                    Debug.WriteLine(DispatchReviveOwnerTransferFieldRequest(
                        new ReviveOwnerTransferRequest(
                            premium: false,
                            timedOut: request.TimedOut,
                            request.Variant,
                            request.Summary,
                            clientPremiumFlag: false)));
                    _playerManager?.Respawn();
                    return;
                }

                if (request.Variant == ReviveOwnerVariant.PremiumSafetyCharmChoice)
                {
                    SetRevivePremiumSafetyCharmContextFromInventory("revive-owner-consume-success", currentTick);
                }
            }

            if (request.Premium && _playerManager?.Player != null)
            {
                if (request.Variant == ReviveOwnerVariant.SoulStoneChoice)
                {
                    _playerManager?.Skills?.CancelActiveBuff(ReviveOwnerSoulStoneSkillId);
                }

                Debug.WriteLine(DispatchReviveOwnerTransferFieldRequest(request));
                Vector2 deathPoint = new(_playerManager.Player.DeathX, _playerManager.Player.DeathY);
                Vector2 respawnPoint = ResolveCurrentFieldReviveRespawnPoint(request.Variant, deathPoint);
                _playerManager.RespawnAt(respawnPoint.X, respawnPoint.Y);
                return;
            }

            Debug.WriteLine(DispatchReviveOwnerTransferFieldRequest(request));
            _playerManager?.Respawn();
        }

        private ReviveOwnerVariant ResolveReviveOwnerVariant()
        {
            bool hasSoulStone = _playerManager?.Skills?.HasBuff(ReviveOwnerSoulStoneSkillId) == true;
            int safetyCharmCount = GetInventoryWindowItemCount(5130000);
            int wheelOfFortuneCount = GetInventoryWindowItemCount(5510000);
            bool fieldAllowsCurrentFieldRecovery = IsPremiumCurrentFieldReviveUsable(_mapBoard?.MapInfo);
            bool premiumSafetyCharmContextArmed = ResolveRevivePremiumSafetyCharmContextArmed(
                fallbackArmed: GetInventoryWindowItemCount(5131000) > 0);
            bool canUsePremiumCurrentFieldRecovery = IsPremiumCurrentFieldReviveUsable(
                fieldAllowsCurrentFieldRecovery,
                premiumSafetyCharmContextArmed);
            bool canUseUpgradeTombRevive = IsUpgradeTombReviveUsable();

            return ResolveReviveOwnerVariant(
                hasSoulStone,
                premiumSafetyCharmContextArmed,
                safetyCharmCount,
                wheelOfFortuneCount,
                canUsePremiumCurrentFieldRecovery,
                canUseUpgradeTombRevive);
        }

        internal static ReviveOwnerVariant ResolveReviveOwnerVariant(
            bool hasSoulStone,
            bool premiumSafetyCharmContextArmed,
            int safetyCharmCount,
            int wheelOfFortuneCount,
            bool canUsePremiumCurrentFieldRecovery,
            bool canUseUpgradeTombRevive)
        {
            // Client evidence:
            // - CUIRevive::OnCreate checks soul-stone state first.
            // - It then gates the upgrade-tomb branch on is_fieldtype_upgradetomb_usable plus Wheel of Fortune ownership.
            // - It falls through to the premium/default revive-owner branch otherwise, with an
            //   extra safety-charm gate driven by CWvsContext state and an adjacent field-owned flag.
            // WZ evidence:
            // - Map info can carry revive-current-field markers such as reviveCurField and
            //   forceReturnOnDead even though the current extracted dataset rarely surfaces them.
            // The simulator can back the Soul Stone branch from the active buff runtime and the
            // other branches from the live inventory seam plus the closest available field rule.
            return ReviveOwnerRuntime.ResolveClientVariant(
                hasSoulStone,
                hasUpgradeTombChoice: wheelOfFortuneCount > 0 && canUseUpgradeTombRevive,
                hasPremiumSafetyCharm: canUsePremiumCurrentFieldRecovery && premiumSafetyCharmContextArmed,
                hasSafetyCharm: safetyCharmCount > 0);
        }

        internal static bool IsPremiumCurrentFieldReviveUsable(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return true;
            }

            if (TryGetReviveOwnerMapInfoFlag(mapInfo, "noResurection", out bool noResurrection) && noResurrection)
            {
                return false;
            }

            if (TryGetReviveOwnerMapInfoFlag(mapInfo, "reviveCurField", out bool reviveCurField))
            {
                return reviveCurField;
            }

            if (TryGetReviveOwnerMapInfoFlag(mapInfo, "ReviveCurFieldOfNoTransfer", out bool reviveCurFieldOfNoTransfer))
            {
                return reviveCurFieldOfNoTransfer;
            }

            return ShouldUseCurrentFieldReviveSpawnApproximation(mapInfo);
        }

        internal static bool IsPremiumCurrentFieldReviveUsable(
            bool fieldAllowsCurrentFieldRecovery,
            bool premiumSafetyCharmContextArmed)
        {
            // Client evidence from CUIRevive::OnCreate:
            // - The premium safety-charm branch uses a dual gate:
            //   CWvsContext slot 2073 must be armed and the adjacent field-owned
            //   no-transfer guard must allow current-field recovery.
            return fieldAllowsCurrentFieldRecovery && premiumSafetyCharmContextArmed;
        }

        internal static bool ShouldResetRevivePremiumSafetyCharmOfficialMutationOwnershipOnSessionTransition(
            bool previouslyConnected,
            bool currentlyConnected)
        {
            return previouslyConnected != currentlyConnected;
        }

        internal static bool ShouldClearRevivePremiumSafetyCharmContextOnOfficialSessionAttach(
            bool officialSessionConnected,
            bool hasContextValue,
            string lastMutationSource)
        {
            return officialSessionConnected
                && hasContextValue
                && !IsPacketOwnedRevivePremiumSafetyCharmContextOfficialSessionSource(lastMutationSource);
        }

        private bool ResolveRevivePremiumSafetyCharmContextArmed(bool fallbackArmed)
        {
            int runtimeCharacterId = ResolveReviveOwnerRuntimeCharacterId();
            if (_packetOwnedLocalUtilityContext.RequiresRevivePremiumSafetyCharmCharacterReset(runtimeCharacterId))
            {
                _packetOwnedLocalUtilityContext.ResetRevivePremiumSafetyCharmForCharacter(runtimeCharacterId);
                _packetOwnedRevivePremiumSafetyCharmOfficialMutationObserved = false;
            }

            _packetOwnedLocalUtilityContext.ObserveRevivePremiumSafetyCharmRuntimeCharacterId(runtimeCharacterId);
            return _packetOwnedLocalUtilityContext.ResolveRevivePremiumSafetyCharmContextValue(fallbackArmed);
        }

        private void SyncRevivePremiumSafetyCharmOfficialSessionLifecycle(int currentTick)
        {
            bool officialSessionConnected = _localUtilityOfficialSessionBridge.HasConnectedSession;
            if (!ShouldResetRevivePremiumSafetyCharmOfficialMutationOwnershipOnSessionTransition(
                    _packetOwnedRevivePremiumSafetyCharmLastObservedOfficialSessionConnected,
                    officialSessionConnected))
            {
                return;
            }

            _packetOwnedRevivePremiumSafetyCharmLastObservedOfficialSessionConnected = officialSessionConnected;
            _packetOwnedRevivePremiumSafetyCharmOfficialMutationObserved = false;
            if (!officialSessionConnected)
            {
                ShowUtilityFeedbackMessage(
                    "Local utility official-session bridge disconnected; revive premium safety-charm ownership is unlocked for the next runtime session.");
                return;
            }

            if (!ShouldClearRevivePremiumSafetyCharmContextOnOfficialSessionAttach(
                    officialSessionConnected,
                    _packetOwnedLocalUtilityContext.HasRevivePremiumSafetyCharmContextValue,
                    _packetOwnedLocalUtilityContext.RevivePremiumSafetyCharmLastMutationSource))
            {
                ShowUtilityFeedbackMessage(
                    "Local utility official-session bridge attached; revive premium safety-charm ownership now follows official packet mutation history.");
                return;
            }

            int runtimeCharacterId = ResolveReviveOwnerRuntimeCharacterId();
            _packetOwnedLocalUtilityContext.ClearRevivePremiumSafetyCharmContextValue(
                "official-session-attach-reset",
                currentTick,
                runtimeCharacterId);
            ShowUtilityFeedbackMessage(
                "Local utility official-session bridge attached; cleared simulator-owned revive premium safety-charm context so ownership follows official packet mutation history.");
        }

        private void SetRevivePremiumSafetyCharmContextFromInventory(string source, int currentTick)
        {
            int runtimeCharacterId = ResolveReviveOwnerRuntimeCharacterId();
            if (_packetOwnedLocalUtilityContext.RequiresRevivePremiumSafetyCharmCharacterReset(runtimeCharacterId))
            {
                _packetOwnedLocalUtilityContext.ResetRevivePremiumSafetyCharmForCharacter(runtimeCharacterId);
                _packetOwnedRevivePremiumSafetyCharmOfficialMutationObserved = false;
            }

            if (_packetOwnedRevivePremiumSafetyCharmOfficialMutationObserved)
            {
                return;
            }

            bool armed = GetInventoryWindowItemCount(5131000) > 0;
            _packetOwnedLocalUtilityContext.SetRevivePremiumSafetyCharmContextValue(
                armed,
                string.IsNullOrWhiteSpace(source)
                    ? $"revive-owner-context-slot-{ReviveOwnerPremiumSafetyCharmContextSlot}-sync"
                    : source,
                currentTick,
                runtimeCharacterId);
        }

        private void EnsureRevivePremiumSafetyCharmContextInitializedFromInventory(string source, int currentTick)
        {
            int runtimeCharacterId = ResolveReviveOwnerRuntimeCharacterId();
            if (_packetOwnedLocalUtilityContext.RequiresRevivePremiumSafetyCharmCharacterReset(runtimeCharacterId))
            {
                _packetOwnedLocalUtilityContext.ResetRevivePremiumSafetyCharmForCharacter(runtimeCharacterId);
                _packetOwnedRevivePremiumSafetyCharmOfficialMutationObserved = false;
            }

            _packetOwnedLocalUtilityContext.ObserveRevivePremiumSafetyCharmRuntimeCharacterId(runtimeCharacterId);
            if (_packetOwnedLocalUtilityContext.HasRevivePremiumSafetyCharmContextValue)
            {
                return;
            }

            if (_packetOwnedRevivePremiumSafetyCharmOfficialMutationObserved)
            {
                return;
            }

            SetRevivePremiumSafetyCharmContextFromInventory(source, currentTick);
        }

        private int ResolveReviveOwnerRuntimeCharacterId()
        {
            return _playerManager?.Player?.Build?.Id ?? 0;
        }

        private Vector2 ResolveCurrentFieldReviveRespawnPoint(ReviveOwnerVariant variant, Vector2 fallbackPoint)
        {
            return ResolveCurrentFieldReviveRespawnPointResolution(variant, fallbackPoint).Point;
        }

        private ReviveOwnerRespawnPointResolution ResolveCurrentFieldReviveRespawnPointResolution(
            ReviveOwnerVariant variant,
            Vector2 fallbackPoint)
        {
            if (!ReviveOwnerRuntime.UsesCurrentFieldRespawn(variant))
            {
                return new ReviveOwnerRespawnPointResolution(
                    fallbackPoint,
                    ReviveOwnerRespawnPointSource.DeathPoint);
            }

            Vector2 spawnPoint = _playerManager?.GetSpawnPoint() ?? fallbackPoint;
            return ResolveCurrentFieldReviveRespawnPointWithSource(_mapBoard?.MapInfo, spawnPoint, fallbackPoint);
        }

        internal static Vector2 ResolveCurrentFieldReviveRespawnPoint(MapInfo mapInfo, Vector2 spawnPoint, Vector2 fallbackPoint)
        {
            return ResolveCurrentFieldReviveRespawnPointWithSource(mapInfo, spawnPoint, fallbackPoint).Point;
        }

        internal static ReviveOwnerRespawnPointResolution ResolveCurrentFieldReviveRespawnPointWithSource(
            MapInfo mapInfo,
            Vector2 spawnPoint,
            Vector2 fallbackPoint)
        {
            if (TryGetReviveOwnerMapInfoPoint(mapInfo, "ReviveCurFieldOfNoTransferPoint", out Vector2 revivePoint))
            {
                return new ReviveOwnerRespawnPointResolution(
                    revivePoint,
                    ReviveOwnerRespawnPointSource.AuthoredCurrentFieldPoint);
            }

            if (ShouldUseCurrentFieldReviveSpawnApproximation(mapInfo))
            {
                return new ReviveOwnerRespawnPointResolution(
                    spawnPoint,
                    ReviveOwnerRespawnPointSource.SpawnApproximation);
            }

            return new ReviveOwnerRespawnPointResolution(
                fallbackPoint,
                ReviveOwnerRespawnPointSource.DeathPoint);
        }

        internal static bool TryGetReviveOwnerMapInfoFlag(MapInfo mapInfo, string propertyName, out bool value)
        {
            value = false;
            if (!TryFindReviveOwnerMapInfoProperty(mapInfo, propertyName, out WzImageProperty property))
            {
                return false;
            }

            return TryReadReviveOwnerBoolean(property, out value);
        }

        internal static bool TryGetReviveOwnerMapInfoPoint(MapInfo mapInfo, string propertyName, out Vector2 point)
        {
            point = default;
            if (!TryFindReviveOwnerMapInfoProperty(mapInfo, propertyName, out WzImageProperty property))
            {
                return false;
            }

            property = ResolveReviveOwnerLinkedProperty(property);
            if (property is WzVectorProperty vectorProperty)
            {
                point = new Vector2(vectorProperty.X.Value, vectorProperty.Y.Value);
                return true;
            }

            if (property is not WzSubProperty subProperty)
            {
                return false;
            }

            WzImageProperty xProperty = FindReviveOwnerChildProperty(subProperty, "x");
            WzImageProperty yProperty = FindReviveOwnerChildProperty(subProperty, "y");
            if (!TryReadReviveOwnerInt(xProperty, out int x)
                || !TryReadReviveOwnerInt(yProperty, out int y))
            {
                return false;
            }

            point = new Vector2(x, y);
            return true;
        }

        internal static bool ShouldUseCurrentFieldReviveSpawnApproximation(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return true;
            }

            if (TryGetReviveOwnerMapInfoFlag(mapInfo, "forceReturnOnDead", out bool forceReturnOnDead) && forceReturnOnDead)
            {
                return false;
            }

            if (TryGetReviveOwnerMapInfoFlag(mapInfo, "noResurection", out bool noResurrection) && noResurrection)
            {
                return false;
            }

            int mapId = mapInfo.id;
            return mapInfo.forcedReturn <= 0
                || mapInfo.forcedReturn == MapConstants.MaxMap
                || mapInfo.forcedReturn == mapId;
        }

        private static bool TryFindReviveOwnerMapInfoProperty(MapInfo mapInfo, string propertyName, out WzImageProperty property)
        {
            property = null;
            if (mapInfo == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            foreach (string candidateName in EnumerateReviveOwnerPropertyNameCandidates(propertyName))
            {
                property = FindReviveOwnerPropertyCandidate(mapInfo.Image?["info"]?.WzProperties, candidateName)
                    ?? FindReviveOwnerPropertyCandidate(mapInfo.unsupportedInfoProperties, candidateName)
                    ?? FindReviveOwnerPropertyCandidate(mapInfo.additionalProps, candidateName)
                    ?? FindReviveOwnerNestedInfoPropertyCandidate(mapInfo.unsupportedInfoProperties, candidateName)
                    ?? FindReviveOwnerNestedInfoPropertyCandidate(mapInfo.additionalProps, candidateName);
                if (property != null)
                {
                    return true;
                }
            }

            property = null;
            return false;
        }

        private static IEnumerable<string> EnumerateReviveOwnerPropertyNameCandidates(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                yield break;
            }

            yield return propertyName;
            foreach (string[] aliasGroup in ReviveOwnerMapInfoPropertyAliases)
            {
                if (!aliasGroup.Any(candidate => string.Equals(candidate, propertyName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                foreach (string alias in aliasGroup.Where(alias => !string.Equals(alias, propertyName, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return alias;
                }

                yield break;
            }
        }

        private static WzImageProperty FindReviveOwnerPropertyCandidate(
            System.Collections.Generic.IEnumerable<WzImageProperty> properties,
            string propertyName)
        {
            if (properties == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            WzImageProperty directProperty = properties.FirstOrDefault(
                candidate => string.Equals(candidate?.Name, propertyName, StringComparison.OrdinalIgnoreCase));
            if (directProperty != null)
            {
                return directProperty;
            }

            foreach (WzImageProperty property in properties)
            {
                if (property is not WzSubProperty subProperty)
                {
                    continue;
                }

                WzImageProperty nestedProperty = FindReviveOwnerPropertyCandidate(subProperty.WzProperties, propertyName);
                if (nestedProperty != null)
                {
                    return nestedProperty;
                }
            }

            return null;
        }

        private static WzImageProperty FindReviveOwnerNestedInfoPropertyCandidate(
            IEnumerable<WzImageProperty> properties,
            string propertyName)
        {
            if (properties == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            foreach (WzImageProperty infoCandidate in properties.Where(
                candidate => string.Equals(candidate?.Name, "info", StringComparison.OrdinalIgnoreCase)))
            {
                if (infoCandidate is not WzSubProperty infoProperty)
                {
                    continue;
                }

                WzImageProperty property = FindReviveOwnerPropertyCandidate(infoProperty.WzProperties, propertyName);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static WzImageProperty FindReviveOwnerChildProperty(WzSubProperty property, string childName)
        {
            if (property?.WzProperties == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            return property.WzProperties.FirstOrDefault(
                candidate => string.Equals(candidate?.Name, childName, StringComparison.OrdinalIgnoreCase));
        }

        private static WzImageProperty ResolveReviveOwnerLinkedProperty(WzImageProperty property)
        {
            const int maxDepth = 8;
            WzImageProperty resolved = property;
            for (int depth = 0; depth < maxDepth && resolved is WzUOLProperty uolProperty; depth++)
            {
                if (uolProperty.WzValue is not WzImageProperty linkedProperty
                    || ReferenceEquals(linkedProperty, resolved))
                {
                    break;
                }

                resolved = linkedProperty;
            }

            return resolved;
        }

        private static bool TryReadReviveOwnerBoolean(WzImageProperty property, out bool value)
        {
            value = false;
            property = ResolveReviveOwnerLinkedProperty(property);
            if (property == null)
            {
                return false;
            }

            if (property is WzSubProperty scalarContainer
                && TryResolveReviveOwnerScalarProperty(scalarContainer, out WzImageProperty scalarProperty))
            {
                property = scalarProperty;
            }

            if (property is WzStringProperty stringProperty)
            {
                string rawValue = stringProperty.Value?.Trim();
                if (bool.TryParse(rawValue, out bool parsedBool))
                {
                    value = parsedBool;
                    return true;
                }

                if (TryReadReviveOwnerNamedBoolean(rawValue, out value))
                {
                    return true;
                }

                if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt))
                {
                    value = parsedInt != 0;
                    return true;
                }

                return false;
            }

            if (!TryReadReviveOwnerNumericScalar(property, out double numericValue))
            {
                return false;
            }

            value = Math.Abs(numericValue) > double.Epsilon;
            return true;
        }

        private static bool TryReadReviveOwnerNamedBoolean(string rawValue, out bool value)
        {
            switch (rawValue?.Trim().ToLowerInvariant())
            {
                case "y":
                case "yes":
                case "on":
                case "enabled":
                    value = true;
                    return true;
                case "n":
                case "no":
                case "off":
                case "disabled":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        private static bool TryReadReviveOwnerInt(WzImageProperty property, out int value)
        {
            value = 0;
            property = ResolveReviveOwnerLinkedProperty(property);
            if (property == null)
            {
                return false;
            }

            if (property is WzSubProperty scalarContainer
                && TryResolveReviveOwnerScalarProperty(scalarContainer, out WzImageProperty scalarProperty))
            {
                property = scalarProperty;
            }

            if (property is WzStringProperty stringProperty)
            {
                string rawValue = stringProperty.Value?.Trim();
                if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                {
                    return true;
                }

                if (bool.TryParse(rawValue, out bool parsedBool))
                {
                    value = parsedBool ? 1 : 0;
                    return true;
                }

                if (TryReadReviveOwnerNamedBoolean(rawValue, out bool namedBool))
                {
                    value = namedBool ? 1 : 0;
                    return true;
                }

                return false;
            }

            if (!TryReadReviveOwnerNumericScalar(property, out double numericValue))
            {
                return false;
            }

            value = (int)Math.Round(numericValue, MidpointRounding.AwayFromZero);
            return true;
        }

        private static bool TryReadReviveOwnerNumericScalar(WzImageProperty property, out double value)
        {
            property = ResolveReviveOwnerLinkedProperty(property);
            switch (property)
            {
                case WzIntProperty intProperty:
                    value = intProperty.Value;
                    return true;
                case WzShortProperty shortProperty:
                    value = shortProperty.Value;
                    return true;
                case WzLongProperty longProperty:
                    value = longProperty.Value;
                    return true;
                case WzFloatProperty floatProperty:
                    value = floatProperty.Value;
                    return true;
                case WzDoubleProperty doubleProperty:
                    value = doubleProperty.Value;
                    return true;
                default:
                    value = 0;
                    return false;
            }
        }

        private static bool TryResolveReviveOwnerScalarProperty(WzSubProperty property, out WzImageProperty scalarProperty)
        {
            scalarProperty = null;
            if (property?.WzProperties == null || property.WzProperties.Count == 0)
            {
                return false;
            }

            string[] preferredScalarChildNames = { "value", "val", "data", "0" };
            foreach (string childName in preferredScalarChildNames)
            {
                WzImageProperty child = FindReviveOwnerChildProperty(property, childName);
                if (TryGetReviveOwnerScalarPropertyCandidate(child, out scalarProperty))
                {
                    return true;
                }
            }

            foreach (WzImageProperty child in property.WzProperties)
            {
                if (TryGetReviveOwnerScalarPropertyCandidate(child, out scalarProperty))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetReviveOwnerScalarPropertyCandidate(WzImageProperty property, out WzImageProperty scalarProperty)
        {
            scalarProperty = ResolveReviveOwnerLinkedProperty(property);
            if (scalarProperty == null)
            {
                return false;
            }

            return scalarProperty is WzStringProperty
                or WzIntProperty
                or WzShortProperty
                or WzLongProperty
                or WzFloatProperty
                or WzDoubleProperty;
        }

        private bool IsUpgradeTombReviveUsable()
        {
            // Client evidence: is_fieldtype_upgradetomb_usable(0x4b7a30)
            // blocks field types 1, 3, 4, 5, 7, 10, 11, and 15, and also
            // rejects maps in the 9xxxxxxx, 200090xxx, and 390xxxxxx ranges.
            MapInfo mapInfo = _mapBoard?.MapInfo;
            int mapId = mapInfo?.id ?? 0;
            FieldType? fieldType = mapInfo?.fieldType;

            if (fieldType is FieldType.FIELDTYPE_SNOWBALL
                or FieldType.FIELDTYPE_TOURNAMENT
                or FieldType.FIELDTYPE_COCONUT
                or FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM
                or FieldType.FIELDTYPE_PARTYRAID
                or FieldType.FIELDTYPE_GUILDBOSS
                or FieldType.FIELDTYPE_PARTYRAID_BOSS
                or FieldType.FIELDTYPE_SPACEGAGA)
            {
                return false;
            }

            return mapId / 100000000 != 9
                && mapId / 1000 != 200090
                && mapId / 1000000 != 390;
        }

        private static string BuildDefaultReviveDetail(ReviveOwnerVariant variant, string ownerLabel, Vector2 spawnPoint)
        {
            if (variant == ReviveOwnerVariant.SafetyCharmChoice)
            {
                string detailPrefix = ResolveReviveOwnerDetailPrefix(variant, ownerLabel);
                return $"{detailPrefix} still resolves through the default revive destination at ({spawnPoint.X:0}, {spawnPoint.Y:0}).";
            }

            return $"Default branch returns to the simulator respawn seam at ({spawnPoint.X:0}, {spawnPoint.Y:0}).";
        }

        private static string BuildPremiumReviveDetail(
            ReviveOwnerVariant variant,
            string ownerLabel,
            ReviveOwnerRespawnPointResolution respawnResolution)
        {
            string detailPrefix = ResolveReviveOwnerDetailPrefix(variant, ownerLabel);
            string pointSource = respawnResolution.Source switch
            {
                ReviveOwnerRespawnPointSource.AuthoredCurrentFieldPoint => "the WZ-authored no-transfer revive point",
                ReviveOwnerRespawnPointSource.SpawnApproximation => "the simulator spawn approximation",
                ReviveOwnerRespawnPointSource.DeathPoint => "the death point",
                _ => "the resolved revive point",
            };
            Vector2 respawnPoint = respawnResolution.Point;
            return $"{detailPrefix} revives in the current field at {pointSource} ({respawnPoint.X:0}, {respawnPoint.Y:0}).";
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

        private static string ResolveReviveOwnerDetailPrefix(ReviveOwnerVariant variant, string ownerLabel)
        {
            if (variant == ReviveOwnerVariant.SoulStoneChoice)
            {
                // WZ evidence: String/Skill.img/22181003/h -> "revives with #x% HP"
                return "Soul Stone buff branch";
            }

            int cashItemId = ReviveOwnerRuntime.GetConsumableCashItemId(variant);
            if (cashItemId <= 0)
            {
                return ownerLabel;
            }

            bool hasName = InventoryItemMetadataResolver.TryResolveItemName(cashItemId, out string resolvedName)
                && !string.IsNullOrWhiteSpace(resolvedName);
            bool hasDescription = InventoryItemMetadataResolver.TryResolveItemDescription(cashItemId, out string resolvedDescription)
                && !string.IsNullOrWhiteSpace(resolvedDescription);
            string normalizedDescription = hasDescription
                ? NormalizeReviveOwnerDescription(resolvedDescription)
                : string.Empty;

            if (hasName && !string.IsNullOrWhiteSpace(normalizedDescription))
            {
                return $"{resolvedName} ({cashItemId}): {normalizedDescription}";
            }

            if (hasName)
            {
                return $"{resolvedName} ({cashItemId})";
            }

            return $"{ownerLabel} ({cashItemId})";
        }

        private static string NormalizeReviveOwnerDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return string.Empty;
            }

            string normalized = description
                .Replace("#c", string.Empty, StringComparison.Ordinal)
                .Replace("#", string.Empty, StringComparison.Ordinal)
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Trim();

            int sentenceEnd = normalized.IndexOf('.');
            if (sentenceEnd >= 0)
            {
                normalized = normalized[..(sentenceEnd + 1)];
            }

            return normalized.Trim();
        }

        private void ApplyReviveOwnerWindowPlacement()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Revive) is not ReviveConfirmationWindow reviveWindow)
            {
                return;
            }

            reviveWindow.Position = ReviveOwnerRuntime.ResolveNativeWindowPosition(
                _renderParams.RenderWidth,
                _renderParams.RenderHeight);
        }

        private string DispatchReviveOwnerTransferFieldRequest(ReviveOwnerTransferRequest request)
        {
            if (!TryBuildReviveOwnerTransferFieldPayload(request.ClientPremiumFlag, out byte[] payload))
            {
                return "Revive owner could not build the synthetic transfer-field request payload.";
            }

            string payloadHex = Convert.ToHexString(payload);
            string summary = $"Mirrored CUIRevive::Revive as opcode {ReviveOwnerTransferFieldRequestOpcode} [{payloadHex}] with client bPremium={(request.ClientPremiumFlag ? 1 : 0)}, resolved premium branch={(request.Premium ? 1 : 0)}, and synthetic field key {ReviveOwnerSyntheticFieldKey}.";
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(
                ReviveOwnerTransferFieldRequestOpcode,
                payload,
                out string bridgeStatus))
            {
                return $"{summary} Dispatched it through the live official-session bridge. {bridgeStatus}";
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(
                ReviveOwnerTransferFieldRequestOpcode,
                payload,
                out string outboxStatus))
            {
                return $"{summary} Dispatched it through the generic packet outbox after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
            }

            if (_localUtilityOfficialSessionBridge.IsRunning
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(
                    ReviveOwnerTransferFieldRequestOpcode,
                    payload,
                    out string queuedBridgeStatus))
            {
                return $"{summary} Queued it for deferred official-session injection after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {queuedBridgeStatus}";
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(
                ReviveOwnerTransferFieldRequestOpcode,
                payload,
                out string queuedOutboxStatus))
            {
                return $"{summary} Queued it for deferred generic packet outbox delivery after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred outbox: {queuedOutboxStatus}";
            }

            return $"{summary} The request remained simulator-owned because neither the live bridge nor the packet outbox accepted opcode {ReviveOwnerTransferFieldRequestOpcode}. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
        }

        private string DispatchReviveOwnerUpgradeTombEffectRequest(Vector2 revivePoint, int currentTick)
        {
            RegisterReviveOwnerUpgradeTombEffect(revivePoint, currentTick);

            if (!TryBuildReviveOwnerUpgradeTombEffectPayload(revivePoint, out byte[] payload))
            {
                return "Revive owner could not build the synthetic upgrade-tomb effect request payload.";
            }

            string payloadHex = Convert.ToHexString(payload);
            string summary = $"Mirrored CUserLocal::RequestUpgradeTombEffect as opcode {ReviveOwnerUpgradeTombEffectOpcode} [{payloadHex}] with item {ReviveOwnerUpgradeTombItemId} at ({revivePoint.X:0}, {revivePoint.Y:0}).";
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(
                ReviveOwnerUpgradeTombEffectOpcode,
                payload,
                out string bridgeStatus))
            {
                return $"{summary} Dispatched it through the live official-session bridge. {bridgeStatus}";
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(
                ReviveOwnerUpgradeTombEffectOpcode,
                payload,
                out string outboxStatus))
            {
                return $"{summary} Dispatched it through the generic packet outbox after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
            }

            if (_localUtilityOfficialSessionBridge.IsRunning
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(
                    ReviveOwnerUpgradeTombEffectOpcode,
                    payload,
                    out string queuedBridgeStatus))
            {
                return $"{summary} Queued it for deferred official-session injection after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {queuedBridgeStatus}";
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(
                ReviveOwnerUpgradeTombEffectOpcode,
                payload,
                out string queuedOutboxStatus))
            {
                return $"{summary} Queued it for deferred generic packet outbox delivery after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred outbox: {queuedOutboxStatus}";
            }

            return $"{summary} The request remained simulator-owned because neither the live bridge nor the packet outbox accepted opcode {ReviveOwnerUpgradeTombEffectOpcode}. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
        }

        private void RegisterReviveOwnerUpgradeTombEffect(Vector2 revivePoint, int currentTick)
        {
            if (_animationEffects == null || _tombFallFrames == null || _tombFallFrames.Count == 0)
            {
                return;
            }

            _animationEffects.AddOneTime(
                _tombFallFrames,
                revivePoint.X,
                revivePoint.Y,
                flip: false,
                currentTick,
                zOrder: 1);
        }

        internal static bool TryBuildReviveOwnerTransferFieldPayload(bool premium, out byte[] payload)
        {
            try
            {
                using var stream = new MemoryStream();
                using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
                writer.Write(ReviveOwnerSyntheticFieldKey);
                writer.Write(0);
                WriteReviveOwnerMapleString(writer, string.Empty);
                writer.Write((byte)0);
                writer.Write((byte)(premium ? 1 : 0));
                writer.Write((byte)0);
                writer.Flush();
                payload = stream.ToArray();
                return true;
            }
            catch
            {
                payload = Array.Empty<byte>();
                return false;
            }
        }

        internal static bool TryBuildReviveOwnerUpgradeTombEffectPayload(Vector2 revivePoint, out byte[] payload)
        {
            try
            {
                using var stream = new MemoryStream();
                using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
                writer.Write(ReviveOwnerUpgradeTombItemId);
                writer.Write((int)Math.Round(revivePoint.X));
                writer.Write((int)Math.Round(revivePoint.Y));
                writer.Flush();
                payload = stream.ToArray();
                return true;
            }
            catch
            {
                payload = Array.Empty<byte>();
                return false;
            }
        }

        private static void WriteReviveOwnerMapleString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.Default.GetBytes(value ?? string.Empty);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private readonly struct ReviveOwnerPendingOpen
        {
            public ReviveOwnerPendingOpen(
                string mapName,
                string normalDetail,
                string premiumDetail,
                ReviveOwnerVariant variant,
                Vector2 premiumRespawnPoint,
                int armedAtTick)
            {
                MapName = mapName ?? string.Empty;
                NormalDetail = normalDetail ?? string.Empty;
                PremiumDetail = premiumDetail ?? string.Empty;
                Variant = variant;
                PremiumRespawnPoint = premiumRespawnPoint;
                ArmedAtTick = armedAtTick;
            }

            public string MapName { get; }
            public string NormalDetail { get; }
            public string PremiumDetail { get; }
            public ReviveOwnerVariant Variant { get; }
            public Vector2 PremiumRespawnPoint { get; }
            public int ArmedAtTick { get; }
        }
    }
}
