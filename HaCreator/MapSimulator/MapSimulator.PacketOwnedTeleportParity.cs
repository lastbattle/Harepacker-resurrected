using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Entities;
using System;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
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
                message = $"Packet-owned teleport result rejected portal index {portalIndex}.";
                return true;
            }

            _packetOwnedTeleportRequestActive = false;
            _packetOwnedTeleportRequestCompletedAt = Environment.TickCount;

            PortalItem portal = _portalPool?.GetPortal(portalIndex);
            PortalInstance portalInstance = portal?.PortalInstance;
            if (portalInstance == null)
            {
                message = $"Packet-owned teleport result returned portal index {portalIndex}, but the current portal list could not resolve it.";
                return false;
            }

            RegisterPacketOwnedTeleportHandoff(portalInstance);
            ApplySameMapTeleportPosition(portalInstance.X, portalInstance.Y);
            message = $"Applied packet-owned teleport result for portal index {portalIndex} ({portalInstance.pn} -> {portalInstance.tn}).";
            return true;
        }

        private void RegisterPacketOwnedTeleportHandoff(PortalInstance portalInstance)
        {
            if (portalInstance == null)
            {
                return;
            }

            _lastPacketOwnedTeleportSourcePortalName = portalInstance.pn;
            _lastPacketOwnedTeleportTargetPortalName = portalInstance.tn;
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
