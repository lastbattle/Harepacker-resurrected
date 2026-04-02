using System;
using System.IO;
using MapleLib.PacketLib;

namespace HaCreator.MapSimulator.Pools
{
    public enum RemoteDropPacketType
    {
        Enter = 322,
        Leave = 324
    }

    public readonly record struct RemoteDropEnterPacket(
        byte EnterType,
        int DropId,
        bool IsMoney,
        int Info,
        int OwnerId,
        DropOwnershipType OwnershipType,
        short TargetX,
        short TargetY,
        int SourceId,
        bool HasStartPosition,
        short StartX,
        short StartY,
        short DelayMs,
        bool AllowPetPickup,
        bool ElevateLayer);

    public readonly record struct RemoteDropLeavePacket(
        PacketDropLeaveReason Reason,
        int DropId,
        int ActorId,
        short DelayMs,
        int SecondaryActorId);

    public static class RemoteDropPacketCodec
    {
        public static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                (int)RemoteDropPacketType.Enter => "drop-enter (322)",
                (int)RemoteDropPacketType.Leave => "drop-leave (324)",
                _ => packetType.ToString()
            };
        }

        public static bool TryParseEnter(ReadOnlySpan<byte> payload, out RemoteDropEnterPacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload.ToArray());
                byte enterType = reader.ReadByte();
                int dropId = reader.ReadInt();
                bool isMoney = reader.ReadByte() != 0;
                int info = reader.ReadInt();
                int ownerId = reader.ReadInt();
                DropOwnershipType ownershipType = NormalizeOwnershipType(reader.ReadByte());
                short targetX = reader.ReadShort();
                short targetY = reader.ReadShort();
                int sourceId = reader.ReadInt();

                bool hasStartPosition = RequiresStartPosition(enterType);
                short startX = 0;
                short startY = 0;
                short delayMs = 0;
                if (hasStartPosition)
                {
                    startX = reader.ReadShort();
                    startY = reader.ReadShort();
                    delayMs = reader.ReadShort();
                }

                if (!isMoney)
                {
                    reader.ReadLong();
                }

                bool allowPetPickup = reader.ReadByte() != 0;
                bool elevateLayer = false;
                try
                {
                    elevateLayer = reader.ReadByte() != 0;
                }
                catch (EndOfStreamException)
                {
                    elevateLayer = false;
                }

                packet = new RemoteDropEnterPacket(
                    enterType,
                    dropId,
                    isMoney,
                    info,
                    ownerId,
                    ownershipType,
                    targetX,
                    targetY,
                    sourceId,
                    hasStartPosition,
                    startX,
                    startY,
                    delayMs,
                    allowPetPickup,
                    elevateLayer);
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is InvalidOperationException)
            {
                error = $"DropEnter parse failed: {ex.Message}";
                return false;
            }
        }

        public static bool TryParseLeave(ReadOnlySpan<byte> payload, out RemoteDropLeavePacket packet, out string error)
        {
            packet = default;
            error = null;

            try
            {
                var reader = new PacketReader(payload.ToArray());
                PacketDropLeaveReason reason = NormalizeLeaveReason(reader.ReadByte());
                int dropId = reader.ReadInt();
                int actorId = 0;
                short delayMs = 0;
                int secondaryActorId = 0;

                switch (reason)
                {
                    case PacketDropLeaveReason.PlayerPickup:
                    case PacketDropLeaveReason.MobPickup:
                    case PacketDropLeaveReason.PetPickup:
                        actorId = reader.ReadInt();
                        break;

                    case PacketDropLeaveReason.Explode:
                        delayMs = reader.ReadShort();
                        break;
                }

                if (reason == PacketDropLeaveReason.PetPickup)
                {
                    try
                    {
                        secondaryActorId = reader.ReadInt();
                    }
                    catch (EndOfStreamException)
                    {
                        secondaryActorId = 0;
                    }
                }

                packet = new RemoteDropLeavePacket(reason, dropId, actorId, delayMs, secondaryActorId);
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is InvalidOperationException)
            {
                error = $"DropLeave parse failed: {ex.Message}";
                return false;
            }
        }

        public static byte[] BuildEnterPayload(
            byte enterType,
            int dropId,
            bool isMoney,
            int info,
            int ownerId,
            DropOwnershipType ownershipType,
            short targetX,
            short targetY,
            int sourceId,
            short? startX = null,
            short? startY = null,
            short delayMs = 0,
            long expireRaw = 0,
            bool allowPetPickup = true,
            bool elevateLayer = false)
        {
            bool hasStartPosition = RequiresStartPosition(enterType);
            int length = 1 + 4 + 1 + 4 + 4 + 1 + 2 + 2 + 4 + (hasStartPosition ? 6 : 0) + (isMoney ? 0 : 8) + 1 + 1;
            byte[] payload = new byte[length];
            int offset = 0;

            payload[offset++] = enterType;
            BitConverter.TryWriteBytes(payload.AsSpan(offset, 4), dropId);
            offset += 4;
            payload[offset++] = isMoney ? (byte)1 : (byte)0;
            BitConverter.TryWriteBytes(payload.AsSpan(offset, 4), info);
            offset += 4;
            BitConverter.TryWriteBytes(payload.AsSpan(offset, 4), ownerId);
            offset += 4;
            payload[offset++] = (byte)ownershipType;
            BitConverter.TryWriteBytes(payload.AsSpan(offset, 2), targetX);
            offset += 2;
            BitConverter.TryWriteBytes(payload.AsSpan(offset, 2), targetY);
            offset += 2;
            BitConverter.TryWriteBytes(payload.AsSpan(offset, 4), sourceId);
            offset += 4;

            if (hasStartPosition)
            {
                BitConverter.TryWriteBytes(payload.AsSpan(offset, 2), startX ?? targetX);
                offset += 2;
                BitConverter.TryWriteBytes(payload.AsSpan(offset, 2), startY ?? targetY);
                offset += 2;
                BitConverter.TryWriteBytes(payload.AsSpan(offset, 2), delayMs);
                offset += 2;
            }

            if (!isMoney)
            {
                BitConverter.TryWriteBytes(payload.AsSpan(offset, 8), expireRaw);
                offset += 8;
            }

            payload[offset++] = allowPetPickup ? (byte)1 : (byte)0;
            payload[offset] = elevateLayer ? (byte)1 : (byte)0;
            return payload;
        }

        public static byte[] BuildLeavePayload(
            PacketDropLeaveReason reason,
            int dropId,
            int actorId = 0,
            short delayMs = 0,
            int secondaryActorId = 0)
        {
            int length = 1 + 4;
            if (reason == PacketDropLeaveReason.PlayerPickup
                || reason == PacketDropLeaveReason.MobPickup
                || reason == PacketDropLeaveReason.PetPickup)
            {
                length += 4;
            }
            else if (reason == PacketDropLeaveReason.Explode)
            {
                length += 2;
            }

            if (reason == PacketDropLeaveReason.PetPickup)
            {
                length += 4;
            }

            byte[] payload = new byte[length];
            int offset = 0;
            payload[offset++] = (byte)reason;
            BitConverter.TryWriteBytes(payload.AsSpan(offset, 4), dropId);
            offset += 4;

            if (reason == PacketDropLeaveReason.PlayerPickup
                || reason == PacketDropLeaveReason.MobPickup
                || reason == PacketDropLeaveReason.PetPickup)
            {
                BitConverter.TryWriteBytes(payload.AsSpan(offset, 4), actorId);
                offset += 4;
            }
            else if (reason == PacketDropLeaveReason.Explode)
            {
                BitConverter.TryWriteBytes(payload.AsSpan(offset, 2), delayMs);
                offset += 2;
            }

            if (reason == PacketDropLeaveReason.PetPickup)
            {
                BitConverter.TryWriteBytes(payload.AsSpan(offset, 4), secondaryActorId);
            }

            return payload;
        }

        private static bool RequiresStartPosition(byte enterType)
        {
            return enterType < 2 || enterType == 3 || enterType == 4;
        }

        private static DropOwnershipType NormalizeOwnershipType(byte value)
        {
            return value switch
            {
                0 => DropOwnershipType.Character,
                1 => DropOwnershipType.Party,
                3 => DropOwnershipType.Explosive,
                _ => DropOwnershipType.None
            };
        }

        private static PacketDropLeaveReason NormalizeLeaveReason(byte value)
        {
            return value switch
            {
                2 => PacketDropLeaveReason.PlayerPickup,
                3 => PacketDropLeaveReason.MobPickup,
                4 => PacketDropLeaveReason.Explode,
                5 => PacketDropLeaveReason.PetPickup,
                _ => PacketDropLeaveReason.Remove
            };
        }
    }
}
