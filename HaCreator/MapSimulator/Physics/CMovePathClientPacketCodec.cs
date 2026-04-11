using System;
using System.Collections.Generic;
using System.IO;

namespace HaCreator.MapSimulator.Physics
{
    internal static class CMovePathClientPacketCodec
    {
        public static bool TryEncode(
            IReadOnlyList<MovePathElement> path,
            out byte[] payload,
            out string error,
            bool includeClientRandomCounts = false)
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

            writer.Flush();
            payload = stream.ToArray();
            return true;
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
                        writer.Write((short)0);
                    }

                    writer.Write((short)0);
                    writer.Write((short)0);
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
                writer.Write((short)0);
                writer.Write((short)0);
            }
        }

        private static byte EncodeMoveAction(MoveAction action, bool facingRight)
        {
            int actionCode = Math.Clamp((int)action, 0, 0x0F);
            return (byte)((actionCode << 1) | (facingRight ? 0 : 1));
        }

        private static short ClampToShort(int value)
        {
            return (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        }
    }
}
