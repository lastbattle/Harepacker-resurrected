using System;
using System.Collections.Generic;
using System.IO;

using BinaryWriter = MapleLib.PacketLib.PacketWriter;
namespace HaCreator.MapSimulator.Physics
{
    internal static class CMovePathClientPacketCodec
    {
        public static bool TryEncode(
            IReadOnlyList<MovePathElement> path,
            out byte[] payload,
            out string error,
            bool includeClientRandomCounts = false,
            bool includeClientFlushTail = false,
            IReadOnlyList<byte> passiveKeyPadStates = null)
        {
            payload = Array.Empty<byte>();
            error = null;
            if (path == null || path.Count == 0)
            {
                error = "Move path is empty.";
                return false;
            }

            if (path.Count > byte.MaxValue)
            {
                error = $"Move path has {path.Count} elements; the client packet count is one byte.";
                return false;
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            MovePathElement start = path[0];
            writer.Write(ClampToShort(start.X));
            writer.Write(ClampToShort(start.Y));
            writer.Write(start.VelocityX);
            writer.Write(start.VelocityY);
            writer.Write((byte)path.Count);

            for (int i = 0; i < path.Count; i++)
            {
                WriteElement(writer, path[i], includeClientRandomCounts);
            }

            if (includeClientFlushTail)
            {
                WriteFlushTail(writer, path, start, passiveKeyPadStates);
            }

            writer.Flush();
            payload = stream.ToArray();
            return true;
        }

        internal static IReadOnlyList<MovePathElement> NormalizeForPortalOwnedClientMakeMovePath(
            IReadOnlyList<MovePathElement> path)
        {
            if (path == null || path.Count <= 1)
            {
                return path ?? Array.Empty<MovePathElement>();
            }

            List<MovePathElement> normalized = new(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                MovePathElement current = NormalizePortalOwnedClientMakeMovePathElement(path[i]);
                if (normalized.Count == 0)
                {
                    normalized.Add(current);
                    continue;
                }

                int tailIndex = normalized.Count - 1;
                MovePathElement tail = normalized[tailIndex];
                if (CanCoalesceClientMakeMovePathTail(tail, current))
                {
                    tail.Duration = ClampDurationToShort(tail.Duration + Math.Max(0, (int)current.Duration));
                    tail.X = current.X;
                    tail.Y = current.Y;
                    tail.VelocityX = current.VelocityX;
                    tail.VelocityY = current.VelocityY;
                    tail.FallStartFootholdId = current.FallStartFootholdId;
                    tail.XOffset = current.XOffset;
                    tail.YOffset = current.YOffset;
                    tail.RandomCount = current.RandomCount;
                    tail.ActualRandomCount = current.ActualRandomCount;
                    normalized[tailIndex] = tail;
                    continue;
                }

                if (CanReplacePlaceholderTailWithCurrent(tail))
                {
                    normalized[tailIndex] = current;
                    continue;
                }

                normalized.Add(current);
            }

            return normalized;
        }

        internal static IReadOnlyList<byte> NormalizePassiveKeyPadStatesForClientMakeMovePath(
            IReadOnlyList<MovePathElement> path,
            IReadOnlyList<byte> passiveKeyPadStates)
        {
            if (path == null || path.Count == 0 || passiveKeyPadStates == null || passiveKeyPadStates.Count == 0)
            {
                return Array.Empty<byte>();
            }

            List<MovePathElement> normalized = new(path.Count);
            List<byte> normalizedKeyPadStates = new(Math.Min(path.Count, passiveKeyPadStates.Count));
            for (int i = 0; i < path.Count; i++)
            {
                MovePathElement current = NormalizePortalOwnedClientMakeMovePathElement(path[i]);
                byte currentKeyPadState = i < passiveKeyPadStates.Count
                    ? (byte)(passiveKeyPadStates[i] & 0x0F)
                    : (byte)0;

                if (normalized.Count == 0)
                {
                    normalized.Add(current);
                    normalizedKeyPadStates.Add(currentKeyPadState);
                    continue;
                }

                int tailIndex = normalized.Count - 1;
                MovePathElement tail = normalized[tailIndex];
                if (CanCoalesceClientMakeMovePathTail(tail, current))
                {
                    tail.Duration = ClampDurationToShort(tail.Duration + Math.Max(0, (int)current.Duration));
                    tail.X = current.X;
                    tail.Y = current.Y;
                    tail.VelocityX = current.VelocityX;
                    tail.VelocityY = current.VelocityY;
                    tail.FallStartFootholdId = current.FallStartFootholdId;
                    tail.XOffset = current.XOffset;
                    tail.YOffset = current.YOffset;
                    tail.RandomCount = current.RandomCount;
                    tail.ActualRandomCount = current.ActualRandomCount;
                    normalized[tailIndex] = tail;
                    normalizedKeyPadStates[tailIndex] = currentKeyPadState;
                    continue;
                }

                if (CanReplacePlaceholderTailWithCurrent(tail))
                {
                    normalized[tailIndex] = current;
                    normalizedKeyPadStates[tailIndex] = currentKeyPadState;
                    continue;
                }

                normalized.Add(current);
                normalizedKeyPadStates.Add(currentKeyPadState);
            }

            return normalizedKeyPadStates;
        }

        private static MovePathElement NormalizePortalOwnedClientMakeMovePathElement(MovePathElement element)
        {
            if (!IsPortalOwnedImpactAttribute(element.MovePathAttribute))
            {
                return element;
            }

            element.Duration = 0;
            return element;
        }

        internal static IReadOnlyList<MovePathElement> ApplyPortalOwnedFlushCadenceHint(
            IReadOnlyList<MovePathElement> path,
            bool isTimeForFlush)
        {
            if (path == null || path.Count <= 1 || isTimeForFlush)
            {
                return path ?? Array.Empty<MovePathElement>();
            }

            return new[] { path[path.Count - 1] };
        }

        internal static IReadOnlyList<MovePathElement> ShapePortalOwnedMovePathForEncode(
            IReadOnlyList<MovePathElement> path,
            bool flushAdmitted,
            IReadOnlyList<MovePathElement> postFlushCarry,
            out bool consumedPostFlushCarry)
        {
            IReadOnlyList<MovePathElement> cadenceShapedPath =
                ApplyPortalOwnedFlushCadenceHintWithRetainedCarrySuffix(
                    path,
                    flushAdmitted,
                    postFlushCarry,
                    out consumedPostFlushCarry);
            cadenceShapedPath = ApplyPortalOwnedPostFlushCarryHint(
                cadenceShapedPath,
                !flushAdmitted && !consumedPostFlushCarry ? postFlushCarry : Array.Empty<MovePathElement>(),
                out bool prependedPostFlushCarry);
            consumedPostFlushCarry |= prependedPostFlushCarry;

            return NormalizeForPortalOwnedClientMakeMovePath(cadenceShapedPath);
        }

        private static IReadOnlyList<MovePathElement> ApplyPortalOwnedFlushCadenceHintWithRetainedCarrySuffix(
            IReadOnlyList<MovePathElement> path,
            bool flushAdmitted,
            IReadOnlyList<MovePathElement> carryPath,
            out bool consumedCarry)
        {
            consumedCarry = false;
            if (path == null || path.Count == 0 || flushAdmitted || carryPath == null || carryPath.Count == 0)
            {
                return ApplyPortalOwnedFlushCadenceHint(path, flushAdmitted);
            }

            if (!TryFindFirstEncodedCarryShapeIndex(path, carryPath, out int carryIndex))
            {
                return ApplyPortalOwnedFlushCadenceHint(path, flushAdmitted);
            }

            consumedCarry = true;
            if (carryIndex <= 0)
            {
                return path;
            }

            MovePathElement[] suffix = new MovePathElement[path.Count - carryIndex];
            for (int i = carryIndex; i < path.Count; i++)
            {
                suffix[i - carryIndex] = path[i];
            }

            return suffix;
        }

        internal static IReadOnlyList<MovePathElement> ApplyPortalOwnedPostFlushCarryHint(
            IReadOnlyList<MovePathElement> path,
            IReadOnlyList<MovePathElement> carryPath,
            out bool consumedCarry)
        {
            consumedCarry = false;
            if (carryPath == null || carryPath.Count == 0)
            {
                return path ?? Array.Empty<MovePathElement>();
            }

            if (path == null || path.Count == 0)
            {
                return Array.Empty<MovePathElement>();
            }

            if (ContainsAllEncodedCarryShapes(path, carryPath))
            {
                consumedCarry = true;
                return path;
            }

            if (path.Count != 1)
            {
                return path;
            }

            List<MovePathElement> merged = new(carryPath.Count + path.Count);
            for (int i = 0; i < carryPath.Count; i++)
            {
                MovePathElement carry = carryPath[i];
                if (HasEncodedShape(path, carry))
                {
                    consumedCarry = true;
                    continue;
                }

                merged.Add(carry);
                consumedCarry = true;
            }

            if (merged.Count == 0)
            {
                return path;
            }

            merged.AddRange(path);
            return merged;
        }

        internal static IReadOnlyList<MovePathElement> CapturePortalOwnedPostFlushCarryHint(
            IReadOnlyList<MovePathElement> flushAdmittedPath)
        {
            if (flushAdmittedPath == null || flushAdmittedPath.Count == 0)
            {
                return Array.Empty<MovePathElement>();
            }

            int lastGroundedIndex = -1;
            for (int i = flushAdmittedPath.Count - 1; i >= 0; i--)
            {
                if (flushAdmittedPath[i].FootholdId > 0)
                {
                    lastGroundedIndex = i;
                    break;
                }
            }

            int carryStartIndex = lastGroundedIndex + 1;
            if (carryStartIndex <= 0 || carryStartIndex >= flushAdmittedPath.Count)
            {
                return Array.Empty<MovePathElement>();
            }

            return new[] { flushAdmittedPath[carryStartIndex] };
        }

        internal static IReadOnlyList<MovePathElement> TrimPortalOwnedClientFlushRetainedTailForEncode(
            IReadOnlyList<MovePathElement> flushAdmittedPath,
            bool retainsPostGroundTail)
        {
            if (!retainsPostGroundTail
                || flushAdmittedPath == null
                || flushAdmittedPath.Count <= 1)
            {
                return flushAdmittedPath ?? Array.Empty<MovePathElement>();
            }

            int lastGroundedIndex = -1;
            for (int i = flushAdmittedPath.Count - 1; i >= 0; i--)
            {
                if (flushAdmittedPath[i].FootholdId > 0)
                {
                    lastGroundedIndex = i;
                    break;
                }
            }

            if (lastGroundedIndex < 0 || lastGroundedIndex >= flushAdmittedPath.Count - 1)
            {
                return flushAdmittedPath;
            }

            MovePathElement[] encoded = new MovePathElement[lastGroundedIndex + 1];
            for (int i = 0; i <= lastGroundedIndex; i++)
            {
                encoded[i] = flushAdmittedPath[i];
            }

            return encoded;
        }

        private static void WriteElement(BinaryWriter writer, MovePathElement element, bool includeClientRandomCounts)
        {
            byte attribute = (byte)Math.Clamp(element.MovePathAttribute, byte.MinValue, byte.MaxValue);
            writer.Write(attribute);
            bool writesCommonMoveSuffix = true;

            switch (attribute)
            {
                case 0:
                case 5:
                case 12:
                case 14:
                case 35:
                case 36:
                    writer.Write(ClampToShort(element.X));
                    writer.Write(ClampToShort(element.Y));
                    writer.Write(element.VelocityX);
                    writer.Write(element.VelocityY);
                    writer.Write(ClampToShort(element.FootholdId));
                    if (attribute == 12)
                    {
                        writer.Write(ClampToShort(element.FallStartFootholdId));
                    }

                    writer.Write(element.XOffset);
                    writer.Write(element.YOffset);
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
                    writer.Write(element.VelocityX);
                    writer.Write(element.VelocityY);
                    break;
                case 3:
                case 4:
                case 6:
                case 7:
                case 8:
                case 10:
                    writer.Write(ClampToShort(element.X));
                    writer.Write(ClampToShort(element.Y));
                    writer.Write(ClampToShort(element.FootholdId));
                    break;
                case 9:
                    writer.Write((byte)0);
                    writesCommonMoveSuffix = false;
                    break;
                case 11:
                    writer.Write(element.VelocityX);
                    writer.Write(element.VelocityY);
                    writer.Write((short)0);
                    break;
                case 17:
                    writer.Write(ClampToShort(element.X));
                    writer.Write(ClampToShort(element.Y));
                    writer.Write(element.VelocityX);
                    writer.Write(element.VelocityY);
                    break;
                case >= 20 and <= 30:
                    break;
                default:
                    break;
            }

            if (!writesCommonMoveSuffix)
            {
                return;
            }

            writer.Write(EncodeMoveAction(element.Action, element.FacingRight));
            writer.Write(element.Duration);
            if (includeClientRandomCounts)
            {
                // Keep the packet-owned suffix shape aligned with client CMovePath payloads.
                writer.Write(element.RandomCount);
                writer.Write(element.ActualRandomCount);
            }
        }

        private static byte EncodeMoveAction(MoveAction action, bool facingRight)
        {
            int actionCode = Math.Clamp((int)action, 0, 0x0F);
            return (byte)((actionCode << 1) | (facingRight ? 0 : 1));
        }

        private static void WriteFlushTail(
            BinaryWriter writer,
            IReadOnlyList<MovePathElement> path,
            MovePathElement start,
            IReadOnlyList<byte> passiveKeyPadStates)
        {
            int stateCount = Math.Clamp(passiveKeyPadStates?.Count ?? 0, 0, byte.MaxValue);
            writer.Write((byte)stateCount);
            for (int i = 0; i < stateCount; i += 2)
            {
                byte low = (byte)(passiveKeyPadStates[i] & 0x0F);
                byte packed = low;
                if (i + 1 < stateCount)
                {
                    packed |= (byte)((passiveKeyPadStates[i + 1] & 0x0F) << 4);
                }

                writer.Write(packed);
            }

            short left = ClampToShort(start.X);
            short top = ClampToShort(start.Y);
            short right = left;
            short bottom = top;

            for (int i = 0; i < path.Count; i++)
            {
                short x = ClampToShort(path[i].X);
                short y = ClampToShort(path[i].Y);
                left = (short)Math.Min(left, x);
                top = (short)Math.Min(top, y);
                right = (short)Math.Max(right, x);
                bottom = (short)Math.Max(bottom, y);
            }

            writer.Write(left);
            writer.Write(top);
            writer.Write(right);
            writer.Write(bottom);
        }

        private static short ClampToShort(int value)
        {
            return (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        }

        private static short ClampDurationToShort(int duration)
        {
            return (short)Math.Clamp(duration, short.MinValue, short.MaxValue);
        }

        private static bool CanReplacePlaceholderTailWithCurrent(MovePathElement tail)
        {
            return tail.MovePathAttribute == 0 && tail.Duration == 0;
        }

        private static bool CanCoalesceClientMakeMovePathTail(MovePathElement tail, MovePathElement current)
        {
            if (!IsClientCoalesceAttribute(current.MovePathAttribute)
                || tail.MovePathAttribute != current.MovePathAttribute
                || tail.FootholdId != current.FootholdId
                || tail.FallStartFootholdId != current.FallStartFootholdId
                || tail.Action != current.Action
                || tail.FacingRight != current.FacingRight
                || tail.XOffset != current.XOffset
                || tail.YOffset != current.YOffset)
            {
                return false;
            }

            return HasStableVelocityDirection(tail.VelocityX, current.VelocityX)
                && HasStableVelocityDirection(tail.VelocityY, current.VelocityY);
        }

        private static bool IsClientCoalesceAttribute(int attribute)
        {
            return attribute == 0 || attribute == 12 || attribute == 14;
        }

        private static bool IsPortalOwnedImpactAttribute(int attribute)
        {
            return attribute == 24 || attribute == 25;
        }

        private static bool HasStableVelocityDirection(short previousVelocity, short currentVelocity)
        {
            if (previousVelocity == 0 || currentVelocity == 0)
            {
                return previousVelocity == currentVelocity;
            }

            return (previousVelocity < 0 && currentVelocity < 0)
                || (previousVelocity > 0 && currentVelocity > 0);
        }

        private static bool HasSameEncodedShape(MovePathElement left, MovePathElement right)
        {
            return left.MovePathAttribute == right.MovePathAttribute
                   && left.X == right.X
                   && left.Y == right.Y
                   && left.VelocityX == right.VelocityX
                   && left.VelocityY == right.VelocityY
                   && left.FootholdId == right.FootholdId
                   && left.FallStartFootholdId == right.FallStartFootholdId
                   && left.Action == right.Action
                   && left.FacingRight == right.FacingRight
                   && left.Duration == right.Duration
                   && left.XOffset == right.XOffset
                   && left.YOffset == right.YOffset
                   && left.RandomCount == right.RandomCount
                   && left.ActualRandomCount == right.ActualRandomCount;
        }

        private static bool HasEncodedShape(IReadOnlyList<MovePathElement> path, MovePathElement candidate)
        {
            for (int i = 0; i < path.Count; i++)
            {
                if (HasSameEncodedShape(path[i], candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAllEncodedCarryShapes(
            IReadOnlyList<MovePathElement> path,
            IReadOnlyList<MovePathElement> carryPath)
        {
            for (int i = 0; i < carryPath.Count; i++)
            {
                if (!HasEncodedShape(path, carryPath[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryFindFirstEncodedCarryShapeIndex(
            IReadOnlyList<MovePathElement> path,
            IReadOnlyList<MovePathElement> carryPath,
            out int carryIndex)
        {
            carryIndex = -1;
            for (int i = 0; i < path.Count; i++)
            {
                for (int j = 0; j < carryPath.Count; j++)
                {
                    if (HasSameEncodedShape(path[i], carryPath[j]))
                    {
                        carryIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
