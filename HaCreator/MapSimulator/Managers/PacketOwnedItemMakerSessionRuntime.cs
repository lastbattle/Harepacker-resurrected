using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    [Flags]
    internal enum PacketOwnedItemMakerSessionFlags
    {
        None = 0,
        ServerOwnsCraftExecution = 1 << 0,
        HasAuthoritativeDisassemblyTargets = 1 << 1,
        HasAuthoritativeHiddenRecipeList = 1 << 2
    }

    internal readonly record struct PacketOwnedItemMakerDisassemblyTargetEntry(int SlotIndex, int ItemId);
    internal readonly record struct PacketOwnedItemMakerSessionHiddenEntry(int BucketKey, int OutputItemId);

    internal sealed class PacketOwnedItemMakerSession
    {
        public PacketOwnedItemMakerSessionFlags Flags { get; init; }
        public IReadOnlyList<PacketOwnedItemMakerDisassemblyTargetEntry> DisassemblyTargets { get; init; }
            = Array.Empty<PacketOwnedItemMakerDisassemblyTargetEntry>();
        public IReadOnlyList<PacketOwnedItemMakerSessionHiddenEntry> HiddenRecipeEntries { get; init; }
            = Array.Empty<PacketOwnedItemMakerSessionHiddenEntry>();

        public bool ServerOwnsCraftExecution => Flags.HasFlag(PacketOwnedItemMakerSessionFlags.ServerOwnsCraftExecution);
        public bool HasAuthoritativeDisassemblyTargets => Flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeDisassemblyTargets);
        public bool HasAuthoritativeHiddenRecipeList => Flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeHiddenRecipeList);
    }

    internal static class PacketOwnedItemMakerSessionRuntime
    {
        public const int PacketType = 1024;

        public static bool TryDecode(byte[] payload, out PacketOwnedItemMakerSession result, out string error)
        {
            result = null;
            error = null;

            if (payload == null || payload.Length < sizeof(int))
            {
                error = "Maker-session payload must include at least the Int32 session flags.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);

                PacketOwnedItemMakerSessionFlags flags = (PacketOwnedItemMakerSessionFlags)reader.ReadInt32();
                List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargets = null;
                List<PacketOwnedItemMakerSessionHiddenEntry> hiddenEntries = null;

                if (flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeDisassemblyTargets))
                {
                    int count = ReadNonNegativeCount(reader, "Maker-session disassembly target count is missing or negative.");
                    if (count > 0)
                    {
                        disassemblyTargets = new List<PacketOwnedItemMakerDisassemblyTargetEntry>(count);
                        for (int i = 0; i < count; i++)
                        {
                            EnsureReadable(reader, sizeof(int) * 2, "Maker-session disassembly target entry is truncated.");
                            int slotIndex = reader.ReadInt32();
                            int itemId = reader.ReadInt32();
                            if (slotIndex < 0)
                            {
                                throw new InvalidDataException("Maker-session disassembly target slots must be zero-based and non-negative.");
                            }

                            if (itemId <= 0)
                            {
                                throw new InvalidDataException("Maker-session disassembly target entries must include a positive item id.");
                            }

                            disassemblyTargets.Add(new PacketOwnedItemMakerDisassemblyTargetEntry(slotIndex, itemId));
                        }
                    }
                }

                if (flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeHiddenRecipeList))
                {
                    int count = ReadNonNegativeCount(reader, "Maker-session hidden recipe count is missing or negative.");
                    if (count > 0)
                    {
                        hiddenEntries = new List<PacketOwnedItemMakerSessionHiddenEntry>(count);
                        for (int i = 0; i < count; i++)
                        {
                            EnsureReadable(reader, sizeof(int) * 2, "Maker-session hidden recipe entry is truncated.");
                            int bucketKey = reader.ReadInt32();
                            int outputItemId = reader.ReadInt32();
                            if (outputItemId <= 0)
                            {
                                throw new InvalidDataException("Maker-session hidden recipe entries must include a positive output item id.");
                            }

                            hiddenEntries.Add(new PacketOwnedItemMakerSessionHiddenEntry(bucketKey, outputItemId));
                        }
                    }
                }

                if (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    error = $"Maker-session payload left {reader.BaseStream.Length - reader.BaseStream.Position} unread byte(s).";
                    return false;
                }

                result = new PacketOwnedItemMakerSession
                {
                    Flags = flags,
                    DisassemblyTargets = disassemblyTargets ?? (IReadOnlyList<PacketOwnedItemMakerDisassemblyTargetEntry>)Array.Empty<PacketOwnedItemMakerDisassemblyTargetEntry>(),
                    HiddenRecipeEntries = hiddenEntries ?? (IReadOnlyList<PacketOwnedItemMakerSessionHiddenEntry>)Array.Empty<PacketOwnedItemMakerSessionHiddenEntry>()
                };
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException or InvalidDataException)
            {
                error = $"Maker-session payload could not be decoded: {ex.Message}";
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

        private static int ReadNonNegativeCount(BinaryReader reader, string message)
        {
            EnsureReadable(reader, sizeof(int), message);
            int count = reader.ReadInt32();
            if (count < 0)
            {
                throw new InvalidDataException(message);
            }

            return count;
        }
    }
}
