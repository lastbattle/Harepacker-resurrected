using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    internal readonly record struct PacketOwnedItemMakerHiddenRecipeUnlockEntry(int BucketKey, int OutputItemId);

    internal sealed class PacketOwnedItemMakerHiddenRecipeUnlock
    {
        public IReadOnlyList<PacketOwnedItemMakerHiddenRecipeUnlockEntry> Entries { get; init; }
            = Array.Empty<PacketOwnedItemMakerHiddenRecipeUnlockEntry>();
    }

    internal static class PacketOwnedItemMakerHiddenRecipeUnlockRuntime
    {
        public const int PacketType = 1019;

        public static bool TryDecode(byte[] payload, out PacketOwnedItemMakerHiddenRecipeUnlock result, out string error)
        {
            result = null;
            error = null;

            if (payload == null || payload.Length < sizeof(int))
            {
                error = "Maker-hidden-unlock payload must include at least the Int32 entry count.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);

                int count = reader.ReadInt32();
                if (count < 0)
                {
                    error = "Maker-hidden-unlock entry count cannot be negative.";
                    return false;
                }

                List<PacketOwnedItemMakerHiddenRecipeUnlockEntry> entries = new(count);
                HashSet<(int BucketKey, int OutputItemId)> seen = new();
                for (int i = 0; i < count; i++)
                {
                    EnsureReadable(reader, sizeof(int) * 2, "Maker-hidden-unlock entry payload is truncated.");
                    int bucketKey = reader.ReadInt32();
                    int outputItemId = reader.ReadInt32();
                    if (outputItemId <= 0)
                    {
                        throw new InvalidDataException("Maker-hidden-unlock entries must include a positive output item id.");
                    }

                    (int BucketKey, int OutputItemId) key = (bucketKey, outputItemId);
                    if (seen.Add(key))
                    {
                        entries.Add(new PacketOwnedItemMakerHiddenRecipeUnlockEntry(bucketKey, outputItemId));
                    }
                }

                if (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    error = $"Maker-hidden-unlock payload left {reader.BaseStream.Length - reader.BaseStream.Position} unread byte(s).";
                    return false;
                }

                result = new PacketOwnedItemMakerHiddenRecipeUnlock
                {
                    Entries = entries
                };
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException or InvalidDataException)
            {
                error = $"Maker-hidden-unlock payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static void EnsureReadable(BinaryReader reader, int requiredBytes, string message)
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < requiredBytes)
            {
                throw new InvalidDataException(message);
            }
        }
    }
}
