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

        public static bool TryDecodeFieldInitPayload(byte[] payload, out int fieldId, out int shipKind)
        {
            fieldId = 0;
            shipKind = -1;
            if (payload == null || payload.Length < sizeof(int) + sizeof(byte))
            {
                return false;
            }

            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            fieldId = reader.ReadInt32();
            shipKind = reader.ReadByte();
            return IsSupportedShipKind(shipKind);
        }

        public static bool TryDecodeRawFieldInitPacket(byte[] rawPacket, out int fieldId, out int shipKind)
        {
            fieldId = 0;
            shipKind = -1;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort) + sizeof(int) + sizeof(byte))
            {
                return false;
            }

            ushort opcode = BitConverter.ToUInt16(rawPacket, 0);
            if (opcode != OutboundFieldInitOpcode)
            {
                return false;
            }

            byte[] payload = new byte[rawPacket.Length - sizeof(ushort)];
            Buffer.BlockCopy(rawPacket, sizeof(ushort), payload, 0, payload.Length);
            return TryDecodeFieldInitPayload(payload, out fieldId, out shipKind);
        }

        public static string DescribeRawFieldInitPacket(byte[] rawPacket)
        {
            return TryDecodeRawFieldInitPacket(rawPacket, out int fieldId, out int shipKind)
                ? $"transport field-init opcode {OutboundFieldInitOpcode} field={fieldId} shipKind={shipKind}"
                : $"transport field-init opcode {OutboundFieldInitOpcode}";
        }
    }
}
