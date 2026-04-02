using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private bool TryApplyPacketOwnedTeleportResult(float targetX, float targetY, out string message)
        {
            _packetOwnedTeleportRequestActive = false;
            int currentTime = Environment.TickCount;
            _packetOwnedTeleportRequestCompletedAt = currentTime;

            if (TryResolvePacketOwnedTeleportPortalByPosition(targetX, targetY, out int portalIndex, out PortalInstance portalInstance))
            {
                _lastPacketOwnedTeleportPortalIndex = portalIndex;
                RegisterPacketOwnedTeleportHandoff(portalInstance);
                string registrationMessage = ApplyPacketOwnedTeleportRegistrationSideEffects(portalInstance, currentTime);
                ApplySameMapTeleportPosition(portalInstance.X, portalInstance.Y);
                message = string.IsNullOrWhiteSpace(registrationMessage)
                    ? $"Applied packet-owned teleport result through resolved portal index {portalIndex} ({portalInstance.pn} -> {portalInstance.tn})."
                    : $"Applied packet-owned teleport result through resolved portal index {portalIndex} ({portalInstance.pn} -> {portalInstance.tn}). {registrationMessage}";
                return true;
            }

            _lastPacketOwnedTeleportPortalIndex = -1;
            RegisterPacketOwnedTeleportHandoff(sourcePortalName: null, targetPortalName: null);
            ApplySameMapTeleportPosition(targetX, targetY);
            message = $"Applied packet-owned teleport result to exact coordinates ({targetX}, {targetY}).";
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
                return;
            }

            _lastPacketOwnedTeleportPortalRequestTick = Environment.TickCount;
            _lastPacketOwnedTeleportSourcePortalName = sourcePortal.pn;
            _lastPacketOwnedTeleportTargetPortalName = sourcePortal.tn;
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

            // `CUserLocal::OnTeleport` re-enters `TryRegisterTeleport(..., bForced = 1)`,
            // so the packet-owned apply seam owns the post-ack portal cooldown and passenger cleanup.
            _packetOwnedTeleportRequestCompletedAt = currentTime;
            _playerManager?.ForceStand();

            string detachedPassengerMessage = ClearPacketOwnedTeleportPassengerLink();
            return string.IsNullOrWhiteSpace(detachedPassengerMessage)
                ? null
                : detachedPassengerMessage;
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
            portalIndex = -1;
            portalInstance = null;
            if (_portalPool == null)
            {
                return false;
            }

            for (int i = 0; i < _portalPool.PortalCount; i++)
            {
                PortalItem portal = _portalPool.GetPortal(i);
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
    }
}
