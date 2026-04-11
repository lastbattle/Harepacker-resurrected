using System;
using System.Buffers.Binary;

namespace HaCreator.MapSimulator.Fields
{
    internal enum RemotePortalPacketType
    {
        TownPortalCreate = 330,
        TownPortalRemove = 331,
        OpenGateCreate = 332,
        OpenGateRemove = 333
    }

    internal readonly record struct RemoteTownPortalCreatedPacket(byte State, uint OwnerCharacterId, short X, short Y);
    internal readonly record struct RemoteTownPortalRemovedPacket(byte State, uint OwnerCharacterId);
    internal readonly record struct RemoteOpenGateCreatedPacket(byte State, uint OwnerCharacterId, short X, short Y, bool IsFirstSlot, uint PartyId);
    internal readonly record struct RemoteOpenGateRemovedPacket(byte State, uint OwnerCharacterId, bool IsFirstSlot);

    internal static class RemotePortalPacketCodec
    {
        private const int TownPortalCreateLength = 9;
        private const int TownPortalRemoveLength = 5;
        private const int OpenGateCreateLength = 14;
        private const int OpenGateRemoveLength = 6;

        public static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                (int)RemotePortalPacketType.TownPortalCreate => "towncreate (330)",
                (int)RemotePortalPacketType.TownPortalRemove => "townremove (331)",
                (int)RemotePortalPacketType.OpenGateCreate => "opengatecreate (332)",
                (int)RemotePortalPacketType.OpenGateRemove => "opengateremove (333)",
                _ => packetType.ToString()
            };
        }

        public static bool TryParseTownPortalCreated(ReadOnlySpan<byte> payload, out RemoteTownPortalCreatedPacket packet, out string error)
        {
            packet = default;
            if (!TryValidateLength(payload, TownPortalCreateLength, "TownPortalCreate", out error))
            {
                return false;
            }

            packet = new RemoteTownPortalCreatedPacket(
                payload[0],
                BinaryPrimitives.ReadUInt32LittleEndian(payload[1..5]),
                BinaryPrimitives.ReadInt16LittleEndian(payload[5..7]),
                BinaryPrimitives.ReadInt16LittleEndian(payload[7..9]));
            return true;
        }

        public static bool TryParseTownPortalRemoved(ReadOnlySpan<byte> payload, out RemoteTownPortalRemovedPacket packet, out string error)
        {
            packet = default;
            if (!TryValidateLength(payload, TownPortalRemoveLength, "TownPortalRemove", out error))
            {
                return false;
            }

            packet = new RemoteTownPortalRemovedPacket(
                payload[0],
                BinaryPrimitives.ReadUInt32LittleEndian(payload[1..5]));
            return true;
        }

        public static bool TryParseOpenGateCreated(ReadOnlySpan<byte> payload, out RemoteOpenGateCreatedPacket packet, out string error)
        {
            packet = default;
            if (!TryValidateLength(payload, OpenGateCreateLength, "OpenGateCreate", out error))
            {
                return false;
            }

            packet = new RemoteOpenGateCreatedPacket(
                payload[0],
                BinaryPrimitives.ReadUInt32LittleEndian(payload[1..5]),
                BinaryPrimitives.ReadInt16LittleEndian(payload[5..7]),
                BinaryPrimitives.ReadInt16LittleEndian(payload[7..9]),
                payload[9] != 0,
                BinaryPrimitives.ReadUInt32LittleEndian(payload[10..14]));
            return true;
        }

        public static bool TryParseOpenGateRemoved(ReadOnlySpan<byte> payload, out RemoteOpenGateRemovedPacket packet, out string error)
        {
            packet = default;
            if (!TryValidateLength(payload, OpenGateRemoveLength, "OpenGateRemove", out error))
            {
                return false;
            }

            packet = new RemoteOpenGateRemovedPacket(
                payload[0],
                BinaryPrimitives.ReadUInt32LittleEndian(payload[1..5]),
                payload[5] != 0);
            return true;
        }

        private static bool TryValidateLength(ReadOnlySpan<byte> payload, int minimumLength, string label, out string error)
        {
            if (payload.Length < minimumLength)
            {
                error = $"{label} expects at least {minimumLength} bytes but received {payload.Length}.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
