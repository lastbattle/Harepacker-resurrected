using HaCreator.MapSimulator.Fields;
using MapleLib.PacketLib;
using System;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Packet framing helper for client-authored CRPSGameDlg opcode 160 packets.
    /// Transport ownership lives in the shared role-session bridge.
    /// </summary>
    public static class RockPaperScissorsClientPacketTransportManager
    {
        internal static byte[] BuildRawPacket(RockPaperScissorsClientPacket packet)
        {
            if (packet == null)
            {
                return Array.Empty<byte>();
            }

            byte[] payload = BuildClientPayload(packet);
            using PacketWriter writer = new();
            writer.Write((ushort)packet.Opcode);
            writer.WriteBytes(payload);
            return writer.ToArray();
        }

        public static byte[] BuildClientPayload(RockPaperScissorsClientPacket packet)
        {
            if (packet == null)
            {
                return Array.Empty<byte>();
            }

            using PacketWriter writer = new();
            writer.WriteByte((byte)packet.RequestType);
            if (packet.Payload != null)
            {
                writer.WriteBytes(packet.Payload);
            }

            return writer.ToArray();
        }
    }
}
