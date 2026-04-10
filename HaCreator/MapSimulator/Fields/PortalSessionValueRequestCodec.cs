using System;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Fields
{
    internal static class PortalSessionValueRequestCodec
    {
        public const int Opcode = 191;
        public const byte RequestResetFlag = 0;

        public static bool TryBuildPayload(string key, out byte[] payload)
        {
            payload = Array.Empty<byte>();
            string normalizedKey = key?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return false;
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            WriteMapleString(writer, normalizedKey);
            writer.Write(RequestResetFlag);
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
