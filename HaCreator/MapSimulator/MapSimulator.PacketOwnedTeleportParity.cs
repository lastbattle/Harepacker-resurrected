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

        private bool TryApplyPacketOwnedTeleportResult(float targetX, float targetY, out string message)
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
            RegisterPacketOwnedTeleportHandoff(sourcePortalName: null, targetPortalName: null);
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

            if (!string.IsNullOrWhiteSpace(target.TargetPortalName) && _portalPool != null)
            {
                int portalIndex = _portalPool.GetPortalIndexByName(target.TargetPortalName);
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
                return TryApplyPacketOwnedTeleportResult(target.FallbackX.Value, target.FallbackY.Value, out message);
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
                _lastPacketOwnedTeleportOutboundOpcode = PacketOwnedTeleportPortalRequestOpcode;
                _lastPacketOwnedTeleportOutboundPayload = payload;
                _lastPacketOwnedTeleportOutboundSummary = summary;
                return;
            }

            ResetPacketOwnedTeleportOutboundRequest(summary, sourcePortal.pn, sourcePortal.tn, stampTick: false);
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

                if (Math.Abs(instance.X - targetX) > 0.5f || Math.Abs(instance.Y - targetY) > 0.5f)
                {
                    continue;
                }

                portalIndex = i;
                portalInstance = instance;
                return true;
            }

            return false;
        }

        private bool TryResolvePacketOwnedTargetPortalNameByPosition(int targetMapId, float targetX, float targetY, out string targetPortalName)
        {
            targetPortalName = null;
            if (targetMapId <= 0
                || targetMapId == MapConstants.MaxMap
                || _loadMapCallback == null)
            {
                return false;
            }

            Tuple<Board, string> targetMap = _loadMapCallback(targetMapId);
            IEnumerable<PortalInstance> targetPortals = targetMap?.Item1?.BoardItems?.Portals;
            if (!TryResolvePacketOwnedTeleportPortalByPosition(targetPortals, targetX, targetY, out PortalInstance portalInstance))
            {
                return false;
            }

            targetPortalName = portalInstance.pn;
            return !string.IsNullOrWhiteSpace(targetPortalName);
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

                if (Math.Abs(portal.X - targetX) > 0.5f || Math.Abs(portal.Y - targetY) > 0.5f)
                {
                    continue;
                }

                portalInstance = portal;
                return true;
            }

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
            payload = Array.Empty<byte>();
            summary = "The simulator could not resolve destination portal coordinates for opcode 113.";
            if (sourcePortal == null
                || string.IsNullOrWhiteSpace(sourcePortal.pn)
                || string.IsNullOrWhiteSpace(sourcePortal.tn))
            {
                summary = "Opcode 113 requires source and target portal names, but the request did not provide both values.";
                return false;
            }

            if (!TryResolvePacketOwnedPortalRequestTarget(sourcePortal, out PacketOwnedTeleportPortalRequestTarget targetPortal, out string resolutionSummary))
            {
                return false;
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write(PacketOwnedTeleportSyntheticFieldKey);
            WritePacketOwnedMapleString(writer, sourcePortal.pn);
            writer.Write((short)Math.Clamp((int)MathF.Round(sourcePortal.X), short.MinValue, short.MaxValue));
            writer.Write((short)Math.Clamp((int)MathF.Round(sourcePortal.Y), short.MinValue, short.MaxValue));
            writer.Write((short)Math.Clamp((int)MathF.Round(targetPortal.X), short.MinValue, short.MaxValue));
            writer.Write((short)Math.Clamp((int)MathF.Round(targetPortal.Y), short.MinValue, short.MaxValue));
            writer.Flush();

            payload = stream.ToArray();
            summary = $"Recorded synthetic opcode {PacketOwnedTeleportPortalRequestOpcode} portal request for {sourcePortal.pn}->{sourcePortal.tn} with field key {PacketOwnedTeleportSyntheticFieldKey} using {resolutionSummary}.";
            return true;
        }

        private bool TryResolvePacketOwnedPortalRequestTarget(
            PortalInstance sourcePortal,
            out PacketOwnedTeleportPortalRequestTarget target,
            out string summary)
        {
            target = default;
            summary = "no destination portal coordinates";
            if (sourcePortal == null || string.IsNullOrWhiteSpace(sourcePortal.tn))
            {
                summary = "a missing target portal name";
                return false;
            }

            if (TryResolvePacketOwnedPortalRequestTargetFromBoardPortals(
                _mapBoard?.BoardItems?.Portals,
                sourcePortal.tn,
                out PortalInstance currentFieldPortal))
            {
                target = new PacketOwnedTeleportPortalRequestTarget(currentFieldPortal.X, currentFieldPortal.Y);
                summary = $"current-field portal '{sourcePortal.tn}'";
                return true;
            }

            if (sourcePortal.tm <= 0
                || sourcePortal.tm == MapConstants.MaxMap
                || sourcePortal.tm == (_mapBoard?.MapInfo?.id ?? -1)
                || _loadMapCallback == null)
            {
                summary = $"destination portal '{sourcePortal.tn}' in the current or target field";
                return false;
            }

            Tuple<Board, string> targetMap = _loadMapCallback(sourcePortal.tm);
            if (TryResolvePacketOwnedPortalRequestTargetFromBoardPortals(
                targetMap?.Item1?.BoardItems?.Portals,
                sourcePortal.tn,
                out PortalInstance crossMapPortal))
            {
                target = new PacketOwnedTeleportPortalRequestTarget(crossMapPortal.X, crossMapPortal.Y);
                summary = $"target-map portal '{sourcePortal.tn}' in map {sourcePortal.tm}";
                return true;
            }

            summary = $"destination portal '{sourcePortal.tn}' in map {sourcePortal.tm}";
            return false;
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
