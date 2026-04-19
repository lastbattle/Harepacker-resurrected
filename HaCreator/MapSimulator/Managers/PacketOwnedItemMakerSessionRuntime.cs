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

    [Flags]
    internal enum PacketOwnedItemMakerSessionDeltaFlags
    {
        None = 0,
        HasServerOwnsCraftExecutionValue = 1 << 0,
        ServerOwnsCraftExecutionValue = 1 << 1,
        HasAuthoritativeDisassemblyTargetsValue = 1 << 2,
        AuthoritativeDisassemblyTargetsValue = 1 << 3,
        ClearDisassemblyTargets = 1 << 4,
        HasDisassemblyTargetAdditions = 1 << 5,
        HasDisassemblyTargetRemovals = 1 << 6,
        HasAuthoritativeHiddenRecipeListValue = 1 << 7,
        AuthoritativeHiddenRecipeListValue = 1 << 8,
        ClearHiddenRecipeEntries = 1 << 9,
        HasHiddenRecipeAdditions = 1 << 10,
        HasHiddenRecipeRemovals = 1 << 11,
        ClearAll = 1 << 12
    }

    internal sealed class PacketOwnedItemMakerSession
    {
        public PacketOwnedItemMakerSessionFlags Flags { get; init; }
        public bool IsDeltaUpdate { get; init; }
        public PacketOwnedItemMakerSessionDeltaFlags DeltaFlags { get; init; }
        public IReadOnlyList<PacketOwnedItemMakerDisassemblyTargetEntry> DisassemblyTargets { get; init; }
            = Array.Empty<PacketOwnedItemMakerDisassemblyTargetEntry>();
        public IReadOnlyList<PacketOwnedItemMakerSessionHiddenEntry> HiddenRecipeEntries { get; init; }
            = Array.Empty<PacketOwnedItemMakerSessionHiddenEntry>();
        public IReadOnlyList<PacketOwnedItemMakerDisassemblyTargetEntry> DisassemblyTargetAdditions { get; init; }
            = Array.Empty<PacketOwnedItemMakerDisassemblyTargetEntry>();
        public IReadOnlyList<PacketOwnedItemMakerDisassemblyTargetEntry> DisassemblyTargetRemovals { get; init; }
            = Array.Empty<PacketOwnedItemMakerDisassemblyTargetEntry>();
        public IReadOnlyList<PacketOwnedItemMakerSessionHiddenEntry> HiddenRecipeAdditions { get; init; }
            = Array.Empty<PacketOwnedItemMakerSessionHiddenEntry>();
        public IReadOnlyList<PacketOwnedItemMakerSessionHiddenEntry> HiddenRecipeRemovals { get; init; }
            = Array.Empty<PacketOwnedItemMakerSessionHiddenEntry>();

        public bool ServerOwnsCraftExecution => IsDeltaUpdate
            ? DeltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.ServerOwnsCraftExecutionValue)
            : Flags.HasFlag(PacketOwnedItemMakerSessionFlags.ServerOwnsCraftExecution);
        public bool HasAuthoritativeDisassemblyTargets => IsDeltaUpdate
            ? DeltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.AuthoritativeDisassemblyTargetsValue)
            : Flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeDisassemblyTargets);
        public bool HasAuthoritativeHiddenRecipeList => IsDeltaUpdate
            ? DeltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.AuthoritativeHiddenRecipeListValue)
            : Flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeHiddenRecipeList);
        public bool HasServerOwnsCraftExecutionOverride =>
            IsDeltaUpdate && DeltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasServerOwnsCraftExecutionValue);
        public bool HasAuthoritativeDisassemblyTargetsOverride =>
            IsDeltaUpdate && DeltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasAuthoritativeDisassemblyTargetsValue);
        public bool HasAuthoritativeHiddenRecipeListOverride =>
            IsDeltaUpdate && DeltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasAuthoritativeHiddenRecipeListValue);
        public bool ClearsAllState =>
            IsDeltaUpdate && DeltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.ClearAll);
        public bool ClearsDisassemblyTargets =>
            IsDeltaUpdate && DeltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.ClearDisassemblyTargets);
        public bool ClearsHiddenRecipeEntries =>
            IsDeltaUpdate && DeltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.ClearHiddenRecipeEntries);
    }

    internal static class PacketOwnedItemMakerSessionRuntime
    {
        public const int PacketType = 1024;
        internal const int DeltaPacketMagic = 0x54444B4D; // "MKDT"
        private const byte CompactDeltaMarker = 0x80;

        public static bool TryDecode(byte[] payload, out PacketOwnedItemMakerSession result, out string error)
        {
            result = null;
            error = null;

            if (payload == null || payload.Length == 0)
            {
                error = "Maker-session payload must include at least the Int32 session flags.";
                return false;
            }

            IReadOnlyList<PacketOwnedPayloadEnvelopeRuntime.Candidate> decodeCandidates =
                PacketOwnedPayloadEnvelopeRuntime.EnumerateDecodeCandidates(payload, (ushort)PacketType);
            if (decodeCandidates.Count == 0)
            {
                error = "Maker-session payload must include at least the Int32 session flags.";
                return false;
            }

            string firstDecodeError = null;
            for (int i = 0; i < decodeCandidates.Count; i++)
            {
                PacketOwnedPayloadEnvelopeRuntime.Candidate candidate = decodeCandidates[i];
                if (TryDecodeCore(candidate.Payload, out result, out error))
                {
                    return true;
                }

                firstDecodeError ??= error;
            }

            error = firstDecodeError ?? error ?? "Maker-session payload could not be decoded.";
            return false;
        }

        private static bool TryDecodeCore(byte[] payload, out PacketOwnedItemMakerSession result, out string error)
        {
            if (TryDecodeInt32Envelope(payload, out result, out error))
            {
                return true;
            }

            string intEnvelopeError = error;
            if (TryDecodeCompactEnvelope(payload, out result, out error))
            {
                return true;
            }

            error = intEnvelopeError ?? error ?? "Maker-session payload could not be decoded.";
            return false;
        }

        private static bool TryDecodeInt32Envelope(byte[] payload, out PacketOwnedItemMakerSession result, out string error)
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

                if (payload.Length >= sizeof(int) * 2 && reader.ReadInt32() == DeltaPacketMagic)
                {
                    return TryDecodeDeltaPayload(reader, out result, out error);
                }

                reader.BaseStream.Position = 0;

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

        private static bool TryDecodeCompactEnvelope(byte[] payload, out PacketOwnedItemMakerSession result, out string error)
        {
            result = null;
            error = null;

            if (payload == null || payload.Length < sizeof(byte))
            {
                error = "Maker-session compact payload must include at least the Byte session flags.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);

                byte compactFlags = reader.ReadByte();
                bool isDelta = (compactFlags & CompactDeltaMarker) != 0;
                if (isDelta)
                {
                    return TryDecodeCompactDeltaPayload(reader, out result, out error);
                }

                PacketOwnedItemMakerSessionFlags flags = (PacketOwnedItemMakerSessionFlags)(compactFlags & 0x7F);
                List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargets = null;
                List<PacketOwnedItemMakerSessionHiddenEntry> hiddenEntries = null;

                if (flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeDisassemblyTargets))
                {
                    int count = ReadNonNegativeCount16(reader, "Maker-session compact disassembly target count is missing or negative.");
                    if (count > 0)
                    {
                        disassemblyTargets = new List<PacketOwnedItemMakerDisassemblyTargetEntry>(count);
                        for (int i = 0; i < count; i++)
                        {
                            EnsureReadable(reader, sizeof(int) * 2, "Maker-session compact disassembly target entry is truncated.");
                            int slotIndex = reader.ReadInt32();
                            int itemId = reader.ReadInt32();
                            if (slotIndex < 0)
                            {
                                throw new InvalidDataException("Maker-session compact disassembly target slots must be zero-based and non-negative.");
                            }

                            if (itemId <= 0)
                            {
                                throw new InvalidDataException("Maker-session compact disassembly target entries must include a positive item id.");
                            }

                            disassemblyTargets.Add(new PacketOwnedItemMakerDisassemblyTargetEntry(slotIndex, itemId));
                        }
                    }
                }

                if (flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeHiddenRecipeList))
                {
                    int count = ReadNonNegativeCount16(reader, "Maker-session compact hidden recipe count is missing or negative.");
                    if (count > 0)
                    {
                        hiddenEntries = new List<PacketOwnedItemMakerSessionHiddenEntry>(count);
                        for (int i = 0; i < count; i++)
                        {
                            EnsureReadable(reader, sizeof(int) * 2, "Maker-session compact hidden recipe entry is truncated.");
                            int bucketKey = reader.ReadInt32();
                            int outputItemId = reader.ReadInt32();
                            if (outputItemId <= 0)
                            {
                                throw new InvalidDataException("Maker-session compact hidden recipe entries must include a positive output item id.");
                            }

                            hiddenEntries.Add(new PacketOwnedItemMakerSessionHiddenEntry(bucketKey, outputItemId));
                        }
                    }
                }

                if (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    error = $"Maker-session compact payload left {reader.BaseStream.Length - reader.BaseStream.Position} unread byte(s).";
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
                error = $"Maker-session compact payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static bool TryDecodeCompactDeltaPayload(
            BinaryReader reader,
            out PacketOwnedItemMakerSession result,
            out string error)
        {
            result = null;
            error = null;

            EnsureReadable(reader, sizeof(byte), "Maker-session compact delta payload is missing its Byte delta flags.");
            byte compactDeltaFlags = reader.ReadByte();
            PacketOwnedItemMakerSessionDeltaFlags deltaFlags = PacketOwnedItemMakerSessionDeltaFlags.None;

            if ((compactDeltaFlags & 0x01) != 0)
            {
                EnsureReadable(reader, sizeof(byte), "Maker-session compact delta payload is missing its server-owned-craft override value.");
                deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.HasServerOwnsCraftExecutionValue;
                if (reader.ReadByte() != 0)
                {
                    deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.ServerOwnsCraftExecutionValue;
                }
            }

            if ((compactDeltaFlags & 0x02) != 0)
            {
                EnsureReadable(reader, sizeof(byte), "Maker-session compact delta payload is missing its disassembly-list override value.");
                deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.HasAuthoritativeDisassemblyTargetsValue;
                if (reader.ReadByte() != 0)
                {
                    deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.AuthoritativeDisassemblyTargetsValue;
                }
            }

            if ((compactDeltaFlags & 0x04) != 0)
            {
                deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.ClearDisassemblyTargets;
            }

            List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetAdditions = null;
            if ((compactDeltaFlags & 0x08) != 0)
            {
                deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.HasDisassemblyTargetAdditions;
                disassemblyTargetAdditions = ReadDisassemblyTargetEntries16(
                    reader,
                    "Maker-session compact delta disassembly target addition");
            }

            List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetRemovals = null;
            if ((compactDeltaFlags & 0x10) != 0)
            {
                deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.HasDisassemblyTargetRemovals;
                disassemblyTargetRemovals = ReadDisassemblyTargetEntries16(
                    reader,
                    "Maker-session compact delta disassembly target removal");
            }

            if ((compactDeltaFlags & 0x20) != 0)
            {
                EnsureReadable(reader, sizeof(byte), "Maker-session compact delta payload is missing its hidden-list override value.");
                deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.HasAuthoritativeHiddenRecipeListValue;
                if (reader.ReadByte() != 0)
                {
                    deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.AuthoritativeHiddenRecipeListValue;
                }
            }

            if ((compactDeltaFlags & 0x40) != 0)
            {
                deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.ClearHiddenRecipeEntries;
            }

            if ((compactDeltaFlags & 0x80) != 0)
            {
                deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.ClearAll;
            }

            List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeAdditions = null;
            List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeRemovals = null;
            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                // Some maker-session compact delta variants append hidden-list deltas without
                // setting the hidden-list override bit. Keep this tail parser permissive and let
                // state-runtime semantics decide whether entries mutate the authoritative roster.
                hiddenRecipeAdditions = ReadHiddenRecipeEntries16(
                    reader,
                    "Maker-session compact delta hidden recipe addition");
                if (hiddenRecipeAdditions?.Count > 0)
                {
                    deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.HasHiddenRecipeAdditions;
                }
            }

            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                hiddenRecipeRemovals = ReadHiddenRecipeRemovalEntries16(
                    reader,
                    "Maker-session compact delta hidden recipe removal");
                if (hiddenRecipeRemovals?.Count > 0)
                {
                    deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.HasHiddenRecipeRemovals;
                }
            }

            if (reader.BaseStream.Position != reader.BaseStream.Length)
            {
                error = $"Maker-session compact delta payload left {reader.BaseStream.Length - reader.BaseStream.Position} unread byte(s).";
                return false;
            }

            result = new PacketOwnedItemMakerSession
            {
                IsDeltaUpdate = true,
                DeltaFlags = deltaFlags,
                DisassemblyTargetAdditions = disassemblyTargetAdditions ?? (IReadOnlyList<PacketOwnedItemMakerDisassemblyTargetEntry>)Array.Empty<PacketOwnedItemMakerDisassemblyTargetEntry>(),
                DisassemblyTargetRemovals = disassemblyTargetRemovals ?? (IReadOnlyList<PacketOwnedItemMakerDisassemblyTargetEntry>)Array.Empty<PacketOwnedItemMakerDisassemblyTargetEntry>(),
                HiddenRecipeAdditions = hiddenRecipeAdditions ?? (IReadOnlyList<PacketOwnedItemMakerSessionHiddenEntry>)Array.Empty<PacketOwnedItemMakerSessionHiddenEntry>(),
                HiddenRecipeRemovals = hiddenRecipeRemovals ?? (IReadOnlyList<PacketOwnedItemMakerSessionHiddenEntry>)Array.Empty<PacketOwnedItemMakerSessionHiddenEntry>()
            };
            return true;
        }

        private static bool TryDecodeDeltaPayload(
            BinaryReader reader,
            out PacketOwnedItemMakerSession result,
            out string error)
        {
            result = null;
            error = null;

            EnsureReadable(reader, sizeof(int), "Maker-session delta payload is missing its Int32 delta flags.");
            PacketOwnedItemMakerSessionDeltaFlags deltaFlags = (PacketOwnedItemMakerSessionDeltaFlags)reader.ReadInt32();

            List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetAdditions = null;
            List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetRemovals = null;
            List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeAdditions = null;
            List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeRemovals = null;

            if (deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasDisassemblyTargetAdditions))
            {
                disassemblyTargetAdditions = ReadDisassemblyTargetEntries(reader, "Maker-session delta disassembly target addition");
            }

            if (deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasDisassemblyTargetRemovals))
            {
                disassemblyTargetRemovals = ReadDisassemblyTargetEntries(reader, "Maker-session delta disassembly target removal");
            }

            if (deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasHiddenRecipeAdditions))
            {
                hiddenRecipeAdditions = ReadHiddenRecipeEntries(reader, "Maker-session delta hidden recipe addition");
            }

            if (deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasHiddenRecipeRemovals))
            {
                hiddenRecipeRemovals = ReadHiddenRecipeRemovalEntries(reader, "Maker-session delta hidden recipe removal");
            }

            if (reader.BaseStream.Position != reader.BaseStream.Length)
            {
                error = $"Maker-session payload left {reader.BaseStream.Length - reader.BaseStream.Position} unread byte(s).";
                return false;
            }

            result = new PacketOwnedItemMakerSession
            {
                IsDeltaUpdate = true,
                DeltaFlags = deltaFlags,
                DisassemblyTargetAdditions = disassemblyTargetAdditions ?? (IReadOnlyList<PacketOwnedItemMakerDisassemblyTargetEntry>)Array.Empty<PacketOwnedItemMakerDisassemblyTargetEntry>(),
                DisassemblyTargetRemovals = disassemblyTargetRemovals ?? (IReadOnlyList<PacketOwnedItemMakerDisassemblyTargetEntry>)Array.Empty<PacketOwnedItemMakerDisassemblyTargetEntry>(),
                HiddenRecipeAdditions = hiddenRecipeAdditions ?? (IReadOnlyList<PacketOwnedItemMakerSessionHiddenEntry>)Array.Empty<PacketOwnedItemMakerSessionHiddenEntry>(),
                HiddenRecipeRemovals = hiddenRecipeRemovals ?? (IReadOnlyList<PacketOwnedItemMakerSessionHiddenEntry>)Array.Empty<PacketOwnedItemMakerSessionHiddenEntry>()
            };
            return true;
        }

        private static List<PacketOwnedItemMakerDisassemblyTargetEntry> ReadDisassemblyTargetEntries(BinaryReader reader, string label)
        {
            int count = ReadNonNegativeCount(reader, $"{label} count is missing or negative.");
            if (count <= 0)
            {
                return null;
            }

            List<PacketOwnedItemMakerDisassemblyTargetEntry> entries = new(count);
            for (int i = 0; i < count; i++)
            {
                EnsureReadable(reader, sizeof(int) * 2, $"{label} entry is truncated.");
                int slotIndex = reader.ReadInt32();
                int itemId = reader.ReadInt32();
                if (slotIndex < 0)
                {
                    throw new InvalidDataException($"{label} slots must be zero-based and non-negative.");
                }

                if (itemId <= 0)
                {
                    throw new InvalidDataException($"{label} entries must include a positive item id.");
                }

                entries.Add(new PacketOwnedItemMakerDisassemblyTargetEntry(slotIndex, itemId));
            }

            return entries;
        }

        private static List<PacketOwnedItemMakerDisassemblyTargetEntry> ReadDisassemblyTargetEntries16(BinaryReader reader, string label)
        {
            int count = ReadNonNegativeCount16(reader, $"{label} count is missing or negative.");
            if (count <= 0)
            {
                return null;
            }

            List<PacketOwnedItemMakerDisassemblyTargetEntry> entries = new(count);
            for (int i = 0; i < count; i++)
            {
                EnsureReadable(reader, sizeof(int) * 2, $"{label} entry is truncated.");
                int slotIndex = reader.ReadInt32();
                int itemId = reader.ReadInt32();
                if (slotIndex < 0)
                {
                    throw new InvalidDataException($"{label} slots must be zero-based and non-negative.");
                }

                if (itemId <= 0)
                {
                    throw new InvalidDataException($"{label} entries must include a positive item id.");
                }

                entries.Add(new PacketOwnedItemMakerDisassemblyTargetEntry(slotIndex, itemId));
            }

            return entries;
        }

        private static List<PacketOwnedItemMakerSessionHiddenEntry> ReadHiddenRecipeEntries(BinaryReader reader, string label)
        {
            int count = ReadNonNegativeCount(reader, $"{label} count is missing or negative.");
            if (count <= 0)
            {
                return null;
            }

            List<PacketOwnedItemMakerSessionHiddenEntry> entries = new(count);
            for (int i = 0; i < count; i++)
            {
                EnsureReadable(reader, sizeof(int) * 2, $"{label} entry is truncated.");
                int bucketKey = reader.ReadInt32();
                int outputItemId = reader.ReadInt32();
                if (outputItemId <= 0)
                {
                    throw new InvalidDataException($"{label} entries must include a positive output item id.");
                }

                entries.Add(new PacketOwnedItemMakerSessionHiddenEntry(bucketKey, outputItemId));
            }

            return entries;
        }

        private static List<PacketOwnedItemMakerSessionHiddenEntry> ReadHiddenRecipeRemovalEntries(BinaryReader reader, string label)
        {
            int count = ReadNonNegativeCount(reader, $"{label} count is missing or negative.");
            if (count <= 0)
            {
                return null;
            }

            List<PacketOwnedItemMakerSessionHiddenEntry> entries = new(count);
            for (int i = 0; i < count; i++)
            {
                EnsureReadable(reader, sizeof(int) * 2, $"{label} entry is truncated.");
                int bucketKey = reader.ReadInt32();
                int outputItemId = reader.ReadInt32();
                entries.Add(new PacketOwnedItemMakerSessionHiddenEntry(bucketKey, outputItemId));
            }

            return entries;
        }

        private static List<PacketOwnedItemMakerSessionHiddenEntry> ReadHiddenRecipeEntries16(BinaryReader reader, string label)
        {
            int count = ReadNonNegativeCount16(reader, $"{label} count is missing or negative.");
            if (count <= 0)
            {
                return null;
            }

            List<PacketOwnedItemMakerSessionHiddenEntry> entries = new(count);
            for (int i = 0; i < count; i++)
            {
                EnsureReadable(reader, sizeof(int) * 2, $"{label} entry is truncated.");
                int bucketKey = reader.ReadInt32();
                int outputItemId = reader.ReadInt32();
                if (outputItemId <= 0)
                {
                    throw new InvalidDataException($"{label} entries must include a positive output item id.");
                }

                entries.Add(new PacketOwnedItemMakerSessionHiddenEntry(bucketKey, outputItemId));
            }

            return entries;
        }

        private static List<PacketOwnedItemMakerSessionHiddenEntry> ReadHiddenRecipeRemovalEntries16(BinaryReader reader, string label)
        {
            int count = ReadNonNegativeCount16(reader, $"{label} count is missing or negative.");
            if (count <= 0)
            {
                return null;
            }

            List<PacketOwnedItemMakerSessionHiddenEntry> entries = new(count);
            for (int i = 0; i < count; i++)
            {
                EnsureReadable(reader, sizeof(int) * 2, $"{label} entry is truncated.");
                int bucketKey = reader.ReadInt32();
                int outputItemId = reader.ReadInt32();
                entries.Add(new PacketOwnedItemMakerSessionHiddenEntry(bucketKey, outputItemId));
            }

            return entries;
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

        private static int ReadNonNegativeCount16(BinaryReader reader, string message)
        {
            EnsureReadable(reader, sizeof(ushort), message);
            return reader.ReadUInt16();
        }
    }
}
