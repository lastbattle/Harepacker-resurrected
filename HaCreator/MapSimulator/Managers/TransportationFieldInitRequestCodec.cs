using System;
using System.IO;

namespace HaCreator.MapSimulator.Managers
{
    public static class TransportationFieldInitRequestCodec
    {
        public const ushort OutboundFieldInitOpcode = 264;

        public static bool IsSupportedShipKind(int shipKind)
        {
            return shipKind is 0 or 1;
        }

        public static byte[] BuildFieldInitPayload(int fieldId, int shipKind)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write(fieldId);
            writer.Write((byte)shipKind);
            return stream.ToArray();
        }

        public static byte[] BuildRawFieldInitPacket(int fieldId, int shipKind)
        {
            byte[] payload = BuildFieldInitPayload(fieldId, shipKind);
            byte[] rawPacket = new byte[sizeof(ushort) + payload.Length];
            BitConverter.GetBytes(OutboundFieldInitOpcode).CopyTo(rawPacket, 0);
            payload.CopyTo(rawPacket, sizeof(ushort));
            return rawPacket;
        }
    }
}
