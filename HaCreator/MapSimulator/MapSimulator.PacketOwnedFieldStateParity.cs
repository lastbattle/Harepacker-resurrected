using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private PendingPortalSessionValueImpact _pendingPortalSessionValueImpact;
        private int _pendingPortalSessionValueImpactMapId = -1;
        private int _lastPortalSessionValueRequestOpcode = -1;
        private byte[] _lastPortalSessionValueRequestPayload = Array.Empty<byte>();
        private string _lastPortalSessionValueRequestSummary;

        private bool TryApplyPacketOwnedFieldScopedPacket(int packetType, byte[] payload, out string message)
        {
            if (TryParsePacketReactorPoolKind(packetType, out _))
            {
                return TryApplyPacketOwnedReactorPoolPacket(packetType, payload, out message);
            }

            if (PacketFieldIngressRouter.IsSupportedFieldStatePacketType(packetType))
            {
                return TryApplyPacketOwnedFieldStatePacket(packetType, payload, out message);
            }

            message = $"Unsupported field-scoped packet type {packetType}.";
            return false;
        }

        private bool TryApplyPacketOwnedFieldStatePacket(int packetType, byte[] payload, out string message)
        {
            if (packetType == SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
            {
                bool applied = _specialFieldRuntime.TryDispatchCurrentWrapperRelayPayload(payload, currTickCount, out message);
                if (TryApplyPendingPortalSessionValueImpactFromPacket(packetType, payload, out string portalImpactMessage))
                {
                    message = string.IsNullOrWhiteSpace(message)
                        ? portalImpactMessage
                        : $"{message} {portalImpactMessage}";
                    return true;
                }

                return applied;
            }

            if (packetType == MassacreField.PacketTypeResult
                || packetType == 178)
            {
                bool applied = _specialFieldRuntime.TryDispatchCurrentWrapperPacketRelay(packetType, payload, currTickCount, out message);
                if (TryApplyPendingPortalSessionValueImpactFromPacket(packetType, payload, out string portalImpactMessage))
                {
                    message = string.IsNullOrWhiteSpace(message)
                        ? portalImpactMessage
                        : $"{message} {portalImpactMessage}";
                    return true;
                }

                return applied;
            }

            if (packetType == PartyRaidField.ClientSessionValuePacketType)
            {
                return TryApplyClientOwnedSessionValuePacket(payload, currTickCount, out message);
            }

            _packetFieldStateRuntime.Initialize(GraphicsDevice, _mapBoard?.MapInfo);
            return _packetFieldStateRuntime.TryApplyPacket(
                packetType,
                payload,
                currTickCount,
                (tag, state, transitionTimeMs, currentTimeMs) => SetDynamicObjectTagState(tag, state, transitionTimeMs, currentTimeMs),
                HandleFieldSpecificDataPacketHandoff,
                out message);
        }

        private bool TryApplyClientOwnedSessionValuePacket(byte[] payload, int currentTick, out string message)
        {
            if (!TryDecodeMapleStringPairPayload(payload, out string key, out string value, out string error))
            {
                message = $"CWvsContext::OnSessionValue packet did not decode into a session key/value pair. {error}";
                return false;
            }

            if (TryApplyStructuredFieldSpecificPair(
                    key,
                    value,
                    PacketFieldSpecificDataOwnerHint.Session,
                    currentTick,
                    out string target))
            {
                message = $"CWvsContext::OnSessionValue applied {key}={value} ({target}).";
                return true;
            }

            message = $"CWvsContext::OnSessionValue decoded {key}={value}, but no active session owner accepted it.";
            return false;
        }

        private string HandleFieldSpecificDataPacketHandoff(byte[] payload, int currentTick)
        {
            string wrapperMessage = HandleClientOwnedFieldSpecificDataPacket(payload, currentTick);
            if (!string.IsNullOrWhiteSpace(wrapperMessage))
            {
                return wrapperMessage;
            }

            if (TryApplyStructuredFieldSpecificDataPayload(payload, currentTick, out string structuredMessage))
            {
                return structuredMessage;
            }

            string areaName = _specialFieldRuntime.ActiveArea?.ToString() ?? "no active special-field owner";
            return $"handoff target={areaName}";
        }

        private bool TryApplyStructuredFieldSpecificDataPayload(byte[] payload, int currentTick, out string message)
        {
            message = null;
            FieldSpecificStringPairOwnerMask activeOwners = GetActiveFieldSpecificStringPairOwners();
            if (payload == null ||
                payload.Length == 0 ||
                activeOwners == FieldSpecificStringPairOwnerMask.None ||
                !PacketFieldSpecificDataCodec.TryDecodeStringPairs(payload, out IReadOnlyList<KeyValuePair<string, string>> pairs, out int headerSize))
            {
                return false;
            }

            List<string> applied = new();
            foreach (KeyValuePair<string, string> pair in pairs)
            {
                string key = pair.Key;
                PacketFieldSpecificDataOwnerHint ownerHint = PacketFieldSpecificDataCodec.ResolveOwnerHint(ref key);
                if (TryApplyStructuredFieldSpecificPair(key, pair.Value, ownerHint, currentTick, out string target))
                {
                    string ownerPrefix = ownerHint == PacketFieldSpecificDataOwnerHint.None ? string.Empty : $"{ownerHint.ToString().ToLowerInvariant()}:";
                    applied.Add($"{ownerPrefix}{key}={pair.Value} ({target})");
                }
            }

            if (applied.Count == 0)
            {
                message =
                    $"decoded {pairs.Count} field-specific key/value pair(s) for {DescribeFieldSpecificStringPairOwners(activeOwners)} " +
                    $"using header size {headerSize}, but no active owner accepted them";
                return true;
            }

            message =
                $"decoded {pairs.Count} field-specific key/value pair(s) for {DescribeFieldSpecificStringPairOwners(activeOwners)} " +
                $"using header size {headerSize}: {string.Join(", ", applied.Take(4))}";
            return true;
        }

        private bool TryApplyStructuredFieldSpecificPair(
            string key,
            string value,
            PacketFieldSpecificDataOwnerHint ownerHint,
            int currentTick,
            out string target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (ShouldRouteFieldSpecificPairToPartyRaid(ownerHint) &&
                _specialFieldRuntime.PartyRaid.IsActive &&
                _specialFieldRuntime.PartyRaid.TryApplyFieldSpecificPair(key, value, ownerHint, currentTick, out string partyRaidOwner))
            {
                TryApplyPendingPortalSessionValueImpact(key, value);
                target = _specialFieldRuntime.PartyRaid.DescribeStructuredFieldSpecificTarget(partyRaidOwner);
                return true;
            }

            if (ShouldRouteFieldSpecificPairToFieldWrappers(ownerHint) &&
                IsEscortResultWrapperMap(_mapBoard?.MapInfo) &&
                TryApplyClientOwnedWrapperFieldValue("escortresult", key, value, currentTick, out _))
            {
                TryApplyPendingPortalSessionValueImpact(key, value);
                target = "escort-result wrapper";
                return true;
            }

            if (ShouldRouteFieldSpecificPairToFieldWrappers(ownerHint) &&
                TryApplyClientOwnedWrapperSessionValue("chaoszakum", key, value, out _))
            {
                TryApplyPendingPortalSessionValueImpact(key, value);
                target = "chaos-zakum session wrapper";
                return true;
            }

            if (ShouldRouteFieldSpecificPairToFieldWrappers(ownerHint) &&
                _mapBoard?.MapInfo?.fieldType == MapleLib.WzLib.WzStructure.Data.FieldType.FIELDTYPE_HUNTINGADBALLOON &&
                TryApplyClientOwnedWrapperFieldValue("huntingadballoon", key, value, currentTick, out _))
            {
                TryApplyPendingPortalSessionValueImpact(key, value);
                target = "hunting-ad-balloon wrapper";
                return true;
            }

            return false;
        }

        private static bool ShouldRouteFieldSpecificPairToPartyRaid(PacketFieldSpecificDataOwnerHint ownerHint)
        {
            return ownerHint is PacketFieldSpecificDataOwnerHint.None
                or PacketFieldSpecificDataOwnerHint.Field
                or PacketFieldSpecificDataOwnerHint.Party
                or PacketFieldSpecificDataOwnerHint.Session;
        }

        private static bool ShouldRouteFieldSpecificPairToFieldWrappers(PacketFieldSpecificDataOwnerHint ownerHint)
        {
            return ownerHint is PacketFieldSpecificDataOwnerHint.None
                or PacketFieldSpecificDataOwnerHint.Field
                or PacketFieldSpecificDataOwnerHint.Session;
        }

        private FieldSpecificStringPairOwnerMask GetActiveFieldSpecificStringPairOwners()
        {
            FieldSpecificStringPairOwnerMask owners = FieldSpecificStringPairOwnerMask.None;
            if (_specialFieldRuntime.PartyRaid.IsActive)
            {
                owners |= FieldSpecificStringPairOwnerMask.PartyRaid;
            }

            if (IsEscortResultWrapperMap(_mapBoard?.MapInfo))
            {
                owners |= FieldSpecificStringPairOwnerMask.EscortResult;
            }

            if (_mapBoard?.MapInfo?.fieldType == MapleLib.WzLib.WzStructure.Data.FieldType.FIELDTYPE_HUNTINGADBALLOON)
            {
                owners |= FieldSpecificStringPairOwnerMask.HuntingAdBalloon;
            }

            if (IsChaosZakumPortalSessionWrapperMap(_mapBoard?.MapInfo))
            {
                owners |= FieldSpecificStringPairOwnerMask.ChaosZakum;
            }

            return owners;
        }

        private string DescribeFieldSpecificStringPairOwners(FieldSpecificStringPairOwnerMask owners)
        {
            List<string> names = new();
            if ((owners & FieldSpecificStringPairOwnerMask.PartyRaid) != 0)
            {
                PartyRaidField partyRaid = _specialFieldRuntime.PartyRaid;
                names.Add(partyRaid.HasNativePartyRaidWrapperOwner
                    ? partyRaid.ClientWrapperOwnerName
                    : "PartyRaidField");
            }

            if ((owners & FieldSpecificStringPairOwnerMask.EscortResult) != 0)
            {
                names.Add("escort-result wrapper");
            }

            if ((owners & FieldSpecificStringPairOwnerMask.HuntingAdBalloon) != 0)
            {
                names.Add("hunting-ad-balloon wrapper");
            }

            if ((owners & FieldSpecificStringPairOwnerMask.ChaosZakum) != 0)
            {
                names.Add("chaos-zakum session wrapper");
            }

            return names.Count == 0 ? "no known owner" : string.Join(", ", names);
        }

        private void QueuePendingPortalSessionValueImpact(PendingPortalSessionValueImpact pendingImpact)
        {
            if (pendingImpact?.IsValid != true)
            {
                _pendingPortalSessionValueImpact = null;
                _pendingPortalSessionValueImpactMapId = -1;
                return;
            }

            _pendingPortalSessionValueImpact = pendingImpact;
            _pendingPortalSessionValueImpactMapId = _mapBoard?.MapInfo?.id ?? -1;
        }

        private bool TryDispatchPortalSessionValueRequest(string key)
        {
            if (!PortalSessionValueRequestCodec.TryBuildPayload(key, out byte[] payload))
            {
                _lastPortalSessionValueRequestOpcode = -1;
                _lastPortalSessionValueRequestPayload = Array.Empty<byte>();
                _lastPortalSessionValueRequestSummary = "Portal session-value request was not dispatched because the key was empty.";
                return false;
            }

            _lastPortalSessionValueRequestOpcode = PortalSessionValueRequestCodec.Opcode;
            _lastPortalSessionValueRequestPayload = payload;
            _lastPortalSessionValueRequestSummary = DispatchPortalSessionValueRequest(payload, key?.Trim());
            return true;
        }

        private string DispatchPortalSessionValueRequest(byte[] payload, string key)
        {
            payload ??= Array.Empty<byte>();
            string source = $"Recorded CWvsContext::SendRequestSessionValue opcode {PortalSessionValueRequestCodec.Opcode} for key '{key}' with reset={PortalSessionValueRequestCodec.RequestResetFlag}.";

            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(
                PortalSessionValueRequestCodec.Opcode,
                payload,
                out string bridgeStatus))
            {
                return $"{source} Dispatched it through the live official-session bridge. {bridgeStatus}";
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(
                PortalSessionValueRequestCodec.Opcode,
                payload,
                out string outboxStatus))
            {
                return $"{source} Dispatched it through the generic packet outbox after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
            }

            if (_localUtilityOfficialSessionBridge.IsRunning
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(
                    PortalSessionValueRequestCodec.Opcode,
                    payload,
                    out string queuedBridgeStatus))
            {
                return $"{source} Queued it for deferred official-session injection after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {queuedBridgeStatus}";
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(
                PortalSessionValueRequestCodec.Opcode,
                payload,
                out string queuedOutboxStatus))
            {
                return $"{source} Queued it for deferred generic packet outbox delivery after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred outbox: {queuedOutboxStatus}";
            }

            return $"{source} The request remained simulator-owned because neither the live bridge nor the packet outbox accepted opcode {PortalSessionValueRequestCodec.Opcode}. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
        }

        private bool TryApplyPendingPortalSessionValueImpact(string key, string value)
        {
            PendingPortalSessionValueImpact pendingImpact = _pendingPortalSessionValueImpact;
            if (pendingImpact?.IsValid != true
                || _pendingPortalSessionValueImpactMapId != (_mapBoard?.MapInfo?.id ?? -1)
                || !pendingImpact.Matches(key, value))
            {
                return false;
            }

            PlayerCharacter player = _playerManager?.Player;
            if (player?.Physics == null)
            {
                return false;
            }

            player.Physics.SetImpactNext(pendingImpact.VelocityX, pendingImpact.VelocityY);
            _pendingPortalSessionValueImpact = null;
            _pendingPortalSessionValueImpactMapId = -1;
            _ = ClearPacketOwnedTeleportPassengerLink();
            return true;
        }

        private bool TryApplyPendingPortalSessionValueImpactFromPacket(int packetType, byte[] payload)
        {
            return TryApplyPendingPortalSessionValueImpactFromPacket(packetType, payload, out _);
        }

        private bool TryApplyPendingPortalSessionValueImpactFromPacket(int packetType, byte[] payload, out string message)
        {
            message = null;
            if (_pendingPortalSessionValueImpact?.IsValid != true
                || !TryDecodePortalSessionValueImpactPacketPairs(packetType, payload, out IReadOnlyList<KeyValuePair<string, string>> pairs, out _))
            {
                return false;
            }

            foreach (KeyValuePair<string, string> pair in pairs)
            {
                if (TryApplyPendingPortalSessionValueImpact(pair.Key, pair.Value))
                {
                    message = $"Released pending portal session-value impact from packet {packetType} for {pair.Key}={pair.Value}.";
                    return true;
                }
            }

            return false;
        }

        internal static bool TryDecodePortalSessionValueImpactPacketPairs(
            int packetType,
            byte[] payload,
            out IReadOnlyList<KeyValuePair<string, string>> pairs,
            out string error)
        {
            pairs = Array.Empty<KeyValuePair<string, string>>();
            error = null;
            payload ??= Array.Empty<byte>();

            if (packetType == SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
            {
                if (!SpecialFieldRuntimeCoordinator.TryDecodeCurrentWrapperRelayPayload(
                        payload,
                        out int relayedPacketType,
                        out byte[] relayedPayload,
                        out string relayError))
                {
                    error = relayError;
                    return false;
                }

                packetType = relayedPacketType;
                payload = relayedPayload;
            }

            if (packetType == PartyRaidField.ClientSessionValuePacketType
                || packetType == PartyRaidField.ClientFieldSetVariablePacketType)
            {
                if (!TryDecodeMapleStringPairPayload(payload, out string key, out string value, out error))
                {
                    return false;
                }

                pairs = new[] { new KeyValuePair<string, string>(key, value) };
                return true;
            }

            if (packetType == 149)
            {
                if (!PacketFieldSpecificDataCodec.TryDecodeStringPairs(payload, out pairs, out int headerSize))
                {
                    error = "Portal session-value impact packet did not decode into Maple string pairs.";
                    return false;
                }

                if (!TryNormalizePortalSessionValueImpactPairs(pairs, out pairs))
                {
                    error = "Portal session-value impact packet did not contain any usable session or field key/value pairs.";
                    return false;
                }

                error = $"decoded field-specific packet header size {headerSize}";
                return true;
            }

            error = $"Packet type {packetType} does not carry portal session-value impact pairs.";
            return false;
        }

        private static bool TryNormalizePortalSessionValueImpactPairs(
            IReadOnlyList<KeyValuePair<string, string>> pairs,
            out IReadOnlyList<KeyValuePair<string, string>> normalizedPairs)
        {
            normalizedPairs = Array.Empty<KeyValuePair<string, string>>();
            if (pairs == null || pairs.Count == 0)
            {
                return false;
            }

            List<KeyValuePair<string, string>> normalized = new(pairs.Count);
            foreach (KeyValuePair<string, string> pair in pairs)
            {
                string key = pair.Key;
                _ = PacketFieldSpecificDataCodec.ResolveOwnerHint(ref key);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                normalized.Add(new KeyValuePair<string, string>(key, pair.Value));
            }

            if (normalized.Count == 0)
            {
                return false;
            }

            normalizedPairs = normalized;
            return true;
        }

        internal static bool TryDecodeMapleStringPairPayload(
            byte[] payload,
            out string key,
            out string value,
            out string error)
        {
            key = string.Empty;
            value = string.Empty;
            error = null;
            payload ??= Array.Empty<byte>();

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                key = ReadMapleString(reader);
                value = ReadMapleString(reader);
                if (stream.Position != stream.Length)
                {
                    error = "Maple string pair payload had trailing bytes after the value string.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(key))
                {
                    error = "Maple string pair payload key was empty.";
                    return false;
                }

                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is ArgumentException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            ushort length = reader.ReadUInt16();
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Maple string pair payload ended before its declared Maple-string length.");
            }

            return Encoding.Default.GetString(bytes);
        }

        [Flags]
        private enum FieldSpecificStringPairOwnerMask
        {
            None = 0,
            PartyRaid = 1 << 0,
            EscortResult = 1 << 1,
            HuntingAdBalloon = 1 << 2,
            ChaosZakum = 1 << 3
        }

        private QuestLogSnapshot BuildQuestLogSnapshotWithPacketState(QuestLogTabType tab, bool showAllLevels)
        {
            QuestLogSnapshot snapshot = _questRuntime.BuildQuestLogSnapshot(tab, _playerManager?.Player?.Build, showAllLevels);
            if (snapshot?.Entries == null || snapshot.Entries.Count == 0)
            {
                return snapshot;
            }

            List<QuestLogEntrySnapshot> updatedEntries = null;
            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                QuestLogEntrySnapshot entry = snapshot.Entries[i];
                if (!_packetFieldStateRuntime.TryGetQuestTimerText(entry.QuestId, currTickCount, out string timerText))
                {
                    continue;
                }

                updatedEntries ??= new List<QuestLogEntrySnapshot>(snapshot.Entries);
                updatedEntries[i] = new QuestLogEntrySnapshot
                {
                    QuestId = entry.QuestId,
                    Name = entry.Name,
                    State = entry.State,
                    StatusText = string.IsNullOrWhiteSpace(entry.StatusText)
                        ? timerText
                        : $"{entry.StatusText} | {timerText}",
                    SummaryText = entry.SummaryText,
                    StageText = string.IsNullOrWhiteSpace(entry.StageText)
                        ? timerText
                        : $"{entry.StageText}\n{timerText}",
                    NpcText = entry.NpcText,
                    ProgressRatio = entry.ProgressRatio,
                    CanStart = entry.CanStart,
                    CanComplete = entry.CanComplete,
                    RequirementLines = entry.RequirementLines,
                    RewardLines = entry.RewardLines,
                    IssueLines = entry.IssueLines
                };
            }

            return updatedEntries == null
                ? snapshot
                : new QuestLogSnapshot { Entries = updatedEntries };
        }

        private QuestWindowDetailState GetQuestWindowDetailStateWithPacketState(int questId)
        {
            QuestDetailDeliveryType deliveryTypeOverride =
                _lastDeliveryQuestId == questId
                    ? _lastPacketOwnedDeliveryType
                    : QuestDetailDeliveryType.None;
            QuestWindowDetailState state = deliveryTypeOverride != QuestDetailDeliveryType.None
                ? _questRuntime.GetQuestWindowDetailState(questId, _playerManager?.Player?.Build, deliveryTypeOverride)
                : _questRuntime.GetQuestWindowDetailState(questId, _playerManager?.Player?.Build);
            if (state == null || !_packetFieldStateRuntime.TryGetQuestTimerText(questId, currTickCount, out string timerText))
            {
                return state;
            }

            string hintText = string.IsNullOrWhiteSpace(state.HintText)
                ? timerText
                : $"{timerText}\n{state.HintText}";
            return new QuestWindowDetailState
            {
                QuestId = state.QuestId,
                Title = state.Title,
                HeaderNoteText = state.HeaderNoteText,
                State = state.State,
                SummaryText = state.SummaryText,
                RequirementText = state.RequirementText,
                RewardText = state.RewardText,
                HintText = hintText,
                NpcText = state.NpcText,
                RequirementLines = state.RequirementLines,
                RewardLines = state.RewardLines,
                CurrentProgress = state.CurrentProgress,
                TotalProgress = state.TotalProgress,
                PrimaryAction = state.PrimaryAction,
                PrimaryActionEnabled = state.PrimaryActionEnabled,
                PrimaryActionSelected = state.PrimaryActionSelected,
                PrimaryActionLabel = state.PrimaryActionLabel,
                SecondaryAction = state.SecondaryAction,
                SecondaryActionEnabled = state.SecondaryActionEnabled,
                SecondaryActionLabel = state.SecondaryActionLabel,
                TertiaryAction = state.TertiaryAction,
                TertiaryActionEnabled = state.TertiaryActionEnabled,
                TertiaryActionLabel = state.TertiaryActionLabel,
                QuaternaryAction = state.QuaternaryAction,
                QuaternaryActionEnabled = state.QuaternaryActionEnabled,
                QuaternaryActionLabel = state.QuaternaryActionLabel,
                TargetNpcId = state.TargetNpcId,
                TargetNpcName = state.TargetNpcName,
                TargetMobId = state.TargetMobId,
                TargetMobName = state.TargetMobName,
                TargetItemId = state.TargetItemId,
                TargetItemName = state.TargetItemName,
                HasDetailInset = true,
                TimeLimitSeconds = state.TimeLimitSeconds,
                RemainingTimeSeconds = state.RemainingTimeSeconds,
                TimerUiKey = state.TimerUiKey,
                DeliveryType = state.DeliveryType,
                DeliveryActionEnabled = state.DeliveryActionEnabled,
                DeliveryCashItemId = state.DeliveryCashItemId,
                DeliveryCashItemName = state.DeliveryCashItemName,
                NpcButtonStyle = state.NpcButtonStyle
            };
        }
    }
}
