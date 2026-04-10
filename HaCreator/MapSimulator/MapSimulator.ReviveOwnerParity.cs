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
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int ReviveOwnerSoulStoneSkillId = 22181003;
        private const int ReviveOwnerTransferFieldRequestOpcode = 41;
        private const int ReviveOwnerUpgradeTombEffectOpcode = 58;
        private const int ReviveOwnerUpgradeTombItemId = 5510000;
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
                ShowUtilityFeedbackMessage,
                () => ResolveReviveOwnerClientButtonClick(ReviveOwnerRuntime.ClientYesButtonId, Environment.TickCount));

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
            if (variant == ReviveOwnerVariant.UpgradeTombChoice)
            {
                Debug.WriteLine(DispatchReviveOwnerUpgradeTombEffectRequest(premiumRespawnPoint, currentTick));
            }

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
                            request.Summary,
                            clientPremiumFlag: false)));
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
            bool canUseUpgradeTombRevive = IsUpgradeTombReviveUsable();

            return ResolveReviveOwnerVariant(
                hasSoulStone,
                premiumSafetyCharmCount,
                safetyCharmCount,
                wheelOfFortuneCount,
                canUsePremiumCurrentFieldRecovery,
                canUseUpgradeTombRevive);
        }

        internal static ReviveOwnerVariant ResolveReviveOwnerVariant(
            bool hasSoulStone,
            int premiumSafetyCharmCount,
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
                hasPremiumSafetyCharm: canUsePremiumCurrentFieldRecovery && premiumSafetyCharmCount > 0,
                hasSafetyCharm: safetyCharmCount > 0);
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

            return ShouldUseCurrentFieldReviveSpawnApproximation(mapInfo);
        }

        private Vector2 ResolveCurrentFieldReviveRespawnPoint(ReviveOwnerVariant variant, Vector2 fallbackPoint)
        {
            if (!ReviveOwnerRuntime.UsesCurrentFieldRespawn(variant))
            {
                return fallbackPoint;
            }

            Vector2 spawnPoint = _playerManager?.GetSpawnPoint() ?? fallbackPoint;
            return ResolveCurrentFieldReviveRespawnPoint(_mapBoard?.MapInfo, spawnPoint, fallbackPoint);
        }

        internal static Vector2 ResolveCurrentFieldReviveRespawnPoint(MapInfo mapInfo, Vector2 spawnPoint, Vector2 fallbackPoint)
        {
            return TryGetReviveOwnerMapInfoPoint(mapInfo, "ReviveCurFieldOfNoTransferPoint", out Vector2 revivePoint)
                ? revivePoint
                : ShouldUseCurrentFieldReviveSpawnApproximation(mapInfo)
                    ? spawnPoint
                    : fallbackPoint;
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

            int mapId = mapInfo.id;
            return mapInfo.forcedReturn <= 0
                || mapInfo.forcedReturn == MapConstants.MaxMap
                || mapInfo.forcedReturn == mapId;
        }

        private static bool TryFindReviveOwnerMapInfoProperty(MapInfo mapInfo, string propertyName, out WzImageProperty property)
        {
            property = null;
            if (mapInfo?.unsupportedInfoProperties == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            property = mapInfo.unsupportedInfoProperties.FirstOrDefault(
                candidate => string.Equals(candidate?.Name, propertyName, StringComparison.OrdinalIgnoreCase));
            return property != null;
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

        private static bool TryReadReviveOwnerBoolean(WzImageProperty property, out bool value)
        {
            value = false;
            if (property == null)
            {
                return false;
            }

            if (property is WzStringProperty stringProperty)
            {
                string rawValue = stringProperty.Value?.Trim();
                if (bool.TryParse(rawValue, out bool parsedBool))
                {
                    value = parsedBool;
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

        private static bool TryReadReviveOwnerInt(WzImageProperty property, out int value)
        {
            value = 0;
            if (property == null)
            {
                return false;
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
            if (!TryBuildReviveOwnerTransferFieldPayload(request.ClientPremiumFlag, out byte[] payload))
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
    }
}
