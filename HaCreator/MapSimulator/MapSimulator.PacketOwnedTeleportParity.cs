using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int PacketOwnedTeleportPortalRequestOpcode = 113;
        private const int PacketOwnedTeleportForcedMovePathAttribute = 4;
        private const byte PacketOwnedTeleportSyntheticFieldKey = 0;
        private const string PacketOwnedTeleportGeneralEffectImageName = "BasicEff.img";
        private const string PacketOwnedTeleportGeneralEffectPath = "Teleport";
        private const float PacketOwnedTeleportPortalExactTolerance = 0.5f;
        private const float PacketOwnedTeleportPortalNearestTolerance = 12f;

        private bool TryApplyPacketOwnedTeleportResult(float targetX, float targetY, out string message)
        {
            return TryApplyPacketOwnedTeleportResult(
                targetX,
                targetY,
                sourcePortalName: null,
                targetPortalName: null,
                targetPortalNameCandidates: null,
                out message);
        }

        private bool TryApplyPacketOwnedTeleportResult(
            float targetX,
            float targetY,
            string sourcePortalName,
            string targetPortalName,
            IEnumerable<string> targetPortalNameCandidates,
            out string message)
        {
            _packetOwnedTeleportRequestActive = false;
            int currentTime = Environment.TickCount;
            _packetOwnedTeleportRequestCompletedAt = currentTime;

            if (TryResolvePacketOwnedTeleportPortalByPosition(targetX, targetY, out int portalIndex, out PortalInstance portalInstance))
            {
                _lastPacketOwnedTeleportPortalIndex = portalIndex;
                RegisterPacketOwnedTeleportHandoff(portalInstance);
                string resolvedRegistrationMessage = ApplyPacketOwnedTeleportRegistrationSideEffects(portalInstance, currentTime);
                ApplySameMapTeleportPosition(portalInstance.X, portalInstance.Y);
                message = string.IsNullOrWhiteSpace(resolvedRegistrationMessage)
                    ? $"Applied packet-owned teleport result through resolved portal index {portalIndex} ({portalInstance.pn} -> {portalInstance.tn})."
                    : $"Applied packet-owned teleport result through resolved portal index {portalIndex} ({portalInstance.pn} -> {portalInstance.tn}). {resolvedRegistrationMessage}";
                return true;
            }

            _lastPacketOwnedTeleportPortalIndex = -1;
            RegisterPacketOwnedTeleportHandoff(
                sourcePortalName,
                ResolvePacketOwnedTeleportFallbackHandoffTargetPortalName(targetPortalName, targetPortalNameCandidates));
            string registrationMessage = ApplyPacketOwnedTeleportRegistrationSideEffects(targetX, targetY, currentTime);
            ApplySameMapTeleportPosition(targetX, targetY);
            message = string.IsNullOrWhiteSpace(registrationMessage)
                ? $"Applied packet-owned teleport result to exact coordinates ({targetX}, {targetY})."
                : $"Applied packet-owned teleport result to exact coordinates ({targetX}, {targetY}). {registrationMessage}";
            return true;
        }

        private bool TryApplyPacketOwnedTeleportResult(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < 2)
            {
                message = "Teleport-result payload must contain success and portal index bytes.";
                return false;
            }

            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
            bool succeeded = reader.ReadByte() != 0;
            int portalIndex = reader.ReadByte();
            return TryApplyPacketOwnedTeleportResult(succeeded, portalIndex, out message);
        }

        private bool TryApplyPacketOwnedTeleportResult(bool succeeded, int portalIndex, out string message)
        {
            _lastPacketOwnedTeleportPortalIndex = portalIndex;

            if (!succeeded)
            {
                _packetOwnedTeleportRequestActive = false;
                message = $"Packet-owned teleport result rejected portal index {portalIndex}.";
                return true;
            }

            _packetOwnedTeleportRequestActive = false;
            int currentTime = Environment.TickCount;
            _packetOwnedTeleportRequestCompletedAt = currentTime;

            PortalItem portal = _portalPool?.GetPortal(portalIndex);
            PortalInstance portalInstance = portal?.PortalInstance;
            if (portalInstance == null)
            {
                message = $"Packet-owned teleport result returned portal index {portalIndex}, but the current portal list could not resolve it.";
                return false;
            }

            RegisterPacketOwnedTeleportHandoff(portalInstance);
            string registrationMessage = ApplyPacketOwnedTeleportRegistrationSideEffects(portalInstance, currentTime);
            ApplySameMapTeleportPosition(portalInstance.X, portalInstance.Y);
            message = string.IsNullOrWhiteSpace(registrationMessage)
                ? $"Applied packet-owned teleport result for portal index {portalIndex} ({portalInstance.pn} -> {portalInstance.tn})."
                : $"Applied packet-owned teleport result for portal index {portalIndex} ({portalInstance.pn} -> {portalInstance.tn}). {registrationMessage}";
            return true;
        }

        private bool TryFinalizePendingCrossMapTeleport(PendingCrossMapTeleportTarget target, out string message)
        {
            message = null;
            if (target == null)
            {
                return false;
            }

            if ((_mapBoard?.MapInfo?.id ?? -1) != target.MapId)
            {
                message = $"Pending packet-owned cross-map teleport expected map {target.MapId}, but the current field differs.";
                return false;
            }

            if (TryResolvePendingCrossMapTeleportPortalIndex(target, out int portalIndex))
            {
                if (_portalPool.GetPortal(portalIndex)?.PortalInstance != null)
                {
                    _packetOwnedTeleportRequestActive = true;
                    return TryApplyPacketOwnedTeleportResult(succeeded: true, portalIndex, out message);
                }
            }

            if (target.HasFallbackCoordinates)
            {
                if (TryResolvePacketOwnedTeleportPortalByPosition(
                    target.FallbackX.Value,
                    target.FallbackY.Value,
                    out int resolvedPortalIndex,
                    out _))
                {
                    _packetOwnedTeleportRequestActive = true;
                    return TryApplyPacketOwnedTeleportResult(succeeded: true, resolvedPortalIndex, out message);
                }

                _packetOwnedTeleportRequestActive = true;
                return TryApplyPacketOwnedTeleportResult(
                    target.FallbackX.Value,
                    target.FallbackY.Value,
                    target.SourcePortalName,
                    target.TargetPortalName,
                    target.TargetPortalNameCandidates,
                    out message);
            }

            _packetOwnedTeleportRequestActive = false;
            message = string.IsNullOrWhiteSpace(target.TargetPortalName)
                ? $"Packet-owned cross-map teleport could not resolve a landing point in map {target.MapId}."
                : $"Packet-owned cross-map teleport could not resolve portal '{target.TargetPortalName}' in map {target.MapId}.";
            return false;
        }

        private void RegisterPacketOwnedTeleportHandoff(PortalInstance portalInstance)
        {
            RegisterPacketOwnedTeleportHandoff(portalInstance?.pn, portalInstance?.tn);
        }

        private void RegisterPacketOwnedTeleportHandoff(string sourcePortalName, string targetPortalName)
        {
            _lastPacketOwnedTeleportSourcePortalName = sourcePortalName;
            _lastPacketOwnedTeleportTargetPortalName = targetPortalName;
        }

        private void RecordPacketOwnedTeleportPortalRequest(PortalInstance sourcePortal)
        {
            if (sourcePortal == null)
            {
                ResetPacketOwnedTeleportOutboundRequest("Teleport request metadata could not be recorded because the source portal was missing.");
                return;
            }

            _lastPacketOwnedTeleportPortalRequestTick = Environment.TickCount;
            _lastPacketOwnedTeleportSourcePortalName = sourcePortal.pn;
            _lastPacketOwnedTeleportTargetPortalName = sourcePortal.tn;

            if (TryBuildPacketOwnedTeleportPortalRequest(sourcePortal, out byte[] payload, out string summary))
            {
                StorePacketOwnedTeleportOutboundRequest(payload, summary);
                return;
            }

            ResetPacketOwnedTeleportOutboundRequest(summary, sourcePortal.pn, sourcePortal.tn, stampTick: false);
        }

        private void RecordPacketOwnedTeleportPortalRequest(
            string sourcePortalName,
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            string targetPortalName,
            string targetResolutionSummary)
        {
            _lastPacketOwnedTeleportPortalRequestTick = Environment.TickCount;
            _lastPacketOwnedTeleportSourcePortalName = sourcePortalName;
            _lastPacketOwnedTeleportTargetPortalName = targetPortalName;

            if (TryBuildPacketOwnedTeleportPortalRequest(
                sourcePortalName,
                sourceX,
                sourceY,
                targetX,
                targetY,
                targetPortalName,
                targetResolutionSummary,
                out byte[] payload,
                out string summary))
            {
                StorePacketOwnedTeleportOutboundRequest(payload, summary);
                return;
            }

            ResetPacketOwnedTeleportOutboundRequest(summary, sourcePortalName, targetPortalName, stampTick: false);
        }

        private void ResetPacketOwnedTeleportOutboundRequest(
            string summary,
            string sourcePortalName = null,
            string targetPortalName = null,
            bool stampTick = true)
        {
            if (stampTick)
            {
                _lastPacketOwnedTeleportPortalRequestTick = Environment.TickCount;
            }

            _lastPacketOwnedTeleportSourcePortalName = sourcePortalName;
            _lastPacketOwnedTeleportTargetPortalName = targetPortalName;
            _lastPacketOwnedTeleportOutboundOpcode = -1;
            _lastPacketOwnedTeleportOutboundPayload = Array.Empty<byte>();
            _lastPacketOwnedTeleportOutboundSummary = summary;
        }

        private void StorePacketOwnedTeleportOutboundRequest(byte[] payload, string summary)
        {
            _lastPacketOwnedTeleportOutboundOpcode = PacketOwnedTeleportPortalRequestOpcode;
            _lastPacketOwnedTeleportOutboundPayload = payload ?? Array.Empty<byte>();
            _lastPacketOwnedTeleportOutboundSummary = DispatchPacketOwnedTeleportPortalRequest(
                _lastPacketOwnedTeleportOutboundPayload,
                summary);
        }

        private string DispatchPacketOwnedTeleportPortalRequest(byte[] payload, string summary)
        {
            payload ??= Array.Empty<byte>();

            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(
                PacketOwnedTeleportPortalRequestOpcode,
                payload,
                out string bridgeStatus))
            {
                return $"{summary} Dispatched it through the live official-session bridge. {bridgeStatus}";
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(
                PacketOwnedTeleportPortalRequestOpcode,
                payload,
                out string outboxStatus))
            {
                return $"{summary} Dispatched it through the generic packet outbox after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
            }

            if (_localUtilityOfficialSessionBridge.IsRunning
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(
                    PacketOwnedTeleportPortalRequestOpcode,
                    payload,
                    out string queuedBridgeStatus))
            {
                return $"{summary} Queued it for deferred official-session injection after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {queuedBridgeStatus}";
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(
                PacketOwnedTeleportPortalRequestOpcode,
                payload,
                out string queuedOutboxStatus))
            {
                return $"{summary} Queued it for deferred generic packet outbox delivery after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred outbox: {queuedOutboxStatus}";
            }

            return $"{summary} The request remained simulator-owned because neither the live bridge nor the packet outbox accepted opcode {PacketOwnedTeleportPortalRequestOpcode}. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
        }

        private bool IsPacketOwnedTeleportRegistrationCoolingDown(int currentTime)
        {
            if (_packetOwnedTeleportRequestCompletedAt == int.MinValue)
            {
                return false;
            }

            int elapsed = Math.Max(0, unchecked(currentTime - _packetOwnedTeleportRequestCompletedAt));
            return elapsed < PACKET_OWNED_TELEPORT_REGISTRATION_COOLDOWN_MS;
        }

        private string ApplyPacketOwnedTeleportRegistrationSideEffects(PortalInstance portalInstance, int currentTime)
        {
            if (portalInstance == null)
            {
                return null;
            }

            return ApplyPacketOwnedTeleportRegistrationSideEffects(portalInstance.X, portalInstance.Y, currentTime);
        }

        private string ApplyPacketOwnedTeleportRegistrationSideEffects(float targetX, float targetY, int currentTime)
        {
            // `CUserLocal::OnTeleport` re-enters `TryRegisterTeleport(..., bForced = 1)`,
            // so the packet-owned apply seam owns the post-ack portal cooldown and the
            // forced registration side effects (effect, move-path attr, set-item background,
            // and passenger cleanup).
            _packetOwnedTeleportRequestCompletedAt = currentTime;
            _lastPacketOwnedTeleportRegistrationTick = currentTime;
            _lastPacketOwnedTeleportMovePathAttribute = PacketOwnedTeleportForcedMovePathAttribute;
            _lastPacketOwnedTeleportSetItemBackgroundActive = true;
            _playerManager?.ForceStand();

            bool effectShown = TryShowPacketOwnedTeleportGeneralEffect(targetX, targetY, currentTime);
            string detachedPassengerMessage = ClearPacketOwnedTeleportPassengerLink();
            var details = new List<string>(3)
            {
                $"Replayed forced teleport registration with move-path attribute {PacketOwnedTeleportForcedMovePathAttribute} and set-item background (1, 1)."
            };
            details.Add(effectShown
                ? $"Played WZ teleport effect {PacketOwnedTeleportGeneralEffectImageName}/{PacketOwnedTeleportGeneralEffectPath}."
                : $"Teleport effect {PacketOwnedTeleportGeneralEffectImageName}/{PacketOwnedTeleportGeneralEffectPath} could not be shown.");
            if (!string.IsNullOrWhiteSpace(detachedPassengerMessage))
            {
                details.Add(detachedPassengerMessage);
            }

            return string.Join(" ", details);
        }

        private string ClearPacketOwnedTeleportPassengerLink()
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player?.Build == null)
            {
                return null;
            }

            int passengerId = _localFollowRuntime.AttachedPassengerId;
            if (passengerId <= 0)
            {
                return null;
            }

            TryResolvePacketOwnedRemoteCharacterSnapshot(passengerId, out LocalFollowUserSnapshot passenger);
            string detachMessage = _localFollowRuntime.ClearAttachedPassenger(
                passenger.Exists
                    ? passenger
                    : LocalFollowUserSnapshot.Missing(passengerId, ResolvePacketOwnedRemoteCharacterName(passengerId)),
                transferField: false,
                transferPosition: null);
            _remoteUserPool?.TryApplyFollowCharacter(
                passengerId,
                driverId: 0,
                transferField: false,
                transferPosition: null,
                localCharacterId: player.Build.Id,
                localCharacterPosition: new Vector2(player.X, player.Y),
                out _);
            return detachMessage;
        }

        private bool TryResolvePacketOwnedTeleportPortalByPosition(float targetX, float targetY, out int portalIndex, out PortalInstance portalInstance)
        {
            return TryResolvePacketOwnedTeleportPortalByPosition(_portalPool, targetX, targetY, out portalIndex, out portalInstance);
        }

        private bool TryResolvePendingCrossMapTeleportPortalIndex(PendingCrossMapTeleportTarget target, out int portalIndex)
        {
            portalIndex = -1;
            if (target == null || _portalPool == null)
            {
                return false;
            }

            if (TryResolvePacketOwnedTeleportPortalIndexByName(_portalPool, target.TargetPortalName, out portalIndex))
            {
                return true;
            }

            string[] candidateNames = target.TargetPortalNameCandidates;
            if (candidateNames != null)
            {
                foreach (string candidateName in candidateNames)
                {
                    if (TryResolvePacketOwnedTeleportPortalIndexByName(_portalPool, candidateName, out portalIndex))
                    {
                        return true;
                    }
                }
            }

            if (target.HasFallbackCoordinates
                && TryResolvePacketOwnedTeleportPortalByPosition(
                    target.FallbackX.Value,
                    target.FallbackY.Value,
                    out portalIndex,
                    out _))
            {
                return true;
            }

            return false;
        }

        private static bool TryResolvePacketOwnedTeleportPortalIndexByName(PortalPool portalPool, string portalName, out int portalIndex)
        {
            portalIndex = -1;
            if (portalPool == null || string.IsNullOrWhiteSpace(portalName))
            {
                return false;
            }

            portalIndex = portalPool.GetPortalIndexByName(portalName);
            return portalPool.GetPortal(portalIndex)?.PortalInstance != null;
        }

        private static bool TryResolvePacketOwnedTeleportPortalByPosition(
            PortalPool portalPool,
            float targetX,
            float targetY,
            out int portalIndex,
            out PortalInstance portalInstance)
        {
            portalIndex = -1;
            portalInstance = null;
            if (portalPool == null)
            {
                return false;
            }

            for (int i = 0; i < portalPool.PortalCount; i++)
            {
                PortalItem portal = portalPool.GetPortal(i);
                PortalInstance instance = portal?.PortalInstance;
                if (instance == null)
                {
                    continue;
                }

                if (Math.Abs(instance.X - targetX) > PacketOwnedTeleportPortalExactTolerance
                    || Math.Abs(instance.Y - targetY) > PacketOwnedTeleportPortalExactTolerance)
                {
                    continue;
                }

                portalIndex = i;
                portalInstance = instance;
                return true;
            }

            float bestDistanceSquared = float.MaxValue;
            bool ambiguous = false;
            for (int i = 0; i < portalPool.PortalCount; i++)
            {
                PortalItem portal = portalPool.GetPortal(i);
                PortalInstance instance = portal?.PortalInstance;
                if (instance == null)
                {
                    continue;
                }

                float dx = instance.X - targetX;
                float dy = instance.Y - targetY;
                if (Math.Abs(dx) > PacketOwnedTeleportPortalNearestTolerance
                    || Math.Abs(dy) > PacketOwnedTeleportPortalNearestTolerance)
                {
                    continue;
                }

                float distanceSquared = dx * dx + dy * dy;
                if (distanceSquared + 0.01f < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    portalIndex = i;
                    portalInstance = instance;
                    ambiguous = false;
                }
                else if (Math.Abs(distanceSquared - bestDistanceSquared) <= 0.01f)
                {
                    ambiguous = true;
                }
            }

            if (portalInstance != null && !ambiguous)
            {
                return true;
            }

            portalIndex = -1;
            portalInstance = null;
            return false;
        }

        private bool TryResolvePacketOwnedTargetPortalNameByPosition(int targetMapId, float targetX, float targetY, out string targetPortalName)
        {
            targetPortalName = null;
            return TryResolvePacketOwnedTargetPortalMetadata(
                targetMapId,
                targetX,
                targetY,
                _mapBoard?.MapInfo?.id ?? -1,
                null,
                out targetPortalName,
                out _,
                out _);
        }

        internal static bool TryResolvePacketOwnedSourcePortalNameByTargetPortalName(
            IEnumerable<PortalInstance> currentFieldPortals,
            int targetMapId,
            string targetPortalName,
            out string sourcePortalName)
        {
            sourcePortalName = null;
            if (currentFieldPortals == null
                || targetMapId <= 0
                || string.IsNullOrWhiteSpace(targetPortalName))
            {
                return false;
            }

            PortalInstance firstMatch = null;
            foreach (PortalInstance portal in currentFieldPortals)
            {
                if (portal == null
                    || portal.tm != targetMapId
                    || string.IsNullOrWhiteSpace(portal.pn)
                    || !string.Equals(portal.tn, targetPortalName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (firstMatch == null)
                {
                    firstMatch = portal;
                    continue;
                }

                if (!string.Equals(firstMatch.pn, portal.pn, StringComparison.OrdinalIgnoreCase))
                {
                    sourcePortalName = null;
                    return false;
                }
            }

            sourcePortalName = firstMatch?.pn;
            return !string.IsNullOrWhiteSpace(sourcePortalName);
        }

        internal static bool TryResolvePacketOwnedSourcePortalNameByTargetPortalCandidates(
            IEnumerable<PortalInstance> currentFieldPortals,
            int targetMapId,
            IEnumerable<string> targetPortalNames,
            out string sourcePortalName)
        {
            sourcePortalName = null;
            if (currentFieldPortals == null || targetMapId <= 0 || targetPortalNames == null)
            {
                return false;
            }

            string resolvedSourcePortalName = null;
            foreach (string targetPortalName in targetPortalNames)
            {
                if (!TryResolvePacketOwnedSourcePortalNameByTargetPortalName(
                    currentFieldPortals,
                    targetMapId,
                    targetPortalName,
                    out string candidateSourcePortalName))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(resolvedSourcePortalName))
                {
                    resolvedSourcePortalName = candidateSourcePortalName;
                    continue;
                }

                if (!string.Equals(resolvedSourcePortalName, candidateSourcePortalName, StringComparison.OrdinalIgnoreCase))
                {
                    sourcePortalName = null;
                    return false;
                }
            }

            sourcePortalName = resolvedSourcePortalName;
            return !string.IsNullOrWhiteSpace(sourcePortalName);
        }

        private bool TryResolvePacketOwnedPendingCrossMapPortalNames(
            PortalInstance sourcePortal,
            out string targetPortalName,
            out string[] candidatePortalNames)
        {
            targetPortalName = sourcePortal?.tn;
            candidatePortalNames = Array.Empty<string>();
            if (sourcePortal == null)
            {
                return false;
            }

            var candidates = new List<string>();
            AddPacketOwnedTeleportCandidateName(candidates, sourcePortal.tn);

            int currentMapId = _mapBoard?.MapInfo?.id ?? -1;
            if (sourcePortal.tm > 0
                && sourcePortal.tm != MapConstants.MaxMap
                && sourcePortal.tm != currentMapId
                && _loadMapCallback != null)
            {
                Tuple<Board, string> targetMap = _loadMapCallback(sourcePortal.tm);
                IEnumerable<PortalInstance> targetPortals = targetMap?.Item1?.BoardItems?.Portals;
                if (TryResolvePacketOwnedPortalRequestTargetByReciprocalLink(
                    targetPortals,
                    currentMapId,
                    sourcePortal.pn,
                    out PortalInstance reciprocalPortal))
                {
                    AddPacketOwnedTeleportCandidateName(candidates, reciprocalPortal.pn);
                    targetPortalName ??= reciprocalPortal.pn;
                }

                if (TryResolvePacketOwnedPortalRequestTargetByMapOnlyReturn(
                    targetPortals,
                    currentMapId,
                    out PortalInstance mapOnlyPortal))
                {
                    AddPacketOwnedTeleportCandidateName(candidates, mapOnlyPortal.pn);
                    targetPortalName ??= mapOnlyPortal.pn;
                }
            }

            candidatePortalNames = candidates.ToArray();
            return !string.IsNullOrWhiteSpace(targetPortalName) || candidatePortalNames.Length > 0;
        }

        private bool TryResolvePacketOwnedTargetPortalMetadata(
            int targetMapId,
            float targetX,
            float targetY,
            int currentMapId,
            string sourcePortalName,
            out string targetPortalName,
            out string resolutionSummary,
            out string[] candidatePortalNames)
        {
            targetPortalName = null;
            resolutionSummary = "temporary portal destination coordinates";
            candidatePortalNames = Array.Empty<string>();
            if (targetMapId <= 0
                || targetMapId == MapConstants.MaxMap
                || _loadMapCallback == null)
            {
                return false;
            }

            Tuple<Board, string> targetMap = _loadMapCallback(targetMapId);
            IEnumerable<PortalInstance> targetPortals = targetMap?.Item1?.BoardItems?.Portals;
            var candidateNames = new List<string>();
            if (TryResolvePacketOwnedTeleportPortalByPosition(targetPortals, targetX, targetY, out PortalInstance exactPortal))
            {
                AddPacketOwnedTeleportCandidateName(candidateNames, exactPortal.pn);
                targetPortalName = exactPortal.pn;
                resolutionSummary = $"target-map portal '{targetPortalName}' in map {targetMapId}";
                candidatePortalNames = candidateNames.ToArray();
                return true;
            }

            if (TryResolvePacketOwnedPortalRequestTargetByReciprocalLink(
                targetPortals,
                currentMapId,
                sourcePortalName,
                out PortalInstance reciprocalPortal))
            {
                AddPacketOwnedTeleportCandidateName(candidateNames, reciprocalPortal.pn);
                targetPortalName ??= reciprocalPortal.pn;
                resolutionSummary = $"reciprocal target-map portal '{reciprocalPortal.pn}' linked back to source portal '{sourcePortalName}'";
            }

            if (TryResolvePacketOwnedPortalRequestTargetByMapOnlyReturn(
                targetPortals,
                currentMapId,
                out PortalInstance mapOnlyPortal))
            {
                AddPacketOwnedTeleportCandidateName(candidateNames, mapOnlyPortal.pn);
                targetPortalName ??= mapOnlyPortal.pn;
                resolutionSummary = $"single target-map portal '{mapOnlyPortal.pn}' that returns to map {currentMapId}";
            }

            candidatePortalNames = candidateNames.ToArray();
            return !string.IsNullOrWhiteSpace(targetPortalName);
        }

        private static void AddPacketOwnedTeleportCandidateName(List<string> candidates, string portalName)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(portalName))
            {
                return;
            }

            foreach (string existing in candidates)
            {
                if (string.Equals(existing, portalName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            candidates.Add(portalName);
        }

        internal static string ResolvePacketOwnedTeleportFallbackHandoffTargetPortalNameForTesting(
            string targetPortalName,
            IEnumerable<string> targetPortalNameCandidates)
        {
            return ResolvePacketOwnedTeleportFallbackHandoffTargetPortalName(targetPortalName, targetPortalNameCandidates);
        }

        private static string ResolvePacketOwnedTeleportFallbackHandoffTargetPortalName(
            string targetPortalName,
            IEnumerable<string> targetPortalNameCandidates)
        {
            if (!string.IsNullOrWhiteSpace(targetPortalName))
            {
                return targetPortalName;
            }

            if (targetPortalNameCandidates == null)
            {
                return null;
            }

            foreach (string candidateName in targetPortalNameCandidates)
            {
                if (!string.IsNullOrWhiteSpace(candidateName))
                {
                    return candidateName;
                }
            }

            return null;
        }

        private static bool TryResolvePacketOwnedTeleportPortalByPosition(
            IEnumerable<PortalInstance> portals,
            float targetX,
            float targetY,
            out PortalInstance portalInstance)
        {
            portalInstance = null;
            if (portals == null)
            {
                return false;
            }

            foreach (PortalInstance portal in portals)
            {
                if (portal == null)
                {
                    continue;
                }

                if (Math.Abs(portal.X - targetX) > PacketOwnedTeleportPortalExactTolerance
                    || Math.Abs(portal.Y - targetY) > PacketOwnedTeleportPortalExactTolerance)
                {
                    continue;
                }

                portalInstance = portal;
                return true;
            }

            float bestDistanceSquared = float.MaxValue;
            bool ambiguous = false;
            foreach (PortalInstance portal in portals)
            {
                if (portal == null)
                {
                    continue;
                }

                float dx = portal.X - targetX;
                float dy = portal.Y - targetY;
                if (Math.Abs(dx) > PacketOwnedTeleportPortalNearestTolerance
                    || Math.Abs(dy) > PacketOwnedTeleportPortalNearestTolerance)
                {
                    continue;
                }

                float distanceSquared = dx * dx + dy * dy;
                if (distanceSquared + 0.01f < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    portalInstance = portal;
                    ambiguous = false;
                }
                else if (Math.Abs(distanceSquared - bestDistanceSquared) <= 0.01f)
                {
                    ambiguous = true;
                }
            }

            if (portalInstance != null && !ambiguous)
            {
                return true;
            }

            portalInstance = null;
            return false;
        }

        private void ApplySameMapTeleportPosition(float targetX, float targetY)
        {
            _playerManager?.TeleportTo(targetX, targetY);
            _playerManager?.SetSpawnPoint(targetX, targetY);

            if (_gameState.UseSmoothCamera)
            {
                _cameraController.TeleportTo(targetX, targetY);
                mapShiftX = _cameraController.MapShiftX;
                mapShiftY = _cameraController.MapShiftY;
                return;
            }

            mapShiftX = (int)MathF.Round(targetX);
            mapShiftY = (int)MathF.Round(targetY);
            SetCameraMoveX(true, false, 0);
            SetCameraMoveX(false, true, 0);
            SetCameraMoveY(true, false, 0);
            SetCameraMoveY(false, true, 0);
        }

        private bool TryShowPacketOwnedTeleportGeneralEffect(float targetX, float targetY, int currentTime)
        {
            _lastPacketOwnedTeleportEffectTick = int.MinValue;
            _lastPacketOwnedTeleportEffectPath = null;

            string cacheKey = $"teleport:{PacketOwnedTeleportGeneralEffectImageName}/{PacketOwnedTeleportGeneralEffectPath}";
            if (!TryGetOrCreatePacketOwnedAnimationFrames(
                cacheKey,
                () => LoadPacketOwnedAnimationFrames(
                    ResolvePacketOwnedPropertyPath(
                        Program.FindImage("Effect", PacketOwnedTeleportGeneralEffectImageName),
                        PacketOwnedTeleportGeneralEffectPath)),
                out List<IDXObject> frames))
            {
                return false;
            }

            _animationEffects?.AddOneTime(
                frames,
                (int)MathF.Round(targetX),
                (int)MathF.Round(targetY),
                flip: false,
                currentTime,
                zOrder: 1);
            _lastPacketOwnedTeleportEffectTick = currentTime;
            _lastPacketOwnedTeleportEffectPath = $"{PacketOwnedTeleportGeneralEffectImageName}/{PacketOwnedTeleportGeneralEffectPath}";
            return true;
        }

        private bool TryBuildPacketOwnedTeleportPortalRequest(PortalInstance sourcePortal, out byte[] payload, out string summary)
        {
            if (!TryResolvePacketOwnedPortalRequestTarget(sourcePortal, out PacketOwnedTeleportPortalRequestTarget targetPortal, out string resolutionSummary))
            {
                payload = Array.Empty<byte>();
                summary = "The simulator could not resolve destination portal coordinates for opcode 113.";
                return false;
            }

            return TryBuildPacketOwnedTeleportPortalRequest(
                sourcePortal.pn,
                sourcePortal.X,
                sourcePortal.Y,
                targetPortal.X,
                targetPortal.Y,
                sourcePortal.tn,
                resolutionSummary,
                out payload,
                out summary);
        }

        private bool TryResolvePacketOwnedPortalRequestTarget(
            PortalInstance sourcePortal,
            out PacketOwnedTeleportPortalRequestTarget target,
            out string summary)
        {
            target = default;
            summary = "no destination portal coordinates";
            if (sourcePortal == null || string.IsNullOrWhiteSpace(sourcePortal.pn))
            {
                summary = "a missing source portal name";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(sourcePortal.tn)
                && TryResolvePacketOwnedPortalRequestTargetFromBoardPortals(
                    _mapBoard?.BoardItems?.Portals,
                    sourcePortal.tn,
                    out PortalInstance currentFieldPortal))
            {
                target = new PacketOwnedTeleportPortalRequestTarget(currentFieldPortal.X, currentFieldPortal.Y);
                summary = $"current-field portal '{sourcePortal.tn}'";
                return true;
            }

            int currentMapId = _mapBoard?.MapInfo?.id ?? -1;
            if (sourcePortal.tm <= 0
                || sourcePortal.tm == MapConstants.MaxMap
                || sourcePortal.tm == currentMapId
                || _loadMapCallback == null)
            {
                summary = string.IsNullOrWhiteSpace(sourcePortal.tn)
                    ? $"destination portal in target map {sourcePortal.tm}"
                    : $"destination portal '{sourcePortal.tn}' in map {sourcePortal.tm}";
                return false;
            }

            Tuple<Board, string> targetMap = _loadMapCallback(sourcePortal.tm);
            IEnumerable<PortalInstance> targetPortals = targetMap?.Item1?.BoardItems?.Portals;
            if (!string.IsNullOrWhiteSpace(sourcePortal.tn)
                && TryResolvePacketOwnedPortalRequestTargetFromBoardPortals(
                    targetPortals,
                    sourcePortal.tn,
                    out PortalInstance crossMapPortal))
            {
                target = new PacketOwnedTeleportPortalRequestTarget(crossMapPortal.X, crossMapPortal.Y);
                summary = $"target-map portal '{sourcePortal.tn}' in map {sourcePortal.tm}";
                return true;
            }

            if (TryResolvePacketOwnedPortalRequestTargetByReciprocalLink(
                targetPortals,
                currentMapId,
                sourcePortal.pn,
                out PortalInstance reciprocalPortal))
            {
                target = new PacketOwnedTeleportPortalRequestTarget(reciprocalPortal.X, reciprocalPortal.Y);
                summary = $"reciprocal target-map portal '{reciprocalPortal.pn}' linked back to source portal '{sourcePortal.pn}'";
                return true;
            }

            if (TryResolvePacketOwnedPortalRequestTargetByMapOnlyReturn(
                targetPortals,
                currentMapId,
                out PortalInstance mapOnlyPortal))
            {
                target = new PacketOwnedTeleportPortalRequestTarget(mapOnlyPortal.X, mapOnlyPortal.Y);
                summary = $"single target-map portal '{mapOnlyPortal.pn}' that returns to map {currentMapId}";
                return true;
            }

            summary = string.IsNullOrWhiteSpace(sourcePortal.tn)
                ? $"destination portal linked to source portal '{sourcePortal.pn}' in map {sourcePortal.tm}"
                : $"destination portal '{sourcePortal.tn}' in map {sourcePortal.tm}";
            return false;
        }

        private static bool TryResolvePacketOwnedPortalRequestTargetByReciprocalLink(
            IEnumerable<PortalInstance> portals,
            int sourceMapId,
            string sourcePortalName,
            out PortalInstance portalInstance)
        {
            portalInstance = null;
            if (portals == null
                || sourceMapId <= 0
                || string.IsNullOrWhiteSpace(sourcePortalName))
            {
                return false;
            }

            foreach (PortalInstance portal in portals)
            {
                if (portal == null
                    || portal.tm != sourceMapId
                    || !string.Equals(portal.tn, sourcePortalName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                portalInstance = portal;
                return true;
            }

            return false;
        }

        private static bool TryResolvePacketOwnedPortalRequestTargetByMapOnlyReturn(
            IEnumerable<PortalInstance> portals,
            int sourceMapId,
            out PortalInstance portalInstance)
        {
            portalInstance = null;
            if (portals == null || sourceMapId <= 0)
            {
                return false;
            }

            PortalInstance firstMatch = null;
            int matchCount = 0;
            foreach (PortalInstance portal in portals)
            {
                if (portal == null || portal.tm != sourceMapId)
                {
                    continue;
                }

                firstMatch ??= portal;
                matchCount++;
                if (matchCount > 1)
                {
                    return false;
                }
            }

            portalInstance = firstMatch;
            return portalInstance != null;
        }

        private static bool TryResolvePacketOwnedPortalRequestTargetFromBoardPortals(
            IEnumerable<PortalInstance> portals,
            string targetPortalName,
            out PortalInstance portalInstance)
        {
            portalInstance = null;
            if (portals == null || string.IsNullOrWhiteSpace(targetPortalName))
            {
                return false;
            }

            foreach (PortalInstance portal in portals)
            {
                if (portal != null
                    && string.Equals(portal.pn, targetPortalName, StringComparison.OrdinalIgnoreCase))
                {
                    portalInstance = portal;
                    return true;
                }
            }

            return false;
        }

        private bool TryBuildPacketOwnedTeleportPortalRequest(
            string sourcePortalName,
            float sourceX,
            float sourceY,
            float targetX,
            float targetY,
            string targetPortalName,
            string targetResolutionSummary,
            out byte[] payload,
            out string summary)
        {
            payload = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(sourcePortalName))
            {
                summary = "Opcode 113 requires a source portal name, but none was resolved.";
                return false;
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write(PacketOwnedTeleportSyntheticFieldKey);
            WritePacketOwnedMapleString(writer, sourcePortalName);
            writer.Write((short)Math.Clamp((int)MathF.Round(sourceX), short.MinValue, short.MaxValue));
            writer.Write((short)Math.Clamp((int)MathF.Round(sourceY), short.MinValue, short.MaxValue));
            writer.Write((short)Math.Clamp((int)MathF.Round(targetX), short.MinValue, short.MaxValue));
            writer.Write((short)Math.Clamp((int)MathF.Round(targetY), short.MinValue, short.MaxValue));
            writer.Flush();

            payload = stream.ToArray();
            string targetNameToken = string.IsNullOrWhiteSpace(targetPortalName) ? "?" : targetPortalName;
            summary = $"Recorded synthetic opcode {PacketOwnedTeleportPortalRequestOpcode} portal request for {sourcePortalName}->{targetNameToken} with field key {PacketOwnedTeleportSyntheticFieldKey} using {targetResolutionSummary}.";
            return true;
        }

        private readonly struct PacketOwnedTeleportPortalRequestTarget
        {
            public PacketOwnedTeleportPortalRequestTarget(float x, float y)
            {
                X = x;
                Y = y;
            }

            public float X { get; }

            public float Y { get; }
        }
    }
}
