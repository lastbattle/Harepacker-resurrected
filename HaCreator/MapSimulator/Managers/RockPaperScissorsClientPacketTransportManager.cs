using HaCreator.MapSimulator.Fields;
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
            byte[] raw = new byte[sizeof(ushort) + payload.Length];
            BitConverter.GetBytes((ushort)packet.Opcode).CopyTo(raw, 0);
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, raw, sizeof(ushort), payload.Length);
            }

            return raw;
        }

        public static byte[] BuildClientPayload(RockPaperScissorsClientPacket packet)
        {
            if (packet == null)
            {
                return Array.Empty<byte>();
            }

            int payloadLength = packet.Payload?.Length ?? 0;
            byte[] payload = new byte[sizeof(byte) + payloadLength];
            payload[0] = (byte)packet.RequestType;
            if (payloadLength > 0)
            {
                Buffer.BlockCopy(packet.Payload, 0, payload, sizeof(byte), payloadLength);
            }

            return payload;
        }
    }
}
