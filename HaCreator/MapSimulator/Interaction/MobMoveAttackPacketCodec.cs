using System;
using System.Collections.Generic;
using System.IO;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Entities;
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
            public IReadOnlyList<MobPacketMovePathElement> MovePathElements { get; init; }
            public DecodedMovePathTailInfo? MovePathTailInfo { get; init; }
        }

        internal readonly record struct DecodedLockedTargetInfo(
            int RawValue,
            int EncodedEntityId,
            MobTargetType TargetType,
            int TargetSlotIndex);

        internal readonly record struct DecodedMovePathTailInfo(
            int PassiveKeyPadStateCount,
            Rectangle PathBounds);

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
                    MovePathElements = TryDecodeMovePathElements(reader, moveActionByte, out var movePathElements, out DecodedMovePathTailInfo? movePathTailInfo)
                        ? movePathElements
                        : Array.Empty<MobPacketMovePathElement>(),
                    MovePathTailInfo = movePathTailInfo
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

        private static bool TryDecodeMovePathElements(
            PacketReader reader,
            byte fallbackMoveActionByte,
            out IReadOnlyList<MobPacketMovePathElement> movePathElements,
            out DecodedMovePathTailInfo? movePathTailInfo)
        {
            movePathElements = Array.Empty<MobPacketMovePathElement>();
            movePathTailInfo = null;
            if (reader == null || reader.Remaining <= 0)
            {
                return true;
            }

            int tailLength = reader.Remaining;
            if (tailLength <= 0)
            {
                return true;
            }

            byte[] tail = reader.ReadBytes(tailLength);
            if (TryDecodeMovePathElementsCore(
                    tail,
                    decodeClientOptionalRandomCounts: false,
                    decodeClientFlushTail: false,
                    fallbackMoveActionByte,
                    out List<MobPacketMovePathElement> withoutRandomCounts,
                    out DecodedMovePathTailInfo? withoutRandomCountsTailInfo,
                    out int consumedWithoutRandomCounts) &&
                consumedWithoutRandomCounts == tail.Length)
            {
                movePathElements = withoutRandomCounts;
                movePathTailInfo = withoutRandomCountsTailInfo;
                return true;
            }

            if (TryDecodeMovePathElementsCore(
                    tail,
                    decodeClientOptionalRandomCounts: true,
                    decodeClientFlushTail: false,
                    fallbackMoveActionByte,
                    out List<MobPacketMovePathElement> withRandomCounts,
                    out DecodedMovePathTailInfo? withRandomCountsTailInfo,
                    out int consumedWithRandomCounts) &&
                consumedWithRandomCounts == tail.Length)
            {
                movePathElements = withRandomCounts;
                movePathTailInfo = withRandomCountsTailInfo;
                return true;
            }

            if (TryDecodeMovePathElementsCore(
                    tail,
                    decodeClientOptionalRandomCounts: false,
                    decodeClientFlushTail: true,
                    fallbackMoveActionByte,
                    out List<MobPacketMovePathElement> withFlushTail,
                    out DecodedMovePathTailInfo? withFlushTailInfo,
                    out int consumedWithFlushTail) &&
                consumedWithFlushTail == tail.Length)
            {
                movePathElements = withFlushTail;
                movePathTailInfo = withFlushTailInfo;
                return true;
            }

            if (TryDecodeMovePathElementsCore(
                    tail,
                    decodeClientOptionalRandomCounts: true,
                    decodeClientFlushTail: true,
                    fallbackMoveActionByte,
                    out List<MobPacketMovePathElement> withRandomCountsAndFlushTail,
                    out DecodedMovePathTailInfo? withRandomCountsAndFlushTailInfo,
                    out int consumedWithRandomCountsAndFlushTail) &&
                consumedWithRandomCountsAndFlushTail == tail.Length)
            {
                movePathElements = withRandomCountsAndFlushTail;
                movePathTailInfo = withRandomCountsAndFlushTailInfo;
                return true;
            }

            // Move-path decode is best-effort for packet-owned replay. Keep attack ownership
            // branches alive even when an uncommon tail layout is not yet modeled.
            movePathElements = Array.Empty<MobPacketMovePathElement>();
            return false;
        }

        private static bool TryDecodeMovePathElementsCore(
            byte[] tail,
            bool decodeClientOptionalRandomCounts,
            bool decodeClientFlushTail,
            byte fallbackMoveActionByte,
            out List<MobPacketMovePathElement> movePathElements,
            out DecodedMovePathTailInfo? movePathTailInfo,
            out int bytesConsumed)
        {
            movePathElements = new List<MobPacketMovePathElement>();
            movePathTailInfo = null;
            bytesConsumed = 0;
            if (tail == null || tail.Length == 0)
            {
                return true;
            }

            PacketReader reader = new(tail);
            try
            {
                float currentX = reader.ReadShort();
                float currentY = reader.ReadShort();
                float currentVelocityX = reader.ReadShort();
                float currentVelocityY = reader.ReadShort();
                int movePathElementCount = reader.ReadByte();
                if (movePathElementCount <= 0)
                {
                    bytesConsumed = reader.Position;
                    return true;
                }

                int cursorTime = 0;
                MobMoveType currentMoveType = MobMoveType.Move;
                MobJumpState currentJumpState = MobJumpState.None;

                for (int i = 0; i < movePathElementCount; i++)
                {
                    byte attr = reader.ReadByte();
                    bool readsCommonMoveSuffix = true;
                    float elementX = currentX;
                    float elementY = currentY;
                    float elementVelocityX = currentVelocityX;
                    float elementVelocityY = currentVelocityY;

                    switch (attr)
                    {
                        case 0:
                        case 5:
                        case 12:
                        case 14:
                        case 35:
                        case 36:
                            elementX = reader.ReadShort();
                            elementY = reader.ReadShort();
                            elementVelocityX = reader.ReadShort();
                            elementVelocityY = reader.ReadShort();
                            _ = reader.ReadShort(); // foothold id
                            if (attr == 12)
                            {
                                _ = reader.ReadShort(); // fall start foothold id
                            }

                            _ = reader.ReadShort(); // x offset
                            _ = reader.ReadShort(); // y offset
                            break;
                        case 1:
                        case 2:
                        case 13:
                        case 16:
                        case 18:
                        case 31:
                        case 32:
                        case 33:
                        case 34:
                            elementVelocityX = reader.ReadShort();
                            elementVelocityY = reader.ReadShort();
                            break;
                        case 3:
                        case 4:
                        case 6:
                        case 7:
                        case 8:
                        case 10:
                            elementX = reader.ReadShort();
                            elementY = reader.ReadShort();
                            _ = reader.ReadShort(); // foothold id
                            elementVelocityX = 0f;
                            elementVelocityY = 0f;
                            break;
                        case 9:
                            _ = reader.ReadByte();
                            elementVelocityX = 0f;
                            elementVelocityY = 0f;
                            readsCommonMoveSuffix = false;
                            break;
                        case 11:
                            elementVelocityX = reader.ReadShort();
                            elementVelocityY = reader.ReadShort();
                            _ = reader.ReadShort();
                            break;
                        case 17:
                            elementX = reader.ReadShort();
                            elementY = reader.ReadShort();
                            elementVelocityX = reader.ReadShort();
                            elementVelocityY = reader.ReadShort();
                            break;
                        case >= 20 and <= 30:
                            break;
                        default:
                            elementX = 0f;
                            elementY = 0f;
                            elementVelocityX = 0f;
                            elementVelocityY = 0f;
                            break;
                    }

                    byte moveActionByte = fallbackMoveActionByte;
                    int elapsedMs = 1;
                    if (readsCommonMoveSuffix)
                    {
                        moveActionByte = reader.ReadByte();
                        elapsedMs = Math.Max(1, (int)reader.ReadShort());
                        if (decodeClientOptionalRandomCounts)
                        {
                            _ = reader.ReadShort();
                            _ = reader.ReadShort();
                        }
                    }

                    int moveAction = moveActionByte >> 1;
                    bool facingRight = (moveActionByte & 1) != 0;
                    ResolveMoveTypeAndJumpState(moveAction, currentMoveType, currentJumpState, out currentMoveType, out currentJumpState);
                    MobAction action = ResolveMobAction(moveAction);

                    cursorTime += elapsedMs;
                    movePathElements.Add(new MobPacketMovePathElement(
                        elementX,
                        elementY,
                        elementVelocityX,
                        elementVelocityY,
                        currentMoveType,
                        currentJumpState,
                        action,
                        facingRight,
                        cursorTime,
                        moveAction));

                    currentX = elementX;
                    currentY = elementY;
                    currentVelocityX = elementVelocityX;
                    currentVelocityY = elementVelocityY;
                }

                if (decodeClientFlushTail)
                {
                    if (!TryDecodeMovePathFlushTail(reader, out DecodedMovePathTailInfo tailInfo))
                    {
                        bytesConsumed = reader.Position;
                        movePathElements.Clear();
                        return false;
                    }

                    movePathTailInfo = tailInfo;
                }

                bytesConsumed = reader.Position;
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is ArgumentOutOfRangeException || ex is OverflowException)
            {
                bytesConsumed = reader.Position;
                movePathElements.Clear();
                return false;
            }
        }

        private static bool TryDecodeMovePathFlushTail(PacketReader reader, out DecodedMovePathTailInfo movePathTailInfo)
        {
            movePathTailInfo = default;
            if (reader == null)
            {
                return false;
            }

            try
            {
                int passiveKeyPadStateCount = reader.ReadByte();
                int packedStateCount = (passiveKeyPadStateCount + 1) / 2;
                for (int i = 0; i < packedStateCount; i++)
                {
                    _ = reader.ReadByte();
                }

                short left = reader.ReadShort();
                short top = reader.ReadShort();
                short right = reader.ReadShort();
                short bottom = reader.ReadShort();
                int width = Math.Max(0, right - left);
                int height = Math.Max(0, bottom - top);
                movePathTailInfo = new DecodedMovePathTailInfo(
                    passiveKeyPadStateCount,
                    new Rectangle(left, top, width, height));
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is ArgumentOutOfRangeException || ex is OverflowException)
            {
                movePathTailInfo = default;
                return false;
            }
        }

        private static void ResolveMoveTypeAndJumpState(
            int moveAction,
            MobMoveType currentMoveType,
            MobJumpState currentJumpState,
            out MobMoveType moveType,
            out MobJumpState jumpState)
        {
            moveType = currentMoveType;
            jumpState = currentJumpState;
            switch (moveAction)
            {
                case 0:
                    jumpState = MobJumpState.None;
                    break;
                case 1:
                    moveType = MobMoveType.Move;
                    jumpState = MobJumpState.None;
                    break;
                case 2:
                case 3:
                    moveType = MobMoveType.Jump;
                    if (jumpState == MobJumpState.None)
                    {
                        jumpState = MobJumpState.Falling;
                    }

                    break;
            }
        }

        private static MobAction ResolveMobAction(int moveAction)
        {
            return moveAction switch
            {
                >= 13 and <= 21 => MobAction.Attack1,
                0 => MobAction.Stand,
                1 => MobAction.Move,
                2 or 3 => MobAction.Jump,
                _ => MobAction.Move
            };
        }
    }
}
