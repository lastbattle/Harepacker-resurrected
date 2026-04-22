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
        private readonly List<PendingPortalSessionValueImpact> _pendingPortalSessionValueImpacts = new();
        private int _lastPortalSessionValueRequestOpcode = -1;
        private byte[] _lastPortalSessionValueRequestPayload = Array.Empty<byte>();
        private string _lastPortalSessionValueRequestSummary;
        private int _lastPortalSessionValueRequestSentTick = int.MinValue;

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
            bool packetApplied = _packetFieldStateRuntime.TryApplyPacket(
                packetType,
                payload,
                currTickCount,
                (tag, state, transitionTimeMs, currentTimeMs) => SetDynamicObjectTagState(tag, state, transitionTimeMs, currentTimeMs),
                tag => TryGetDynamicObjectTagState(tag?.Trim()),
                HandleFieldSpecificDataPacketHandoff,
                out message);
            if (TryApplyPendingPortalSessionValueImpactFromPacket(packetType, payload, out string fieldStatePortalImpactMessage))
            {
                message = string.IsNullOrWhiteSpace(message)
                    ? fieldStatePortalImpactMessage
                    : $"{message} {fieldStatePortalImpactMessage}";
                return true;
            }

            return packetApplied;
        }

        private bool TryApplyClientOwnedSessionValuePacket(byte[] payload, int currentTick, out string message)
        {
            if (!TryDecodeMapleStringPairPayload(payload, out string key, out string value, out string error))
            {
                message = $"CWvsContext::OnSessionValue packet did not decode into a session key/value pair. {error}";
                return false;
            }

            bool animationDisplayerCoolApplied = TryApplyAnimationDisplayerSessionValueCoolOwner(
                key,
                value,
                currentTick,
                out string animationDisplayerCoolMessage);

            bool releasedPendingImpact = TryApplyPendingPortalSessionValueImpact(
                key,
                value,
                PartyRaidField.ClientSessionValuePacketType,
                PacketFieldSpecificDataOwnerHint.Session);

            bool structuredApplied = TryApplyStructuredFieldSpecificPair(
                key,
                value,
                PacketFieldSpecificDataOwnerHint.Session,
                currentTick,
                out string target);
            if (structuredApplied)
            {
                string impactSuffix = releasedPendingImpact
                    ? " Released the pending portal session-value impact before the field virtual owner ran."
                    : string.Empty;
                string coolSuffix = animationDisplayerCoolApplied
                    ? $" {animationDisplayerCoolMessage}."
                    : string.Empty;
                message = $"CWvsContext::OnSessionValue applied {key}={value} ({target}).{impactSuffix}{coolSuffix}";
                return true;
            }

            if (releasedPendingImpact)
            {
                string coolSuffix = animationDisplayerCoolApplied
                    ? $" {animationDisplayerCoolMessage}."
                    : string.Empty;
                message = $"CWvsContext::OnSessionValue released pending portal session-value impact for {key}={value} before any active session owner accepted it.{coolSuffix}";
                return true;
            }

            if (animationDisplayerCoolApplied)
            {
                message = $"CWvsContext::OnSessionValue applied {animationDisplayerCoolMessage}, but no active session owner accepted {key}={value}.";
                return true;
            }

            message = $"CWvsContext::OnSessionValue decoded {key}={value}, but no active session owner accepted it.";
            return false;
        }

        private string HandleFieldSpecificDataPacketHandoff(byte[] payload, int currentTick)
        {
            bool wrapperApplied = _specialFieldRuntime.TryDispatchCurrentWrapperFieldSpecificData(
                TryApplyShowaBathFieldSpecificPresentationOwner,
                out string wrapperMessage);
            if (wrapperApplied || !string.IsNullOrWhiteSpace(wrapperMessage))
            {
                return wrapperMessage;
            }

            if (TryHandleDojoFieldSpecificDataPacket(payload, currentTick, out string dojoMessage))
            {
                return dojoMessage;
            }

            if (TryHandleTutorialFieldSpecificDataPacket(payload, out string tutorialMessage))
            {
                return tutorialMessage;
            }

            if (TryHandleCurrentWrapperFieldSpecificRelayPacket(payload, currentTick, out string relayMessage))
            {
                return relayMessage;
            }

            if (TryApplyStructuredFieldSpecificDataPayload(payload, currentTick, out string structuredMessage))
            {
                return structuredMessage;
            }

            string areaName = _specialFieldRuntime.ActiveArea?.ToString() ?? "no active special-field owner";
            return $"handoff target={areaName}";
        }

        private bool TryHandleDojoFieldSpecificDataPacket(byte[] payload, int currentTick, out string message)
        {
            message = null;
            if (_specialFieldRuntime.ActiveArea != SpecialFieldBacklogArea.MuLungDojoFieldFlow)
            {
                return false;
            }

            if (!TryDecodeDojoFieldSpecificRelayPayload(payload, out int packetType, out byte[] packetPayload, out string decodeSummary))
            {
                return false;
            }

            bool applied = _specialFieldRuntime.TryDispatchCurrentWrapperPacket(packetType, packetPayload, currentTick, out string wrapperMessage);
            string handoffSummary = $"CField_MuLungDojo::OnFieldSpecificData decoded relay packet {packetType} and dispatched it through the active Dojo wrapper owner.";
            if (string.IsNullOrWhiteSpace(wrapperMessage))
            {
                message = $"{handoffSummary} {decodeSummary}";
            }
            else
            {
                message = $"{handoffSummary} {wrapperMessage} {decodeSummary}";
            }

            return applied || !string.IsNullOrWhiteSpace(wrapperMessage);
        }

        private bool TryHandleTutorialFieldSpecificDataPacket(byte[] payload, out string message)
        {
            message = null;
            if (!TryBuildTutorialWrapperContract(_mapBoard?.MapInfo, out TutorialWrapperContract contract) ||
                contract.Kind != TutorialWrapperKind.Tutorial)
            {
                return false;
            }

            if (!TryApplyTutorialFieldSpecificAppearanceOwner(payload, out string tutorialMessage))
            {
                return false;
            }

            string ownerSummary = "CField_Tutorial::DecodeFieldSpecificData accepted packet-owned field-specific appearance payload.";
            message = string.IsNullOrWhiteSpace(tutorialMessage)
                ? ownerSummary
                : $"{ownerSummary} {tutorialMessage}";
            return true;
        }

        private bool TryHandleCurrentWrapperFieldSpecificRelayPacket(byte[] payload, int currentTick, out string message)
        {
            message = null;
            if (payload == null || payload.Length < sizeof(ushort))
            {
                return false;
            }

            if (!TryDecodeFieldSpecificCurrentWrapperRelayPacketChain(
                    payload,
                    out int packetType,
                    out byte[] packetPayload,
                    out string relayEvidence,
                    out _))
            {
                return false;
            }

            bool applied = _specialFieldRuntime.TryDispatchCurrentWrapperPacket(
                packetType,
                packetPayload,
                currentTick,
                out string wrapperMessage);
            if (!applied)
            {
                return false;
            }

            string relaySummary = $"CField::OnFieldSpecificData decoded wrapper relay packet {packetType} with {packetPayload.Length} payload byte(s).";
            if (!string.IsNullOrWhiteSpace(relayEvidence))
            {
                relaySummary = $"{relaySummary} {relayEvidence}";
            }

            message = string.IsNullOrWhiteSpace(wrapperMessage)
                ? relaySummary
                : $"{relaySummary} {wrapperMessage}";
            return true;
        }

        internal static bool TryDecodeFieldSpecificCurrentWrapperRelayPacketChain(
            byte[] payload,
            out int packetType,
            out byte[] packetPayload,
            out string relayEvidence,
            out string error)
        {
            packetType = -1;
            packetPayload = Array.Empty<byte>();
            relayEvidence = string.Empty;
            error = null;
            payload ??= Array.Empty<byte>();

            if (!SpecialFieldRuntimeCoordinator.TryDecodeCurrentWrapperRelayPayload(
                    payload,
                    out int relayedPacketType,
                    out byte[] relayedPayload,
                    out string relayDecodeError))
            {
                error = relayDecodeError;
                return false;
            }

            List<int> relayPacketTypes = new() { relayedPacketType };
            packetType = relayedPacketType;
            packetPayload = relayedPayload;
            const int maxNestedRelayDepth = 8;
            for (int depth = 1; depth < maxNestedRelayDepth && packetType == SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode; depth++)
            {
                if (!SpecialFieldRuntimeCoordinator.TryDecodeCurrentWrapperRelayPayload(
                        packetPayload,
                        out relayedPacketType,
                        out relayedPayload,
                        out relayDecodeError))
                {
                    error = relayDecodeError;
                    return false;
                }

                relayPacketTypes.Add(relayedPacketType);
                packetType = relayedPacketType;
                packetPayload = relayedPayload;
            }

            relayEvidence = relayPacketTypes.Count > 1
                ? $"Decoded wrapper relay packet-id prefixes {string.Join("->", relayPacketTypes)}."
                : string.Empty;

            if (packetType == SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
            {
                error =
                    $"Field-specific wrapper relay decode exceeded bounded depth while unwrapping nested packet id {SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode}. {relayEvidence}";
                return false;
            }

            return true;
        }

        internal static bool TryDecodeDojoFieldSpecificRelayPayload(
            byte[] payload,
            out int packetType,
            out byte[] packetPayload,
            out string summary)
        {
            packetType = -1;
            packetPayload = Array.Empty<byte>();
            if (DojoField.TryDecodeFieldSpecificPacketPayload(payload, out packetType, out packetPayload, out string decodeError))
            {
                summary =
                    $"Dojo field-specific relay decoded packet {packetType} with {packetPayload.Length} payload byte(s).";
                return true;
            }

            if (TryDecodeNestedDojoFieldSpecificRelayPayload(payload, out packetType, out packetPayload, out string nestedEvidence))
            {
                summary =
                    $"Dojo field-specific relay decoded packet {packetType} with {packetPayload.Length} payload byte(s) from nested relay packet-id prefixes ({nestedEvidence}).";
                return true;
            }

            string candidateSummary = DojoField.DescribeFieldSpecificPayloadCandidates(payload);
            if (string.IsNullOrWhiteSpace(candidateSummary) ||
                string.Equals(candidateSummary, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                summary = $"Dojo field-specific relay decode failed. {decodeError}";
            }
            else
            {
                summary = $"Dojo field-specific relay decode failed. {decodeError} Candidate payloads: {candidateSummary}.";
            }

            return false;
        }

        private static bool TryDecodeNestedDojoFieldSpecificRelayPayload(
            byte[] payload,
            out int packetType,
            out byte[] packetPayload,
            out string evidence)
        {
            packetType = -1;
            packetPayload = Array.Empty<byte>();
            evidence = string.Empty;
            payload ??= Array.Empty<byte>();
            if (!SpecialFieldRuntimeCoordinator.TryDecodeCurrentWrapperRelayPayload(
                    payload,
                    out int relayPacketType,
                    out byte[] relayPayload,
                    out _))
            {
                return false;
            }

            if (!SpecialFieldRuntimeCoordinator.TryDecodeDojoPacketFromRelayPrefixChain(
                    relayPacketType,
                    relayPayload,
                    out packetType,
                    out packetPayload,
                    out string relayEvidence))
            {
                return false;
            }

            evidence = $"nested-relay:{relayEvidence}";
            return true;
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
                TryApplyPendingPortalSessionValueImpact(key, value, 149, ownerHint);
                target = _specialFieldRuntime.PartyRaid.DescribeStructuredFieldSpecificTarget(partyRaidOwner);
                return true;
            }

            if (ShouldRouteFieldSpecificPairToFieldWrappers(ownerHint) &&
                IsEscortResultWrapperMap(_mapBoard?.MapInfo) &&
                _specialFieldRuntime.TryDispatchCurrentWrapperFieldValue(key, value, currentTick, out string wrapperMessage))
            {
                TryApplyPendingPortalSessionValueImpact(key, value, 149, ownerHint);
                target = wrapperMessage;
                return true;
            }

            if (ShouldRouteFieldSpecificPairToFieldWrappers(ownerHint) &&
                _specialFieldRuntime.TryDispatchCurrentWrapperSessionValue(key, value, out string sessionMessage))
            {
                TryApplyPendingPortalSessionValueImpact(key, value, 149, ownerHint);
                target = sessionMessage;
                return true;
            }

            if (ShouldRouteFieldSpecificPairToFieldWrappers(ownerHint) &&
                _mapBoard?.MapInfo?.fieldType == MapleLib.WzLib.WzStructure.Data.FieldType.FIELDTYPE_HUNTINGADBALLOON &&
                _specialFieldRuntime.TryDispatchCurrentWrapperFieldValue(key, value, currentTick, out string huntingAdBalloonMessage))
            {
                TryApplyPendingPortalSessionValueImpact(key, value, 149, ownerHint);
                target = huntingAdBalloonMessage;
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
                ClearPendingPortalSessionValueImpacts();
                return;
            }

            int currentMapId = _mapBoard?.MapInfo?.id ?? -1;
            PrunePendingPortalSessionValueImpacts(currentMapId);
            _pendingPortalSessionValueImpacts.RemoveAll(entry =>
                PendingPortalSessionValueImpact.ShouldReplaceQueuedImpact(entry, pendingImpact));
            _pendingPortalSessionValueImpacts.Add(pendingImpact);
        }

        private bool TryDispatchPortalSessionValueRequest(string key, int requestTick)
        {
            if (!PortalSessionValueRequestCodec.TryBuildPayload(key, out byte[] payload))
            {
                _lastPortalSessionValueRequestOpcode = -1;
                _lastPortalSessionValueRequestPayload = Array.Empty<byte>();
                _lastPortalSessionValueRequestSummary = "Portal session-value request was not dispatched because the key was empty.";
                _lastPortalSessionValueRequestSentTick = int.MinValue;
                return false;
            }

            _lastPortalSessionValueRequestOpcode = PortalSessionValueRequestCodec.Opcode;
            _lastPortalSessionValueRequestPayload = payload;
            _lastPortalSessionValueRequestSentTick = requestTick;
            _lastPortalSessionValueRequestSummary = DispatchPortalSessionValueRequest(payload, key?.Trim(), requestTick);
            return true;
        }

        private string DispatchPortalSessionValueRequest(byte[] payload, string key, int requestTick)
        {
            payload ??= Array.Empty<byte>();
            string source = $"Recorded CWvsContext::SendRequestSessionValue opcode {PortalSessionValueRequestCodec.Opcode} for key '{key}' with reset={PortalSessionValueRequestCodec.RequestResetFlag} at tick {requestTick}.";

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

        private void PrunePendingPortalSessionValueImpacts(int currentMapId)
        {
            MapleLib.WzLib.WzStructure.MapInfo mapInfo = _mapBoard?.MapInfo;
            bool partyRaidRuntimeActive = _specialFieldRuntime?.PartyRaid.IsActive == true;
            _pendingPortalSessionValueImpacts.RemoveAll(entry =>
                entry?.IsValid != true
                || entry.MapId != currentMapId
                || !IsPendingPortalSessionValueImpactOwnerStillAdmitted(entry.OwnerKind, mapInfo, partyRaidRuntimeActive));
        }

        internal static bool IsPendingPortalSessionValueImpactOwnerStillAdmitted(
            PortalSessionValueImpactOwnerKind ownerKind,
            MapleLib.WzLib.WzStructure.MapInfo mapInfo,
            bool partyRaidRuntimeActive)
        {
            return ownerKind switch
            {
                PortalSessionValueImpactOwnerKind.UnresolvedSessionValue => true,
                PortalSessionValueImpactOwnerKind.PartyRaid => partyRaidRuntimeActive
                    && PartyRaidField.IsPartyRaidMap(mapInfo),
                PortalSessionValueImpactOwnerKind.HuntingAdBalloon =>
                    mapInfo?.fieldType == MapleLib.WzLib.WzStructure.Data.FieldType.FIELDTYPE_HUNTINGADBALLOON,
                PortalSessionValueImpactOwnerKind.ChaosZakum => IsChaosZakumPortalSessionWrapperMap(mapInfo),
                _ => false
            };
        }

        private void ClearPendingPortalSessionValueImpacts()
        {
            _pendingPortalSessionValueImpacts.Clear();
            _lastPortalSessionValueRequestSentTick = int.MinValue;
        }

        private void ConsumePendingPortalSessionValueImpactsFromTransferLifecycle()
        {
            if (!ShouldConsumePendingPortalSessionValueImpactsForTransferLifecycle(
                    _pendingPortalSessionValueImpacts.Count,
                    _lastPortalSessionValueRequestSentTick))
            {
                return;
            }

            ClearPendingPortalSessionValueImpacts();
        }

        internal static bool ShouldConsumePendingPortalSessionValueImpactsForTransferLifecycle(
            int pendingImpactCount,
            int lastRequestSentTick)
        {
            return pendingImpactCount > 0
                || lastRequestSentTick != int.MinValue;
        }

        private bool TryApplyPendingPortalSessionValueImpact(
            string key,
            string value,
            int packetType,
            PacketFieldSpecificDataOwnerHint ownerHint)
        {
            int currentMapId = _mapBoard?.MapInfo?.id ?? -1;
            PrunePendingPortalSessionValueImpacts(currentMapId);
            if (_pendingPortalSessionValueImpacts.Count == 0)
            {
                return false;
            }

            PortalSessionValueImpactIngress ingress = new(key, value, packetType, ownerHint);
            int pendingIndex = -1;
            PendingPortalSessionValueImpact pendingImpact = null;
            for (int i = _pendingPortalSessionValueImpacts.Count - 1; i >= 0; i--)
            {
                PendingPortalSessionValueImpact candidate = _pendingPortalSessionValueImpacts[i];
                if (!candidate.Matches(currentMapId, ingress))
                {
                    continue;
                }

                pendingIndex = i;
                pendingImpact = candidate;
                break;
            }

            if (pendingIndex < 0 || pendingImpact?.IsValid != true)
            {
                return false;
            }

            PlayerCharacter player = _playerManager?.Player;
            if (player?.Physics == null)
            {
                return false;
            }

            if (!TryApplyCollisionCustomImpactToPlayer(
                    player,
                    pendingImpact.VelocityX,
                    pendingImpact.VelocityY,
                    currTickCount))
            {
                return false;
            }

            _pendingPortalSessionValueImpacts.RemoveAt(pendingIndex);
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
            int currentMapId = _mapBoard?.MapInfo?.id ?? -1;
            PrunePendingPortalSessionValueImpacts(currentMapId);
            if (_pendingPortalSessionValueImpacts.Count == 0
                || !TryDecodePortalSessionValueImpactPacketPairs(packetType, payload, out IReadOnlyList<PortalSessionValueImpactIngress> pairs, out _))
            {
                return false;
            }

            List<string> releasedPairs = null;
            foreach (PortalSessionValueImpactIngress pair in pairs)
            {
                while (TryApplyPendingPortalSessionValueImpact(pair.Key, pair.Value, pair.PacketType, pair.OwnerHint))
                {
                    releasedPairs ??= new List<string>();
                    releasedPairs.Add($"{pair.Key}={pair.Value}");
                }
            }

            if (releasedPairs == null || releasedPairs.Count == 0)
            {
                return false;
            }

            message = releasedPairs.Count == 1
                ? $"Released pending portal session-value impact from packet {packetType} for {releasedPairs[0]}."
                : $"Released {releasedPairs.Count} pending portal session-value impacts from packet {packetType}: {string.Join(", ", releasedPairs.Take(4))}.";
            return true;
        }

        internal static bool TryDecodePortalSessionValueImpactPacketPairs(
            int packetType,
            byte[] payload,
            out IReadOnlyList<PortalSessionValueImpactIngress> pairs,
            out string error)
        {
            pairs = Array.Empty<PortalSessionValueImpactIngress>();
            error = null;
            payload ??= Array.Empty<byte>();

            if (!TryUnwrapCurrentWrapperRelayPacketChain(packetType, payload, out packetType, out payload, out string relayEvidence, out string relayError))
            {
                error = relayError;
                return false;
            }

            if (packetType == PartyRaidField.ClientSessionValuePacketType
                || packetType == PartyRaidField.ClientFieldSetVariablePacketType)
            {
                if (!TryDecodeMapleStringPairPayload(payload, out string key, out string value, out error))
                {
                    return false;
                }

                PacketFieldSpecificDataOwnerHint ownerHint = packetType == PartyRaidField.ClientFieldSetVariablePacketType
                    ? PacketFieldSpecificDataOwnerHint.Field
                    : PacketFieldSpecificDataOwnerHint.Session;
                pairs = new[] { new PortalSessionValueImpactIngress(key, value, packetType, ownerHint) };
                if (!string.IsNullOrWhiteSpace(relayEvidence))
                {
                    error = relayEvidence;
                }

                return true;
            }

            if (packetType == 149)
            {
                // Try wrapper relay first so field-specific payloads that coincidentally parse as string pairs
                // still honor the active wrapper owner seam before generic pair fallback.
                if (TryDecodeFieldSpecificRelayPacketPairs(payload, out pairs, out string relayMessage))
                {
                    error = string.IsNullOrWhiteSpace(relayEvidence)
                        ? relayMessage
                        : $"{relayEvidence}. {relayMessage}";
                    return true;
                }

                if (!PacketFieldSpecificDataCodec.TryDecodeStringPairs(payload, out IReadOnlyList<KeyValuePair<string, string>> decodedPairs, out int headerSize))
                {
                    error = "Portal session-value impact packet did not decode into Maple string pairs.";
                    return false;
                }

                if (!TryNormalizePortalSessionValueImpactPairs(packetType, decodedPairs, out pairs))
                {
                    error = "Portal session-value impact packet did not contain any usable session or field key/value pairs.";
                    return false;
                }

                error = string.IsNullOrWhiteSpace(relayEvidence)
                    ? $"decoded field-specific packet header size {headerSize}"
                    : $"{relayEvidence}. decoded field-specific packet header size {headerSize}";
                return true;
            }

            error = $"Packet type {packetType} does not carry portal session-value impact pairs.";
            return false;
        }

        private static bool TryDecodeFieldSpecificRelayPacketPairs(
            byte[] payload,
            out IReadOnlyList<PortalSessionValueImpactIngress> pairs,
            out string error)
        {
            pairs = Array.Empty<PortalSessionValueImpactIngress>();
            error = null;
            if (!SpecialFieldRuntimeCoordinator.TryDecodeCurrentWrapperRelayPayload(
                    payload,
                    out int relayedPacketType,
                    out byte[] relayedPayload,
                    out _))
            {
                return false;
            }

            if (!TryDecodePortalSessionValueImpactPacketPairs(
                    relayedPacketType,
                    relayedPayload,
                    out pairs,
                    out string relayedError))
            {
                error =
                    $"Portal session-value impact field-specific relay packet {relayedPacketType} did not decode into usable key/value pairs. {relayedError}";
                return false;
            }

            error = $"decoded field-specific relay packet {relayedPacketType}. {relayedError}";
            return true;
        }

        private static bool TryUnwrapCurrentWrapperRelayPacketChain(
            int packetType,
            byte[] payload,
            out int unwrappedPacketType,
            out byte[] unwrappedPayload,
            out string relayEvidence,
            out string error)
        {
            unwrappedPacketType = packetType;
            unwrappedPayload = payload ?? Array.Empty<byte>();
            relayEvidence = string.Empty;
            error = null;
            if (unwrappedPacketType != SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
            {
                return true;
            }

            List<int> relayPacketTypes = new();
            const int maxNestedRelayDepth = 8;
            for (int depth = 0; depth < maxNestedRelayDepth; depth++)
            {
                if (!SpecialFieldRuntimeCoordinator.TryDecodeCurrentWrapperRelayPayload(
                        unwrappedPayload,
                        out int relayedPacketType,
                        out byte[] relayedPayload,
                        out string relayDecodeError))
                {
                    error = relayDecodeError;
                    return false;
                }

                relayPacketTypes.Add(relayedPacketType);
                unwrappedPacketType = relayedPacketType;
                unwrappedPayload = relayedPayload;
                if (unwrappedPacketType != SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
                {
                    relayEvidence = $"decoded relay packet-id prefixes {string.Join("->", relayPacketTypes)}";
                    return true;
                }
            }

            relayEvidence = $"decoded relay packet-id prefixes {string.Join("->", relayPacketTypes)}";
            error =
                $"Portal session-value impact relay decode exceeded bounded depth while unwrapping nested packet id {SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode}. {relayEvidence}.";
            return false;
        }

        private static bool TryNormalizePortalSessionValueImpactPairs(
            int packetType,
            IReadOnlyList<KeyValuePair<string, string>> pairs,
            out IReadOnlyList<PortalSessionValueImpactIngress> normalizedPairs)
        {
            normalizedPairs = Array.Empty<PortalSessionValueImpactIngress>();
            if (pairs == null || pairs.Count == 0)
            {
                return false;
            }

            List<PortalSessionValueImpactIngress> normalized = new(pairs.Count);
            foreach (KeyValuePair<string, string> pair in pairs)
            {
                string key = pair.Key;
                PacketFieldSpecificDataOwnerHint ownerHint = PacketFieldSpecificDataCodec.ResolveOwnerHint(ref key);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                normalized.Add(new PortalSessionValueImpactIngress(key, pair.Value, packetType, ownerHint));
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
            QuestDetailDeliveryType deliveryTypeOverride = ResolvePacketOwnedQuestDeliveryTypeHint(questId);
            QuestWindowDetailState state = deliveryTypeOverride != QuestDetailDeliveryType.None
                ? _questRuntime.GetQuestWindowDetailState(questId, _playerManager?.Player?.Build, deliveryTypeOverride)
                : _questRuntime.GetQuestWindowDetailState(questId, _playerManager?.Player?.Build);
            state = ApplyPacketOwnedDeliveryDisallowParity(state);
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
