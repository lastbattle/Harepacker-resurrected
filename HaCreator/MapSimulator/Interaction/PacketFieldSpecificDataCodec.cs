using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum PacketFieldSpecificDataOwnerHint
    {
        None,
        Field,
        Party,
        Session
    }

    internal static class PacketFieldSpecificDataCodec
    {
        internal static bool TryDecodeStringPairs(
            byte[] payload,
            out IReadOnlyList<KeyValuePair<string, string>> pairs,
            out int headerSize)
        {
            pairs = null;
            headerSize = -1;
            payload ??= Array.Empty<byte>();
            if (TryDecodeStringPairs(payload, 1, out pairs))
            {
                headerSize = 1;
                return true;
            }

            if (TryDecodeStringPairs(payload, 2, out pairs))
            {
                headerSize = 2;
                return true;
            }

            if (TryDecodeStringPairs(payload, 4, out pairs))
            {
                headerSize = 4;
                return true;
            }

            if (TryDecodeStringPairs(payload, 0, out pairs))
            {
                headerSize = 0;
                return true;
            }

            return false;
        }

        internal static PacketFieldSpecificDataOwnerHint ResolveOwnerHint(ref string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return PacketFieldSpecificDataOwnerHint.None;
            }

            if (TryStripPrefix(key, "field", out string strippedKey))
            {
                key = strippedKey;
                return PacketFieldSpecificDataOwnerHint.Field;
            }

            if (TryStripPrefix(key, "party", out strippedKey))
            {
                key = strippedKey;
                return PacketFieldSpecificDataOwnerHint.Party;
            }

            if (TryStripPrefix(key, "session", out strippedKey))
            {
                key = strippedKey;
                return PacketFieldSpecificDataOwnerHint.Session;
            }

            return PacketFieldSpecificDataOwnerHint.None;
        }

        private static bool TryDecodeStringPairs(
            byte[] payload,
            int headerSize,
            out IReadOnlyList<KeyValuePair<string, string>> pairs)
        {
            pairs = null;
            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                int declaredCount = 0;
                if (headerSize == 1)
                {
                    declaredCount = reader.ReadByte();
                }
                else if (headerSize == 2)
                {
                    declaredCount = reader.ReadUInt16();
                }
                else if (headerSize == 4)
                {
                    declaredCount = reader.ReadInt32();
                }

                List<KeyValuePair<string, string>> decoded = new();
                if (headerSize == 0)
                {
                    while (stream.Position < stream.Length)
                    {
                        string key = ReadMapleString(reader);
                        string value = ReadMapleString(reader);
                        decoded.Add(new KeyValuePair<string, string>(key, value));
                    }
                }
                else
                {
                    if (declaredCount <= 0 || declaredCount > 32)
                    {
                        return false;
                    }

                    for (int i = 0; i < declaredCount; i++)
                    {
                        string key = ReadMapleString(reader);
                        string value = ReadMapleString(reader);
                        decoded.Add(new KeyValuePair<string, string>(key, value));
                    }
                }

                if (decoded.Count == 0 || stream.Position != stream.Length || decoded.Any(static pair => string.IsNullOrWhiteSpace(pair.Key)))
                {
                    return false;
                }

                pairs = decoded;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is ArgumentException)
            {
                return false;
            }
        }

        private static bool TryStripPrefix(string value, string prefix, out string stripped)
        {
            stripped = null;
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(prefix) || value.Length <= prefix.Length)
            {
                return false;
            }

            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            char separator = value[prefix.Length];
            if (separator is not (':' or '/' or '.'))
            {
                return false;
            }

            stripped = value[(prefix.Length + 1)..].Trim();
            return !string.IsNullOrWhiteSpace(stripped);
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            ushort length = reader.ReadUInt16();
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Field-specific data string ended before its declared Maple-string length.");
            }

            return Encoding.Default.GetString(bytes);
        }
    }
}
