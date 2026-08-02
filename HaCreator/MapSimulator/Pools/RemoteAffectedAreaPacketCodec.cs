using System;
using System.Buffers.Binary;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Pools
{
    internal enum RemoteAffectedAreaPacketType
    {
        Create = 328,
        Remove = 329
    }

    internal readonly record struct RemoteAffectedAreaCreatedPacket(
        int ObjectId,
        int Type,
        uint OwnerCharacterId,
        int SkillId,
        byte SkillLevel,
        short StartDelayUnits,
        Rectangle Bounds,
        int ElementAttribute,
        int Phase);

    internal readonly record struct RemoteAffectedAreaRemovedPacket(int ObjectId);

    internal static class RemoteAffectedAreaPacketCodec
    {
        private const int CreateLength = 43;
        private const int RemoveLength = 4;

        public static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                (int)RemoteAffectedAreaPacketType.Create => "create (328)",
                (int)RemoteAffectedAreaPacketType.Remove => "remove (329)",
                _ => packetType.ToString()
            };
        }

        public static bool TryParseCreated(ReadOnlySpan<byte> payload, out RemoteAffectedAreaCreatedPacket packet, out string error)
        {
            packet = default;
            if (!TryValidateLength(payload, CreateLength, "AffectedAreaCreate", out error))
            {
                return false;
            }

            int left = BinaryPrimitives.ReadInt32LittleEndian(payload[19..23]);
            int top = BinaryPrimitives.ReadInt32LittleEndian(payload[23..27]);
            int right = BinaryPrimitives.ReadInt32LittleEndian(payload[27..31]);
            int bottom = BinaryPrimitives.ReadInt32LittleEndian(payload[31..35]);

            packet = new RemoteAffectedAreaCreatedPacket(
                BinaryPrimitives.ReadInt32LittleEndian(payload[0..4]),
                BinaryPrimitives.ReadInt32LittleEndian(payload[4..8]),
                BinaryPrimitives.ReadUInt32LittleEndian(payload[8..12]),
                BinaryPrimitives.ReadInt32LittleEndian(payload[12..16]),
                payload[16],
                BinaryPrimitives.ReadInt16LittleEndian(payload[17..19]),
                NormalizeBounds(left, top, right, bottom),
                BinaryPrimitives.ReadInt32LittleEndian(payload[35..39]),
                BinaryPrimitives.ReadInt32LittleEndian(payload[39..43]));
            return true;
        }

        public static bool TryParseRemoved(ReadOnlySpan<byte> payload, out RemoteAffectedAreaRemovedPacket packet, out string error)
        {
            packet = default;
            if (!TryValidateLength(payload, RemoveLength, "AffectedAreaRemove", out error))
            {
                return false;
            }

            packet = new RemoteAffectedAreaRemovedPacket(BinaryPrimitives.ReadInt32LittleEndian(payload));
            return true;
        }

        public static byte[] BuildCreatePayload(
            int objectId,
            int type,
            uint ownerCharacterId,
            int skillId,
            byte skillLevel,
            short startDelayUnits,
            Rectangle bounds,
            int elementAttribute,
            int phase)
        {
            byte[] payload = new byte[CreateLength];
            BitConverter.TryWriteBytes(payload.AsSpan(0, 4), objectId);
            BitConverter.TryWriteBytes(payload.AsSpan(4, 4), type);
            BitConverter.TryWriteBytes(payload.AsSpan(8, 4), ownerCharacterId);
            BitConverter.TryWriteBytes(payload.AsSpan(12, 4), skillId);
            payload[16] = skillLevel;
            BitConverter.TryWriteBytes(payload.AsSpan(17, 2), startDelayUnits);
            BitConverter.TryWriteBytes(payload.AsSpan(19, 4), bounds.Left);
            BitConverter.TryWriteBytes(payload.AsSpan(23, 4), bounds.Top);
            BitConverter.TryWriteBytes(payload.AsSpan(27, 4), bounds.Right);
            BitConverter.TryWriteBytes(payload.AsSpan(31, 4), bounds.Bottom);
            BitConverter.TryWriteBytes(payload.AsSpan(35, 4), elementAttribute);
            BitConverter.TryWriteBytes(payload.AsSpan(39, 4), phase);
            return payload;
        }

        public static byte[] BuildRemovePayload(int objectId)
        {
            byte[] payload = new byte[RemoveLength];
            BitConverter.TryWriteBytes(payload.AsSpan(0, 4), objectId);
            return payload;
        }

        private static Rectangle NormalizeBounds(int left, int top, int right, int bottom)
        {
            int normalizedLeft = Math.Min(left, right);
            int normalizedTop = Math.Min(top, bottom);
            int normalizedRight = Math.Max(left, right);
            int normalizedBottom = Math.Max(top, bottom);
            return new Rectangle(
                normalizedLeft,
                normalizedTop,
                Math.Max(1, normalizedRight - normalizedLeft),
                Math.Max(1, normalizedBottom - normalizedTop));
        }

        private static bool TryValidateLength(ReadOnlySpan<byte> payload, int expectedLength, string label, out string error)
        {
            if (payload.Length != expectedLength)
            {
                error = $"{label} expects {expectedLength} bytes but received {payload.Length}.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
