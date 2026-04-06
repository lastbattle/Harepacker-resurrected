using System;
using System.Collections.Generic;
using System.IO;
using HaCreator.MapSimulator.AI;
using MapleLib.PacketLib;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class MobMoveAttackPacketCodec
    {
        private const int MovePacketType = 287;
        private const int MinAttackMoveAction = 13;
        private const int MaxAttackMoveAction = 21;
        private const int MaxOverrideEntryCount = 128;

        internal sealed class DecodedMoveAttackPacket
        {
            public int PacketType { get; init; }
            public int MobId { get; init; }
            public int TargetInfoRaw { get; init; }
            public DecodedLockedTargetInfo? LockedTargetInfo { get; init; }
            public bool NotForceLandingWhenDiscard { get; init; }
            public bool NotChangeAction { get; init; }
            public bool NextAttackPossible { get; init; }
            public bool FacingLeft { get; init; }
            public int MoveAction { get; init; }
            public int AttackId { get; init; }
            public List<Point> MultiTargetForBall { get; init; }
            public List<int> RandTimeForAreaAttack { get; init; }
        }

        internal readonly record struct DecodedLockedTargetInfo(
            int RawValue,
            int EncodedEntityId,
            MobTargetType TargetType,
            int TargetSlotIndex);

        internal static MobTargetInfo CreateLockedTargetOverride(DecodedLockedTargetInfo? lockedTargetInfo)
        {
            if (lockedTargetInfo is not DecodedLockedTargetInfo decoded)
            {
                return null;
            }

            return new MobTargetInfo
            {
                TargetId = decoded.EncodedEntityId,
                TargetSlotIndex = decoded.TargetSlotIndex,
                TargetType = decoded.TargetType,
                IsValid = true
            };
        }

        internal static bool ShouldQueueSimulatorAttackOverrides(DecodedMoveAttackPacket decodedPacket)
        {
            if (decodedPacket == null || decodedPacket.AttackId <= 0 || !decodedPacket.NextAttackPossible)
            {
                return false;
            }

            // CMob::OnMove only reaches CMob::DoAttack for attack move-actions when
            // bNotChangeAction is clear. Packet-owned target lanes should not leak
            // into the simulator attack queue if the client suppressed the action.
            return !decodedPacket.NotChangeAction;
        }

        public static bool TryDecode(
            int packetType,
            byte[] payload,
            out DecodedMoveAttackPacket decodedPacket,
            out string error)
        {
            decodedPacket = null;
            error = null;

            if (packetType != MovePacketType)
            {
                error = $"Unsupported mob packet type {packetType}.";
                return false;
            }

            PacketReader reader = new(payload ?? Array.Empty<byte>());
            try
            {
                int mobId = reader.ReadInt();
                if (mobId <= 0)
                {
                    error = "Mob move packet did not include a valid mob id.";
                    return false;
                }

                bool notForceLandingWhenDiscard = reader.ReadByte() != 0;
                bool notChangeAction = reader.ReadByte() != 0;
                bool nextAttackPossible = reader.ReadByte() != 0;
                byte moveActionByte = reader.ReadByte();
                int targetInfoRaw = reader.ReadInt();

                List<Point> multiTargetForBall = ReadPointList(reader, out error);
                if (error != null)
                {
                    return false;
                }

                List<int> randTimeForAreaAttack = ReadIntList(reader, out error);
                if (error != null)
                {
                    return false;
                }

                int moveAction = moveActionByte >> 1;
                decodedPacket = new DecodedMoveAttackPacket
                {
                    PacketType = packetType,
                    MobId = mobId,
                    TargetInfoRaw = targetInfoRaw,
                    LockedTargetInfo = TryDecodeLockedTargetInfoRaw(targetInfoRaw, out DecodedLockedTargetInfo lockedTargetInfo)
                        ? lockedTargetInfo
                        : null,
                    NotForceLandingWhenDiscard = notForceLandingWhenDiscard,
                    NotChangeAction = notChangeAction,
                    NextAttackPossible = nextAttackPossible,
                    FacingLeft = (moveActionByte & 1) == 0,
                    MoveAction = moveAction,
                    AttackId = TryResolveAttackId(moveAction, out int attackId) ? attackId : 0,
                    MultiTargetForBall = multiTargetForBall,
                    RandTimeForAreaAttack = randTimeForAreaAttack,
                };
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is ArgumentOutOfRangeException || ex is OverflowException)
            {
                error = $"Mob move packet ended before the client attack override header was complete: {ex.Message}";
                return false;
            }
        }

        internal static bool TryResolveAttackId(int moveAction, out int attackId)
        {
            if (moveAction >= MinAttackMoveAction && moveAction <= MaxAttackMoveAction)
            {
                attackId = moveAction - (MinAttackMoveAction - 1);
                return true;
            }

            attackId = 0;
            return false;
        }

        internal static bool TryDecodeLockedTargetInfoRaw(int targetInfoRaw, out DecodedLockedTargetInfo lockedTargetInfo)
        {
            lockedTargetInfo = default;
            if (targetInfoRaw <= 0)
            {
                return false;
            }

            int encodedType = targetInfoRaw & 0x3;
            int encodedEntityId = targetInfoRaw >> 2;
            if (encodedEntityId <= 0)
            {
                return false;
            }

            switch (encodedType)
            {
                case 0:
                    lockedTargetInfo = new DecodedLockedTargetInfo(
                        targetInfoRaw,
                        encodedEntityId,
                        MobTargetType.Player,
                        -1);
                    return true;

                case 1:
                case 2:
                    lockedTargetInfo = new DecodedLockedTargetInfo(
                        targetInfoRaw,
                        encodedEntityId,
                        MobTargetType.Summoned,
                        encodedType - 1);
                    return true;

                case 3:
                    lockedTargetInfo = new DecodedLockedTargetInfo(
                        targetInfoRaw,
                        encodedEntityId,
                        MobTargetType.Mob,
                        -1);
                    return true;

                default:
                    return false;
            }
        }

        private static List<Point> ReadPointList(PacketReader reader, out string error)
        {
            error = null;
            int count = reader.ReadInt();
            if (count < 0 || count > MaxOverrideEntryCount)
            {
                error = $"Mob move packet multi-target count {count} was outside the supported range 0-{MaxOverrideEntryCount}.";
                return null;
            }

            var points = new List<Point>(count);
            for (int i = 0; i < count; i++)
            {
                points.Add(new Point(reader.ReadInt(), reader.ReadInt()));
            }

            return points;
        }

        private static List<int> ReadIntList(PacketReader reader, out string error)
        {
            error = null;
            int count = reader.ReadInt();
            if (count < 0 || count > MaxOverrideEntryCount)
            {
                error = $"Mob move packet random-area-delay count {count} was outside the supported range 0-{MaxOverrideEntryCount}.";
                return null;
            }

            var values = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                values.Add(reader.ReadInt());
            }

            return values;
        }
    }
}
