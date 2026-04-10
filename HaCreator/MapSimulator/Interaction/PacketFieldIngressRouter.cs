using HaCreator.MapSimulator.Managers;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class PacketFieldIngressRouter
    {
        internal static bool IsSupportedFieldScopedPacketType(int packetType)
        {
            return IsSupportedFieldStatePacketType(packetType)
                || Enum.IsDefined(typeof(PacketReactorPoolPacketKind), (PacketReactorPoolPacketKind)packetType);
        }

        internal static bool IsSupportedFieldStatePacketType(int packetType)
        {
            return packetType == 93
                || packetType == 149
                || packetType == 162
                || packetType == 163
                || packetType == 166
                || packetType == 167
                || packetType == 169
                || packetType == 174
                || packetType == 178;
        }

        internal static bool TryDecodeClientOpcodePacket(byte[] rawPacket, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                error = "Field-scoped client packet must include a 2-byte opcode.";
                return false;
            }

            packetType = BitConverter.ToUInt16(rawPacket, 0);
            if (!IsSupportedFieldScopedPacketType(packetType))
            {
                error = $"Unsupported field-scoped client opcode {packetType}.";
                return false;
            }

            payload = rawPacket.Length == sizeof(ushort)
                ? Array.Empty<byte>()
                : rawPacket[sizeof(ushort)..];

            return true;
        }

        internal static string DescribeSupportedClientOpcodes()
        {
            return "93, 149, 162, 163, 166, 167, 169, 174, 178, reactor-pool";
        }

        internal static string DescribeFieldScopedPacketType(int packetType)
        {
            return packetType.ToString();
        }
    }
}
