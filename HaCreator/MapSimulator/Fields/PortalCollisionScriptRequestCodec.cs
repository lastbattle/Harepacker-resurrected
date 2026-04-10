using System;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Fields
{
    internal static class PortalCollisionScriptRequestCodec
    {
        public const int Opcode = 112;
        public const byte SyntheticFieldKey = 0;

        public static bool TryBuildPayload(
            byte fieldKey,
            string portalName,
            float x,
            float y,
            out byte[] payload)
        {
            payload = Array.Empty<byte>();
            string normalizedPortalName = portalName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedPortalName))
            {
                return false;
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write(fieldKey);
            WriteMapleString(writer, normalizedPortalName);
            writer.Write((short)Math.Clamp((int)MathF.Round(x), short.MinValue, short.MaxValue));
            writer.Write((short)Math.Clamp((int)MathF.Round(y), short.MinValue, short.MaxValue));
            writer.Flush();
            payload = stream.ToArray();
            return true;
        }

        private static void WriteMapleString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.Default.GetBytes(value ?? string.Empty);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }
    }
}
