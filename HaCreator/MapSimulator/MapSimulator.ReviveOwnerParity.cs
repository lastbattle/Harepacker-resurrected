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
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int ReviveOwnerSoulStoneSkillId = 22181003;
        private const int ReviveOwnerTransferFieldRequestOpcode = 41;
        private const byte ReviveOwnerSyntheticFieldKey = 0;

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
            Vector2 premiumRespawnPoint = ResolveCurrentFieldReviveRespawnPoint(variant, deathPoint);

            string normalDetail = BuildDefaultReviveDetail(variant, ownerLabel, spawnPoint);
            string premiumDetail = hasPremiumChoice
                ? BuildPremiumReviveDetail(variant, ownerLabel, deathPoint, premiumRespawnPoint)
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
                    Debug.WriteLine(DispatchReviveOwnerTransferFieldRequest(
                        new ReviveOwnerTransferRequest(
                            premium: false,
                            timedOut: request.TimedOut,
                            request.Variant,
                            request.Summary)));
                    _playerManager?.Respawn();
                    return;
                }

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
            int premiumSafetyCharmCount = GetInventoryWindowItemCount(5131000);
            int safetyCharmCount = GetInventoryWindowItemCount(5130000);
            int wheelOfFortuneCount = GetInventoryWindowItemCount(5510000);
            bool canUsePremiumCurrentFieldRecovery = IsPremiumCurrentFieldReviveUsable();

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
                hasUpgradeTombChoice: wheelOfFortuneCount > 0 && IsUpgradeTombReviveUsable(),
                hasPremiumSafetyCharm: canUsePremiumCurrentFieldRecovery && premiumSafetyCharmCount > 0,
                hasSafetyCharm: canUsePremiumCurrentFieldRecovery && safetyCharmCount > 0);
        }

        private bool IsPremiumCurrentFieldReviveUsable()
        {
            MapInfo mapInfo = _mapBoard?.MapInfo;
            if (mapInfo == null)
            {
                return true;
            }

            if (TryGetReviveOwnerMapInfoFlag(mapInfo, "reviveCurField", out bool reviveCurField))
            {
                return reviveCurField;
            }

            if (TryGetReviveOwnerMapInfoFlag(mapInfo, "ReviveCurFieldOfNoTransfer", out bool reviveCurFieldOfNoTransfer))
            {
                return reviveCurFieldOfNoTransfer;
            }

            if (TryGetReviveOwnerMapInfoFlag(mapInfo, "forceReturnOnDead", out bool forceReturnOnDead) && forceReturnOnDead)
            {
                return false;
            }

            int mapId = mapInfo.id;
            return mapInfo.forcedReturn <= 0
                || mapInfo.forcedReturn == MapConstants.MaxMap
                || mapInfo.forcedReturn == mapId;
        }

        private Vector2 ResolveCurrentFieldReviveRespawnPoint(ReviveOwnerVariant variant, Vector2 fallbackPoint)
        {
            if (!ReviveOwnerRuntime.UsesCurrentFieldRespawn(variant))
            {
                return fallbackPoint;
            }

            return TryGetReviveOwnerMapInfoPoint(_mapBoard?.MapInfo, "ReviveCurFieldOfNoTransferPoint", out Vector2 revivePoint)
                ? revivePoint
                : fallbackPoint;
        }

        internal static bool TryGetReviveOwnerMapInfoFlag(MapInfo mapInfo, string propertyName, out bool value)
        {
            value = false;
            if (mapInfo?.unsupportedInfoProperties == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            var property = mapInfo.unsupportedInfoProperties.FirstOrDefault(
                candidate => string.Equals(candidate?.Name, propertyName, StringComparison.OrdinalIgnoreCase));
            if (property == null)
            {
                return false;
            }

            value = property switch
            {
                WzStringProperty stringProperty when bool.TryParse(stringProperty.Value, out bool parsedBool) => parsedBool,
                WzStringProperty stringProperty when int.TryParse(stringProperty.Value, out int parsed) => parsed != 0,
                _ => InfoTool.GetInt(property, 0) != 0
            };
            return true;
        }

        internal static bool TryGetReviveOwnerMapInfoPoint(MapInfo mapInfo, string propertyName, out Vector2 point)
        {
            point = default;
            if (mapInfo?.unsupportedInfoProperties == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            var property = mapInfo.unsupportedInfoProperties.FirstOrDefault(
                candidate => string.Equals(candidate?.Name, propertyName, StringComparison.OrdinalIgnoreCase));
            if (property is WzVectorProperty vectorProperty)
            {
                point = new Vector2(vectorProperty.X.Value, vectorProperty.Y.Value);
                return true;
            }

            if (property is not WzSubProperty subProperty)
            {
                return false;
            }

            WzImageProperty xProperty = subProperty["x"] ?? subProperty["X"];
            WzImageProperty yProperty = subProperty["y"] ?? subProperty["Y"];
            if (xProperty == null || yProperty == null)
            {
                return false;
            }

            point = new Vector2(InfoTool.GetInt(xProperty, 0), InfoTool.GetInt(yProperty, 0));
            return true;
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

        private static string BuildPremiumReviveDetail(ReviveOwnerVariant variant, string ownerLabel, Vector2 deathPoint, Vector2 respawnPoint)
        {
            string detailPrefix = ResolveReviveOwnerDetailPrefix(variant, ownerLabel);
            bool usesFieldAuthoredPoint = Vector2.DistanceSquared(respawnPoint, deathPoint) > 1f;
            string pointSource = usesFieldAuthoredPoint
                ? "the WZ-authored no-transfer revive point"
                : "the death point";
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

        private string DispatchReviveOwnerTransferFieldRequest(ReviveOwnerTransferRequest request)
        {
            if (!TryBuildReviveOwnerTransferFieldPayload(request.Premium, out byte[] payload))
            {
                return "Revive owner could not build the synthetic transfer-field request payload.";
            }

            string payloadHex = Convert.ToHexString(payload);
            string summary = $"Mirrored CUIRevive::Revive as opcode {ReviveOwnerTransferFieldRequestOpcode} [{payloadHex}] with premium={(request.Premium ? 1 : 0)} and synthetic field key {ReviveOwnerSyntheticFieldKey}.";
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

        private static void WriteReviveOwnerMapleString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.Default.GetBytes(value ?? string.Empty);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }
    }
}
