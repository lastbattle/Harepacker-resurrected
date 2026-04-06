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
            return packetType == 149
                || packetType == 162
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

            if (!ReactorPoolPacketInboxManager.TryDecodeOpcodeFramedPacket(rawPacket, out packetType, out payload, out error))
            {
                return false;
            }

            if (!IsSupportedFieldScopedPacketType(packetType))
            {
                error = $"Unsupported field-scoped client opcode {packetType}.";
                return false;
            }

            return true;
        }
    }
}
