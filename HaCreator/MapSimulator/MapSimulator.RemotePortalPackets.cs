using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using System;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int LocalOpenGateRemoveNoticeStringPoolId = 0x18DE;
        private const string LocalOpenGateRemoveNoticeFallback = "The Open Gate has been closed.";

        private ChatCommandHandler.CommandResult ApplyRemotePortalPacketCommand(int packetType, byte[] payload)
        {
            if (_temporaryPortalField == null || _mapBoard?.MapInfo == null)
            {
                return ChatCommandHandler.CommandResult.Error("Remote portal pools are unavailable until a field is loaded.");
            }

            TemporaryPortalField.RemoteTownPortalResolvedDestination? townPortalDestination = null;
            if (packetType == (int)RemotePortalPacketType.TownPortalCreate
                && TryResolveMysticDoorReturnTarget(out int returnMapId, out float returnX, out float returnY))
            {
                townPortalDestination = new TemporaryPortalField.RemoteTownPortalResolvedDestination(returnMapId, returnX, returnY);
            }

            if (!_temporaryPortalField.TryApplyRemotePortalPacket(
                    packetType,
                    payload,
                    _mapBoard.MapInfo.id,
                    currTickCount,
                    townPortalDestination,
                    out string result))
            {
                return ChatCommandHandler.CommandResult.Error(result ?? $"Failed to apply remote portal packet {packetType}.");
            }

            if (ShouldShowLocalOpenGateRemoveNotice(packetType, payload, _playerManager?.Player?.Build?.Id ?? 0))
            {
                ShowUtilityFeedbackMessage(MapleStoryStringPool.GetOrFallback(
                    LocalOpenGateRemoveNoticeStringPoolId,
                    LocalOpenGateRemoveNoticeFallback));
            }

            return ChatCommandHandler.CommandResult.Ok($"{result} {_temporaryPortalField.DescribeRemotePortalStatus(_mapBoard.MapInfo.id)}");
        }

        private static bool ShouldShowLocalOpenGateRemoveNotice(int packetType, byte[] payload, int localCharacterId)
        {
            if (packetType != (int)RemotePortalPacketType.OpenGateRemove
                || localCharacterId <= 0
                || !RemotePortalPacketCodec.TryParseOpenGateRemoved(payload, out RemoteOpenGateRemovedPacket packet, out _))
            {
                return false;
            }

            return packet.State == 0
                   && packet.OwnerCharacterId == unchecked((uint)localCharacterId);
        }

        internal static bool ShouldShowLocalOpenGateRemoveNoticeForTesting(int packetType, byte[] payload, int localCharacterId)
        {
            return ShouldShowLocalOpenGateRemoveNotice(packetType, payload, localCharacterId);
        }

        private static bool TryParseOpenGateSlot(string token, out bool isFirstSlot)
        {
            string normalized = token?.Trim().ToLowerInvariant() ?? string.Empty;
            switch (normalized)
            {
                case "first":
                case "1":
                case "a":
                case "left":
                    isFirstSlot = true;
                    return true;

                case "second":
                case "2":
                case "b":
                case "right":
                    isFirstSlot = false;
                    return true;

                default:
                    isFirstSlot = false;
                    return false;
            }
        }

        private static byte[] BuildTownPortalCreatePayload(byte state, uint ownerId, short x, short y)
        {
            byte[] payload = new byte[9];
            payload[0] = state;
            BitConverter.TryWriteBytes(payload.AsSpan(1, 4), ownerId);
            BitConverter.TryWriteBytes(payload.AsSpan(5, 2), x);
            BitConverter.TryWriteBytes(payload.AsSpan(7, 2), y);
            return payload;
        }

        private static byte[] BuildTownPortalRemovePayload(byte state, uint ownerId)
        {
            byte[] payload = new byte[5];
            payload[0] = state;
            BitConverter.TryWriteBytes(payload.AsSpan(1, 4), ownerId);
            return payload;
        }

        private static byte[] BuildOpenGateCreatePayload(byte state, uint ownerId, short x, short y, bool isFirstSlot, uint partyId)
        {
            byte[] payload = new byte[14];
            payload[0] = state;
            BitConverter.TryWriteBytes(payload.AsSpan(1, 4), ownerId);
            BitConverter.TryWriteBytes(payload.AsSpan(5, 2), x);
            BitConverter.TryWriteBytes(payload.AsSpan(7, 2), y);
            payload[9] = isFirstSlot ? (byte)1 : (byte)0;
            BitConverter.TryWriteBytes(payload.AsSpan(10, 4), partyId);
            return payload;
        }

        private static byte[] BuildOpenGateRemovePayload(byte state, uint ownerId, bool isFirstSlot)
        {
            byte[] payload = new byte[6];
            payload[0] = state;
            BitConverter.TryWriteBytes(payload.AsSpan(1, 4), ownerId);
            payload[5] = isFirstSlot ? (byte)1 : (byte)0;
            return payload;
        }
    }
}
