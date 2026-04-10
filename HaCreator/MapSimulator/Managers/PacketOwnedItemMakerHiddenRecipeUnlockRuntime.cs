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

        internal static int MergePendingEntries(
            List<PacketOwnedItemMakerHiddenRecipeUnlockEntry> pendingEntries,
            PacketOwnedItemMakerHiddenRecipeUnlock packetUnlock)
        {
            if (pendingEntries == null)
            {
                return 0;
            }

            if (packetUnlock?.Entries == null || packetUnlock.Entries.Count == 0)
            {
                return pendingEntries.Count;
            }

            HashSet<(int BucketKey, int OutputItemId)> seen = new();
            for (int i = 0; i < pendingEntries.Count; i++)
            {
                PacketOwnedItemMakerHiddenRecipeUnlockEntry existing = pendingEntries[i];
                if (existing.OutputItemId > 0)
                {
                    seen.Add((existing.BucketKey, existing.OutputItemId));
                }
            }

            for (int i = 0; i < packetUnlock.Entries.Count; i++)
            {
                PacketOwnedItemMakerHiddenRecipeUnlockEntry entry = packetUnlock.Entries[i];
                if (entry.OutputItemId <= 0 || !seen.Add((entry.BucketKey, entry.OutputItemId)))
                {
                    continue;
                }

                pendingEntries.Add(entry);
            }

            return pendingEntries.Count;
        }

        internal static PacketOwnedItemMakerHiddenRecipeUnlock CreateReplayPayload(
            IReadOnlyList<PacketOwnedItemMakerHiddenRecipeUnlockEntry> pendingEntries)
        {
            return new PacketOwnedItemMakerHiddenRecipeUnlock
            {
                Entries = pendingEntries?.Count > 0
                    ? new List<PacketOwnedItemMakerHiddenRecipeUnlockEntry>(pendingEntries)
                    : Array.Empty<PacketOwnedItemMakerHiddenRecipeUnlockEntry>()
            };
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
