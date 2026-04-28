using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using BinaryReader = MapleLib.PacketLib.PacketReader;
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
        private const byte CompactHiddenTailHasAdditions = 0x01;
        private const byte CompactHiddenTailHasRemovals = 0x02;
        private const byte CompactHiddenTailClearHidden = 0x04;
        private enum DisassemblyTargetEntryEncoding
        {
            WideSlotInt32,
            CompactSlotUInt16,
            WideSlotOnlyInt32,
            CompactSlotOnlyUInt16
        }

        private enum HiddenRecipeEntryEncoding
        {
            Pair,
            CompactBucketUInt16,
            CompactBucketByte,
            OutputOnly
        }

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
                    long deltaPayloadStart = reader.BaseStream.Position;
                    if (TryDecodeDeltaPayload(reader, out result, out error))
                    {
                        return true;
                    }

                    string deltaDecodeError = error;
                    reader.BaseStream.Position = deltaPayloadStart;
                    if (TryDecodeCompactDeltaPayload(reader, out result, out error))
                    {
                        return true;
                    }

                    error = deltaDecodeError ?? error;
                    return false;
                }

                reader.BaseStream.Position = 0;

                PacketOwnedItemMakerSessionFlags flags = (PacketOwnedItemMakerSessionFlags)reader.ReadInt32();
                long payloadStart = reader.BaseStream.Position;
                string firstDecodeError = null;
                DisassemblyTargetEntryEncoding[] disassemblyEncodings = flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeDisassemblyTargets)
                    ? new[]
                    {
                        DisassemblyTargetEntryEncoding.WideSlotInt32,
                        DisassemblyTargetEntryEncoding.WideSlotOnlyInt32,
                        DisassemblyTargetEntryEncoding.CompactSlotUInt16,
                        DisassemblyTargetEntryEncoding.CompactSlotOnlyUInt16
                    }
                    : new[] { DisassemblyTargetEntryEncoding.WideSlotInt32 };
                HiddenRecipeEntryEncoding[] hiddenEncodings = flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeHiddenRecipeList)
                    ? new[]
                    {
                        HiddenRecipeEntryEncoding.Pair,
                        HiddenRecipeEntryEncoding.CompactBucketUInt16,
                        HiddenRecipeEntryEncoding.CompactBucketByte,
                        HiddenRecipeEntryEncoding.OutputOnly
                    }
                    : new[] { HiddenRecipeEntryEncoding.Pair };

                for (int disassemblyEncodingIndex = 0; disassemblyEncodingIndex < disassemblyEncodings.Length; disassemblyEncodingIndex++)
                {
                    DisassemblyTargetEntryEncoding disassemblyEncoding = disassemblyEncodings[disassemblyEncodingIndex];
                    for (int hiddenEncodingIndex = 0; hiddenEncodingIndex < hiddenEncodings.Length; hiddenEncodingIndex++)
                    {
                        HiddenRecipeEntryEncoding hiddenEncoding = hiddenEncodings[hiddenEncodingIndex];
                        reader.BaseStream.Position = payloadStart;
                        if (TryDecodeInt32AuthoritativePayload(
                                reader,
                                flags,
                                disassemblyEncoding,
                                hiddenEncoding,
                                out result,
                                out error))
                        {
                            return true;
                        }

                        firstDecodeError ??= error;
                    }
                }

                error = firstDecodeError ?? "Maker-session payload could not be decoded.";
                return false;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException or InvalidDataException)
            {
                error = $"Maker-session payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static bool TryDecodeInt32AuthoritativePayload(
            BinaryReader reader,
            PacketOwnedItemMakerSessionFlags flags,
            DisassemblyTargetEntryEncoding disassemblyEncoding,
            HiddenRecipeEntryEncoding hiddenEncoding,
            out PacketOwnedItemMakerSession result,
            out string error)
        {
            result = null;
            error = null;

            List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargets = null;
            List<PacketOwnedItemMakerSessionHiddenEntry> hiddenEntries = null;
            if (flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeDisassemblyTargets))
            {
                bool requiresPositiveItemId = disassemblyEncoding is DisassemblyTargetEntryEncoding.WideSlotInt32
                    or DisassemblyTargetEntryEncoding.CompactSlotUInt16;
                if (!TryReadDisassemblyTargetEntries32(
                        reader,
                        disassemblyEncoding,
                        requiresPositiveItemId,
                        "Maker-session disassembly target",
                        out disassemblyTargets))
                {
                    error = "Maker-session disassembly target entries are truncated or invalid.";
                    return false;
                }
            }

            if (flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeHiddenRecipeList))
            {
                if (!TryReadHiddenRecipeEntries32(
                        reader,
                        hiddenEncoding,
                        requirePositiveOutputItemId: true,
                        "Maker-session hidden recipe",
                        out hiddenEntries))
                {
                    if (hiddenEncoding != HiddenRecipeEntryEncoding.OutputOnly
                        || !TryReadRemainingCountlessOutputOnlyHiddenRecipeEntries(
                            reader,
                            requirePositiveOutputItemId: true,
                            out hiddenEntries))
                    {
                        error = "Maker-session hidden recipe entries are truncated or invalid.";
                        return false;
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
                long payloadStart = reader.BaseStream.Position;
                string firstDecodeError = null;
                DisassemblyTargetEntryEncoding[] disassemblyEncodings = flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeDisassemblyTargets)
                    ? new[]
                    {
                        DisassemblyTargetEntryEncoding.WideSlotInt32,
                        DisassemblyTargetEntryEncoding.CompactSlotUInt16,
                        DisassemblyTargetEntryEncoding.WideSlotOnlyInt32,
                        DisassemblyTargetEntryEncoding.CompactSlotOnlyUInt16
                    }
                    : new[] { DisassemblyTargetEntryEncoding.WideSlotInt32 };
                HiddenRecipeEntryEncoding[] hiddenEncodings = flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeHiddenRecipeList)
                    ? new[]
                    {
                        HiddenRecipeEntryEncoding.Pair,
                        HiddenRecipeEntryEncoding.CompactBucketUInt16,
                        HiddenRecipeEntryEncoding.CompactBucketByte,
                        HiddenRecipeEntryEncoding.OutputOnly
                    }
                    : new[] { HiddenRecipeEntryEncoding.Pair };

                for (int disassemblyEncodingIndex = 0; disassemblyEncodingIndex < disassemblyEncodings.Length; disassemblyEncodingIndex++)
                {
                    DisassemblyTargetEntryEncoding disassemblyEncoding = disassemblyEncodings[disassemblyEncodingIndex];
                    for (int hiddenEncodingIndex = 0; hiddenEncodingIndex < hiddenEncodings.Length; hiddenEncodingIndex++)
                    {
                        HiddenRecipeEntryEncoding hiddenEncoding = hiddenEncodings[hiddenEncodingIndex];
                        reader.BaseStream.Position = payloadStart;
                        if (TryDecodeCompactAuthoritativePayload(
                                reader,
                                flags,
                                disassemblyEncoding,
                                hiddenEncoding,
                                out result,
                                out error))
                        {
                            return true;
                        }

                        firstDecodeError ??= error;
                    }
                }

                error = firstDecodeError ?? "Maker-session compact payload could not be decoded.";
                return false;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException or InvalidDataException)
            {
                error = $"Maker-session compact payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static bool TryDecodeCompactAuthoritativePayload(
            BinaryReader reader,
            PacketOwnedItemMakerSessionFlags flags,
            DisassemblyTargetEntryEncoding disassemblyEncoding,
            HiddenRecipeEntryEncoding hiddenEncoding,
            out PacketOwnedItemMakerSession result,
            out string error)
        {
            result = null;
            error = null;
            long payloadStart = reader.BaseStream.Position;
            bool[] compactCountModes = { false, true };
            for (int countModeIndex = 0; countModeIndex < compactCountModes.Length; countModeIndex++)
            {
                bool useByteCounts = compactCountModes[countModeIndex];
                reader.BaseStream.Position = payloadStart;
                List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargets = null;
                List<PacketOwnedItemMakerSessionHiddenEntry> hiddenEntries = null;

                if (flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeDisassemblyTargets))
                {
                    if (!TryReadDisassemblyTargetEntries16(
                            reader,
                            disassemblyEncoding,
                            requirePositiveItemId: disassemblyEncoding is DisassemblyTargetEntryEncoding.WideSlotInt32
                                or DisassemblyTargetEntryEncoding.CompactSlotUInt16,
                            "Maker-session compact disassembly target",
                            out disassemblyTargets,
                            useByteCount: useByteCounts))
                    {
                        continue;
                    }
                }

                if (flags.HasFlag(PacketOwnedItemMakerSessionFlags.HasAuthoritativeHiddenRecipeList)
                    && !TryReadHiddenRecipeEntries16(
                        reader,
                        hiddenEncoding,
                        requirePositiveOutputItemId: true,
                        "Maker-session compact hidden recipe",
                        out hiddenEntries,
                        useByteCount: useByteCounts))
                {
                    if (hiddenEncoding != HiddenRecipeEntryEncoding.OutputOnly
                        || !TryReadRemainingCountlessOutputOnlyHiddenRecipeEntries(
                            reader,
                            requirePositiveOutputItemId: true,
                            out hiddenEntries))
                    {
                        continue;
                    }
                }

                if (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    continue;
                }

                result = new PacketOwnedItemMakerSession
                {
                    Flags = flags,
                    DisassemblyTargets = disassemblyTargets ?? (IReadOnlyList<PacketOwnedItemMakerDisassemblyTargetEntry>)Array.Empty<PacketOwnedItemMakerDisassemblyTargetEntry>(),
                    HiddenRecipeEntries = hiddenEntries ?? (IReadOnlyList<PacketOwnedItemMakerSessionHiddenEntry>)Array.Empty<PacketOwnedItemMakerSessionHiddenEntry>()
                };
                return true;
            }

            error = "Maker-session compact payload entries are truncated or invalid.";
            return false;
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

            bool hasDisassemblyTargetAdditions = (compactDeltaFlags & 0x08) != 0;
            bool hasDisassemblyTargetRemovals = (compactDeltaFlags & 0x10) != 0;
            if (hasDisassemblyTargetAdditions)
            {
                deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.HasDisassemblyTargetAdditions;
            }

            if (hasDisassemblyTargetRemovals)
            {
                deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.HasDisassemblyTargetRemovals;
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

            byte[] listAndHiddenTailPayload = reader.BaseStream.Position < reader.BaseStream.Length
                ? reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position))
                : Array.Empty<byte>();
            if (!TryDecodeCompactDeltaListAndHiddenTailPayload(
                    listAndHiddenTailPayload,
                    hasDisassemblyTargetAdditions,
                    hasDisassemblyTargetRemovals,
                    out List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetAdditions,
                    out List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetRemovals,
                    out List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeAdditions,
                    out List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeRemovals,
                    out bool clearsHiddenRecipes,
                    out error))
            {
                return false;
            }

            if (clearsHiddenRecipes)
            {
                deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.ClearHiddenRecipeEntries;
            }

            if (hiddenRecipeAdditions?.Count > 0)
            {
                deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.HasHiddenRecipeAdditions;
            }

            if (hiddenRecipeRemovals?.Count > 0)
            {
                deltaFlags |= PacketOwnedItemMakerSessionDeltaFlags.HasHiddenRecipeRemovals;
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

        private static bool TryDecodeCompactDeltaListAndHiddenTailPayload(
            byte[] payload,
            bool hasDisassemblyTargetAdditions,
            bool hasDisassemblyTargetRemovals,
            out List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetAdditions,
            out List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetRemovals,
            out List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeAdditions,
            out List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeRemovals,
            out bool clearsHiddenRecipes,
            out string error)
        {
            disassemblyTargetAdditions = null;
            disassemblyTargetRemovals = null;
            hiddenRecipeAdditions = null;
            hiddenRecipeRemovals = null;
            clearsHiddenRecipes = false;
            error = null;

            DisassemblyTargetEntryEncoding[] additionEncodings = hasDisassemblyTargetAdditions
                ? new[]
                {
                    DisassemblyTargetEntryEncoding.WideSlotInt32,
                    DisassemblyTargetEntryEncoding.CompactSlotUInt16,
                    DisassemblyTargetEntryEncoding.WideSlotOnlyInt32,
                    DisassemblyTargetEntryEncoding.CompactSlotOnlyUInt16
                }
                : new[] { DisassemblyTargetEntryEncoding.WideSlotInt32 };
            DisassemblyTargetEntryEncoding[] removalEncodings = hasDisassemblyTargetRemovals
                ? new[]
                {
                    DisassemblyTargetEntryEncoding.WideSlotInt32,
                    DisassemblyTargetEntryEncoding.CompactSlotUInt16,
                    DisassemblyTargetEntryEncoding.WideSlotOnlyInt32,
                    DisassemblyTargetEntryEncoding.CompactSlotOnlyUInt16
                }
                : new[] { DisassemblyTargetEntryEncoding.WideSlotInt32 };

            for (int additionEncodingIndex = 0; additionEncodingIndex < additionEncodings.Length; additionEncodingIndex++)
            {
                DisassemblyTargetEntryEncoding additionEncoding = additionEncodings[additionEncodingIndex];
                for (int removalEncodingIndex = 0; removalEncodingIndex < removalEncodings.Length; removalEncodingIndex++)
                {
                    DisassemblyTargetEntryEncoding removalEncoding = removalEncodings[removalEncodingIndex];
                    if (!TryDecodeCompactDeltaListsAndHiddenTailWithEncodings(
                            payload,
                            hasDisassemblyTargetAdditions,
                            hasDisassemblyTargetRemovals,
                            additionEncoding,
                            removalEncoding,
                            useByteCount: false,
                            out disassemblyTargetAdditions,
                            out disassemblyTargetRemovals,
                            out hiddenRecipeAdditions,
                            out hiddenRecipeRemovals,
                            out clearsHiddenRecipes)
                        && !TryDecodeCompactDeltaListsAndHiddenTailWithEncodings(
                            payload,
                            hasDisassemblyTargetAdditions,
                            hasDisassemblyTargetRemovals,
                            additionEncoding,
                            removalEncoding,
                            useByteCount: true,
                            out disassemblyTargetAdditions,
                            out disassemblyTargetRemovals,
                            out hiddenRecipeAdditions,
                            out hiddenRecipeRemovals,
                            out clearsHiddenRecipes))
                    {
                        continue;
                    }

                    return true;
                }
            }

            error = "Maker-session compact delta hidden tail could not be decoded.";
            return false;
        }

        private static bool TryDecodeCompactDeltaListsAndHiddenTailWithEncodings(
            byte[] payload,
            bool hasDisassemblyTargetAdditions,
            bool hasDisassemblyTargetRemovals,
            DisassemblyTargetEntryEncoding additionEncoding,
            DisassemblyTargetEntryEncoding removalEncoding,
            bool useByteCount,
            out List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetAdditions,
            out List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetRemovals,
            out List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeAdditions,
            out List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeRemovals,
            out bool clearsHiddenRecipes)
        {
            disassemblyTargetAdditions = null;
            disassemblyTargetRemovals = null;
            hiddenRecipeAdditions = null;
            hiddenRecipeRemovals = null;
            clearsHiddenRecipes = false;

            try
            {
                using MemoryStream stream = new(payload ?? Array.Empty<byte>(), writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                if (hasDisassemblyTargetAdditions &&
                    !TryReadDisassemblyTargetEntries16(
                        reader,
                        additionEncoding,
                        requirePositiveItemId: false,
                        "Maker-session compact delta disassembly target addition",
                        out disassemblyTargetAdditions,
                        useByteCount))
                {
                    return false;
                }

                if (hasDisassemblyTargetRemovals &&
                    !TryReadDisassemblyTargetEntries16(
                        reader,
                        removalEncoding,
                        requirePositiveItemId: false,
                        "Maker-session compact delta disassembly target removal",
                        out disassemblyTargetRemovals,
                        useByteCount))
                {
                    return false;
                }

                byte[] hiddenTailPayload = reader.BaseStream.Position < reader.BaseStream.Length
                    ? reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position))
                    : Array.Empty<byte>();
                return TryDecodeCompactHiddenTail(
                    hiddenTailPayload,
                    out hiddenRecipeAdditions,
                    out hiddenRecipeRemovals,
                    out clearsHiddenRecipes,
                    out _);
            }
            catch (EndOfStreamException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        private static bool TryReadDisassemblyTargetEntries16(
            BinaryReader reader,
            DisassemblyTargetEntryEncoding encoding,
            bool requirePositiveItemId,
            string label,
            out List<PacketOwnedItemMakerDisassemblyTargetEntry> entries,
            bool useByteCount = false)
        {
            entries = null;
            if (reader == null)
            {
                return false;
            }

            long startPosition = reader.BaseStream.Position;
            try
            {
                int count = useByteCount
                    ? ReadNonNegativeCount8(reader, $"{label} count is missing or negative.")
                    : ReadNonNegativeCount16(reader, $"{label} count is missing or negative.");
                if (count <= 0)
                {
                    return true;
                }

                int entryWidth = encoding switch
                {
                    DisassemblyTargetEntryEncoding.CompactSlotUInt16 => sizeof(ushort) + sizeof(int),
                    DisassemblyTargetEntryEncoding.WideSlotOnlyInt32 => sizeof(int),
                    DisassemblyTargetEntryEncoding.CompactSlotOnlyUInt16 => sizeof(ushort),
                    _ => sizeof(int) * 2
                };

                long requiredBytes = (long)entryWidth * count;
                if (reader.BaseStream.Length - reader.BaseStream.Position < requiredBytes)
                {
                    reader.BaseStream.Position = startPosition;
                    return false;
                }

                entries = new List<PacketOwnedItemMakerDisassemblyTargetEntry>(count);
                for (int i = 0; i < count; i++)
                {
                    int slotIndex;
                    int itemId;
                    switch (encoding)
                    {
                        case DisassemblyTargetEntryEncoding.CompactSlotUInt16:
                            slotIndex = reader.ReadUInt16();
                            itemId = reader.ReadInt32();
                            break;
                        case DisassemblyTargetEntryEncoding.WideSlotOnlyInt32:
                            slotIndex = reader.ReadInt32();
                            itemId = 0;
                            break;
                        case DisassemblyTargetEntryEncoding.CompactSlotOnlyUInt16:
                            slotIndex = reader.ReadUInt16();
                            itemId = 0;
                            break;
                        default:
                            slotIndex = reader.ReadInt32();
                            itemId = reader.ReadInt32();
                            break;
                    }

                    if (slotIndex < 0)
                    {
                        reader.BaseStream.Position = startPosition;
                        return false;
                    }

                    if (requirePositiveItemId && itemId <= 0)
                    {
                        reader.BaseStream.Position = startPosition;
                        return false;
                    }

                    entries.Add(new PacketOwnedItemMakerDisassemblyTargetEntry(slotIndex, itemId));
                }

                return true;
            }
            catch (EndOfStreamException)
            {
                reader.BaseStream.Position = startPosition;
                return false;
            }
            catch (IOException)
            {
                reader.BaseStream.Position = startPosition;
                return false;
            }
            catch (InvalidDataException)
            {
                reader.BaseStream.Position = startPosition;
                return false;
            }
        }

        private static bool TryReadDisassemblyTargetEntries32(
            BinaryReader reader,
            DisassemblyTargetEntryEncoding encoding,
            bool requirePositiveItemId,
            string label,
            out List<PacketOwnedItemMakerDisassemblyTargetEntry> entries)
        {
            entries = null;
            if (reader == null)
            {
                return false;
            }

            long startPosition = reader.BaseStream.Position;
            try
            {
                int count = ReadNonNegativeCount(reader, $"{label} count is missing or negative.");
                if (count <= 0)
                {
                    return true;
                }

                int entryWidth = encoding switch
                {
                    DisassemblyTargetEntryEncoding.CompactSlotUInt16 => sizeof(ushort) + sizeof(int),
                    DisassemblyTargetEntryEncoding.WideSlotOnlyInt32 => sizeof(int),
                    DisassemblyTargetEntryEncoding.CompactSlotOnlyUInt16 => sizeof(ushort),
                    _ => sizeof(int) * 2
                };

                long requiredBytes = (long)entryWidth * count;
                if (reader.BaseStream.Length - reader.BaseStream.Position < requiredBytes)
                {
                    reader.BaseStream.Position = startPosition;
                    return false;
                }

                entries = new List<PacketOwnedItemMakerDisassemblyTargetEntry>(count);
                for (int i = 0; i < count; i++)
                {
                    int slotIndex;
                    int itemId;
                    switch (encoding)
                    {
                        case DisassemblyTargetEntryEncoding.CompactSlotUInt16:
                            slotIndex = reader.ReadUInt16();
                            itemId = reader.ReadInt32();
                            break;
                        case DisassemblyTargetEntryEncoding.WideSlotOnlyInt32:
                            slotIndex = reader.ReadInt32();
                            itemId = 0;
                            break;
                        case DisassemblyTargetEntryEncoding.CompactSlotOnlyUInt16:
                            slotIndex = reader.ReadUInt16();
                            itemId = 0;
                            break;
                        default:
                            slotIndex = reader.ReadInt32();
                            itemId = reader.ReadInt32();
                            break;
                    }

                    if (slotIndex < 0)
                    {
                        reader.BaseStream.Position = startPosition;
                        return false;
                    }

                    if (requirePositiveItemId && itemId <= 0)
                    {
                        reader.BaseStream.Position = startPosition;
                        return false;
                    }

                    entries.Add(new PacketOwnedItemMakerDisassemblyTargetEntry(slotIndex, itemId));
                }

                return true;
            }
            catch (EndOfStreamException)
            {
                reader.BaseStream.Position = startPosition;
                return false;
            }
            catch (IOException)
            {
                reader.BaseStream.Position = startPosition;
                return false;
            }
            catch (InvalidDataException)
            {
                reader.BaseStream.Position = startPosition;
                return false;
            }
        }

        private static bool TryDecodeDeltaPayload(
            BinaryReader reader,
            out PacketOwnedItemMakerSession result,
            out string error)
        {
            long startPosition = reader?.BaseStream?.Position ?? 0;
            result = null;
            error = null;

            if (reader == null)
            {
                error = "Maker-session delta payload reader is missing.";
                return false;
            }

            string firstDecodeError = null;
            if (TryDecodeDeltaPayloadWithCountWidth(
                    reader,
                    useCompactCountWidth: false,
                    out result,
                    out error))
            {
                return true;
            }

            firstDecodeError = error;
            reader.BaseStream.Position = startPosition;
            if (TryDecodeDeltaPayloadWithCountWidth(
                    reader,
                    useCompactCountWidth: true,
                    out result,
                    out error))
            {
                return true;
            }

            error = firstDecodeError ?? error ?? "Maker-session delta payload could not be decoded.";
            return false;
        }

        private static bool TryDecodeDeltaPayloadWithCountWidth(
            BinaryReader reader,
            bool useCompactCountWidth,
            out PacketOwnedItemMakerSession result,
            out string error)
        {
            long startPosition = reader?.BaseStream?.Position ?? 0;
            result = null;
            error = null;
            try
            {
                EnsureReadable(reader, sizeof(int), "Maker-session delta payload is missing its Int32 delta flags.");
                PacketOwnedItemMakerSessionDeltaFlags deltaFlags = (PacketOwnedItemMakerSessionDeltaFlags)reader.ReadInt32();

                List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetAdditions = null;
                List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetRemovals = null;
                List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeAdditions = null;
                List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeRemovals = null;

                if (useCompactCountWidth)
                {
                    if (!TryReadCompactDeltaLists(
                            reader,
                            deltaFlags,
                            out disassemblyTargetAdditions,
                            out disassemblyTargetRemovals,
                            out hiddenRecipeAdditions,
                            out hiddenRecipeRemovals,
                            out string compactDecodeError))
                    {
                        error = compactDecodeError ?? "Maker-session delta payload could not be decoded with compact count/list encodings.";
                        reader.BaseStream.Position = startPosition;
                        return false;
                    }
                }
                else
                {
                    if (!TryReadWideDeltaLists(
                            reader,
                            deltaFlags,
                            out disassemblyTargetAdditions,
                            out disassemblyTargetRemovals,
                            out hiddenRecipeAdditions,
                            out hiddenRecipeRemovals,
                            out string wideDecodeError))
                    {
                        error = wideDecodeError ?? "Maker-session delta payload could not be decoded with Int32 count/list encodings.";
                        reader.BaseStream.Position = startPosition;
                        return false;
                    }
                }

                if (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    error = $"Maker-session payload left {reader.BaseStream.Length - reader.BaseStream.Position} unread byte(s).";
                    reader.BaseStream.Position = startPosition;
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
            catch (Exception ex) when (ex is EndOfStreamException or IOException or InvalidDataException)
            {
                error = $"Maker-session delta payload could not be decoded: {ex.Message}";
                reader.BaseStream.Position = startPosition;
                return false;
            }
        }

        private static bool TryReadWideDeltaLists(
            BinaryReader reader,
            PacketOwnedItemMakerSessionDeltaFlags deltaFlags,
            out List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetAdditions,
            out List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetRemovals,
            out List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeAdditions,
            out List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeRemovals,
            out string error)
        {
            disassemblyTargetAdditions = null;
            disassemblyTargetRemovals = null;
            hiddenRecipeAdditions = null;
            hiddenRecipeRemovals = null;
            error = null;

            if (reader == null)
            {
                error = "Maker-session delta payload reader is missing.";
                return false;
            }

            long payloadStart = reader.BaseStream.Position;
            DisassemblyTargetEntryEncoding[] disassemblyAdditionEncodings = deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasDisassemblyTargetAdditions)
                ? new[]
                {
                    DisassemblyTargetEntryEncoding.WideSlotInt32,
                    DisassemblyTargetEntryEncoding.CompactSlotUInt16,
                    DisassemblyTargetEntryEncoding.WideSlotOnlyInt32,
                    DisassemblyTargetEntryEncoding.CompactSlotOnlyUInt16
                }
                : new[] { DisassemblyTargetEntryEncoding.WideSlotInt32 };
            DisassemblyTargetEntryEncoding[] disassemblyRemovalEncodings = deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasDisassemblyTargetRemovals)
                ? new[]
                {
                    DisassemblyTargetEntryEncoding.WideSlotInt32,
                    DisassemblyTargetEntryEncoding.CompactSlotUInt16,
                    DisassemblyTargetEntryEncoding.WideSlotOnlyInt32,
                    DisassemblyTargetEntryEncoding.CompactSlotOnlyUInt16
                }
                : new[] { DisassemblyTargetEntryEncoding.WideSlotInt32 };
            HiddenRecipeEntryEncoding[] hiddenAdditionEncodings = deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasHiddenRecipeAdditions)
                ? new[]
                {
                    HiddenRecipeEntryEncoding.Pair,
                    HiddenRecipeEntryEncoding.CompactBucketUInt16,
                    HiddenRecipeEntryEncoding.CompactBucketByte,
                    HiddenRecipeEntryEncoding.OutputOnly
                }
                : new[] { HiddenRecipeEntryEncoding.Pair };
            HiddenRecipeEntryEncoding[] hiddenRemovalEncodings = deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasHiddenRecipeRemovals)
                ? new[]
                {
                    HiddenRecipeEntryEncoding.Pair,
                    HiddenRecipeEntryEncoding.CompactBucketUInt16,
                    HiddenRecipeEntryEncoding.CompactBucketByte,
                    HiddenRecipeEntryEncoding.OutputOnly
                }
                : new[] { HiddenRecipeEntryEncoding.Pair };

            for (int additionEncodingIndex = 0; additionEncodingIndex < disassemblyAdditionEncodings.Length; additionEncodingIndex++)
            {
                DisassemblyTargetEntryEncoding disassemblyAdditionEncoding = disassemblyAdditionEncodings[additionEncodingIndex];
                for (int removalEncodingIndex = 0; removalEncodingIndex < disassemblyRemovalEncodings.Length; removalEncodingIndex++)
                {
                    DisassemblyTargetEntryEncoding disassemblyRemovalEncoding = disassemblyRemovalEncodings[removalEncodingIndex];
                    for (int hiddenAdditionEncodingIndex = 0; hiddenAdditionEncodingIndex < hiddenAdditionEncodings.Length; hiddenAdditionEncodingIndex++)
                    {
                        HiddenRecipeEntryEncoding hiddenAdditionEncoding = hiddenAdditionEncodings[hiddenAdditionEncodingIndex];
                        for (int hiddenRemovalEncodingIndex = 0; hiddenRemovalEncodingIndex < hiddenRemovalEncodings.Length; hiddenRemovalEncodingIndex++)
                        {
                            HiddenRecipeEntryEncoding hiddenRemovalEncoding = hiddenRemovalEncodings[hiddenRemovalEncodingIndex];
                            if (TryReadWideDeltaListsWithEncodings(
                                    reader,
                                    payloadStart,
                                    deltaFlags,
                                    disassemblyAdditionEncoding,
                                    disassemblyRemovalEncoding,
                                    hiddenAdditionEncoding,
                                    hiddenRemovalEncoding,
                                    out disassemblyTargetAdditions,
                                    out disassemblyTargetRemovals,
                                    out hiddenRecipeAdditions,
                                    out hiddenRecipeRemovals))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            reader.BaseStream.Position = payloadStart;
            error = "Maker-session delta payload could not be decoded for any supported Int32-count list encoding combination.";
            return false;
        }

        private static bool TryReadWideDeltaListsWithEncodings(
            BinaryReader reader,
            long payloadStart,
            PacketOwnedItemMakerSessionDeltaFlags deltaFlags,
            DisassemblyTargetEntryEncoding disassemblyAdditionEncoding,
            DisassemblyTargetEntryEncoding disassemblyRemovalEncoding,
            HiddenRecipeEntryEncoding hiddenAdditionEncoding,
            HiddenRecipeEntryEncoding hiddenRemovalEncoding,
            out List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetAdditions,
            out List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetRemovals,
            out List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeAdditions,
            out List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeRemovals)
        {
            disassemblyTargetAdditions = null;
            disassemblyTargetRemovals = null;
            hiddenRecipeAdditions = null;
            hiddenRecipeRemovals = null;

            reader.BaseStream.Position = payloadStart;
            if (deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasDisassemblyTargetAdditions)
                && !TryReadDisassemblyTargetEntries32(
                    reader,
                    disassemblyAdditionEncoding,
                    requirePositiveItemId: false,
                    "Maker-session delta disassembly target addition",
                    out disassemblyTargetAdditions))
            {
                return false;
            }

            if (deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasDisassemblyTargetRemovals)
                && !TryReadDisassemblyTargetEntries32(
                    reader,
                    disassemblyRemovalEncoding,
                    requirePositiveItemId: false,
                    "Maker-session delta disassembly target removal",
                    out disassemblyTargetRemovals))
            {
                return false;
            }

            if (deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasHiddenRecipeAdditions)
                && !TryReadHiddenRecipeEntries32(
                    reader,
                    hiddenAdditionEncoding,
                    requirePositiveOutputItemId: true,
                    "Maker-session delta hidden recipe addition",
                    out hiddenRecipeAdditions))
            {
                return false;
            }

            if (deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasHiddenRecipeRemovals)
                && !TryReadHiddenRecipeEntries32(
                    reader,
                    hiddenRemovalEncoding,
                    requirePositiveOutputItemId: false,
                    "Maker-session delta hidden recipe removal",
                    out hiddenRecipeRemovals))
            {
                return false;
            }

            return reader.BaseStream.Position == reader.BaseStream.Length;
        }

        private static bool TryReadCompactDeltaLists(
            BinaryReader reader,
            PacketOwnedItemMakerSessionDeltaFlags deltaFlags,
            out List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetAdditions,
            out List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetRemovals,
            out List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeAdditions,
            out List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeRemovals,
            out string error)
        {
            disassemblyTargetAdditions = null;
            disassemblyTargetRemovals = null;
            hiddenRecipeAdditions = null;
            hiddenRecipeRemovals = null;
            error = null;

            if (reader == null)
            {
                error = "Maker-session compact delta payload reader is missing.";
                return false;
            }

            long payloadStart = reader.BaseStream.Position;
            DisassemblyTargetEntryEncoding[] disassemblyAdditionEncodings = deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasDisassemblyTargetAdditions)
                ? new[]
                {
                    DisassemblyTargetEntryEncoding.WideSlotInt32,
                    DisassemblyTargetEntryEncoding.CompactSlotUInt16,
                    DisassemblyTargetEntryEncoding.WideSlotOnlyInt32,
                    DisassemblyTargetEntryEncoding.CompactSlotOnlyUInt16
                }
                : new[] { DisassemblyTargetEntryEncoding.WideSlotInt32 };
            DisassemblyTargetEntryEncoding[] disassemblyRemovalEncodings = deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasDisassemblyTargetRemovals)
                ? new[]
                {
                    DisassemblyTargetEntryEncoding.WideSlotInt32,
                    DisassemblyTargetEntryEncoding.CompactSlotUInt16,
                    DisassemblyTargetEntryEncoding.WideSlotOnlyInt32,
                    DisassemblyTargetEntryEncoding.CompactSlotOnlyUInt16
                }
                : new[] { DisassemblyTargetEntryEncoding.WideSlotInt32 };
            HiddenRecipeEntryEncoding[] hiddenAdditionEncodings = deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasHiddenRecipeAdditions)
                ? new[]
                {
                    HiddenRecipeEntryEncoding.Pair,
                    HiddenRecipeEntryEncoding.CompactBucketUInt16,
                    HiddenRecipeEntryEncoding.CompactBucketByte,
                    HiddenRecipeEntryEncoding.OutputOnly
                }
                : new[] { HiddenRecipeEntryEncoding.Pair };
            HiddenRecipeEntryEncoding[] hiddenRemovalEncodings = deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasHiddenRecipeRemovals)
                ? new[]
                {
                    HiddenRecipeEntryEncoding.Pair,
                    HiddenRecipeEntryEncoding.CompactBucketUInt16,
                    HiddenRecipeEntryEncoding.CompactBucketByte,
                    HiddenRecipeEntryEncoding.OutputOnly
                }
                : new[] { HiddenRecipeEntryEncoding.Pair };
            bool[] compactCountModes = { false, true };

            for (int countModeIndex = 0; countModeIndex < compactCountModes.Length; countModeIndex++)
            {
                bool useByteCounts = compactCountModes[countModeIndex];
                for (int additionEncodingIndex = 0; additionEncodingIndex < disassemblyAdditionEncodings.Length; additionEncodingIndex++)
                {
                    DisassemblyTargetEntryEncoding disassemblyAdditionEncoding = disassemblyAdditionEncodings[additionEncodingIndex];
                    for (int removalEncodingIndex = 0; removalEncodingIndex < disassemblyRemovalEncodings.Length; removalEncodingIndex++)
                    {
                        DisassemblyTargetEntryEncoding disassemblyRemovalEncoding = disassemblyRemovalEncodings[removalEncodingIndex];
                        for (int hiddenAdditionEncodingIndex = 0; hiddenAdditionEncodingIndex < hiddenAdditionEncodings.Length; hiddenAdditionEncodingIndex++)
                        {
                            HiddenRecipeEntryEncoding hiddenAdditionEncoding = hiddenAdditionEncodings[hiddenAdditionEncodingIndex];
                            for (int hiddenRemovalEncodingIndex = 0; hiddenRemovalEncodingIndex < hiddenRemovalEncodings.Length; hiddenRemovalEncodingIndex++)
                            {
                                HiddenRecipeEntryEncoding hiddenRemovalEncoding = hiddenRemovalEncodings[hiddenRemovalEncodingIndex];
                                if (TryReadCompactDeltaListsWithEncodings(
                                        reader,
                                        payloadStart,
                                        deltaFlags,
                                        disassemblyAdditionEncoding,
                                        disassemblyRemovalEncoding,
                                        hiddenAdditionEncoding,
                                        hiddenRemovalEncoding,
                                        useByteCounts,
                                        out disassemblyTargetAdditions,
                                        out disassemblyTargetRemovals,
                                        out hiddenRecipeAdditions,
                                        out hiddenRecipeRemovals))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            reader.BaseStream.Position = payloadStart;
            error = "Maker-session compact delta payload could not be decoded for any supported list encoding combination.";
            return false;
        }

        private static bool TryReadCompactDeltaListsWithEncodings(
            BinaryReader reader,
            long payloadStart,
            PacketOwnedItemMakerSessionDeltaFlags deltaFlags,
            DisassemblyTargetEntryEncoding disassemblyAdditionEncoding,
            DisassemblyTargetEntryEncoding disassemblyRemovalEncoding,
            HiddenRecipeEntryEncoding hiddenAdditionEncoding,
            HiddenRecipeEntryEncoding hiddenRemovalEncoding,
            bool useByteCounts,
            out List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetAdditions,
            out List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargetRemovals,
            out List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeAdditions,
            out List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeRemovals)
        {
            disassemblyTargetAdditions = null;
            disassemblyTargetRemovals = null;
            hiddenRecipeAdditions = null;
            hiddenRecipeRemovals = null;

            reader.BaseStream.Position = payloadStart;
            if (deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasDisassemblyTargetAdditions)
                && !TryReadDisassemblyTargetEntries16(
                    reader,
                    disassemblyAdditionEncoding,
                    requirePositiveItemId: false,
                    "Maker-session delta disassembly target addition",
                    out disassemblyTargetAdditions,
                    useByteCount: useByteCounts))
            {
                return false;
            }

            if (deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasDisassemblyTargetRemovals)
                && !TryReadDisassemblyTargetEntries16(
                    reader,
                    disassemblyRemovalEncoding,
                    requirePositiveItemId: false,
                    "Maker-session delta disassembly target removal",
                    out disassemblyTargetRemovals,
                    useByteCount: useByteCounts))
            {
                return false;
            }

            if (deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasHiddenRecipeAdditions)
                && !TryReadHiddenRecipeEntries16(
                    reader,
                    hiddenAdditionEncoding,
                    requirePositiveOutputItemId: true,
                    "Maker-session delta hidden recipe addition",
                    out hiddenRecipeAdditions,
                    useByteCount: useByteCounts))
            {
                return false;
            }

            if (deltaFlags.HasFlag(PacketOwnedItemMakerSessionDeltaFlags.HasHiddenRecipeRemovals)
                && !TryReadHiddenRecipeEntries16(
                    reader,
                    hiddenRemovalEncoding,
                    requirePositiveOutputItemId: false,
                    "Maker-session delta hidden recipe removal",
                    out hiddenRecipeRemovals,
                    useByteCount: useByteCounts))
            {
                return false;
            }

            return reader.BaseStream.Position == reader.BaseStream.Length;
        }

        private static bool TryDecodeCompactHiddenTail(
            byte[] payload,
            out List<PacketOwnedItemMakerSessionHiddenEntry> additions,
            out List<PacketOwnedItemMakerSessionHiddenEntry> removals,
            out bool clearsHiddenRecipes,
            out string error)
        {
            additions = null;
            removals = null;
            clearsHiddenRecipes = false;
            error = null;
            if (payload == null || payload.Length == 0)
            {
                return true;
            }

            if (TryDecodeCompactHiddenTailWithFlags(payload, out additions, out removals, out clearsHiddenRecipes)
                || TryDecodeCompactHiddenTailAdditionsThenRemovals(payload, out additions, out removals)
                || TryDecodeCompactHiddenTailSingleList(payload, out additions, out removals))
            {
                return true;
            }

            error = "Maker-session compact delta hidden tail could not be decoded.";
            return false;
        }

        private static bool TryDecodeCompactHiddenTailWithFlags(
            byte[] payload,
            out List<PacketOwnedItemMakerSessionHiddenEntry> additions,
            out List<PacketOwnedItemMakerSessionHiddenEntry> removals,
            out bool clearsHiddenRecipes)
        {
            additions = null;
            removals = null;
            clearsHiddenRecipes = false;
            if (payload == null || payload.Length < sizeof(byte))
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                byte flags = reader.ReadByte();
                if ((flags & ~(CompactHiddenTailHasAdditions | CompactHiddenTailHasRemovals | CompactHiddenTailClearHidden)) != 0)
                {
                    return false;
                }

                clearsHiddenRecipes = (flags & CompactHiddenTailClearHidden) != 0;
                int hiddenTailPayloadLength = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
                byte[] hiddenTailPayload = reader.ReadBytes(hiddenTailPayloadLength);
                bool hasAdditions = (flags & CompactHiddenTailHasAdditions) != 0;
                bool hasRemovals = (flags & CompactHiddenTailHasRemovals) != 0;
                if (hasAdditions && hasRemovals)
                {
                    if (!TryDecodeHiddenTailTwoLists(hiddenTailPayload, additionsFirst: true, out additions, out removals)
                        && !TryDecodeHiddenTailTwoLists(hiddenTailPayload, additionsFirst: false, out additions, out removals))
                    {
                        return false;
                    }
                }
                else if (hasAdditions)
                {
                    if (!TryDecodeHiddenTailSingleList(
                            hiddenTailPayload,
                            isAdditionList: true,
                            out additions))
                    {
                        return false;
                    }
                }
                else if (hasRemovals)
                {
                    if (!TryDecodeHiddenTailSingleList(
                            hiddenTailPayload,
                            isAdditionList: false,
                            out removals))
                    {
                        return false;
                    }
                }
                else if (hiddenTailPayload.Length != 0)
                {
                    return false;
                }

                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        private static bool TryDecodeCompactHiddenTailAdditionsThenRemovals(
            byte[] payload,
            out List<PacketOwnedItemMakerSessionHiddenEntry> additions,
            out List<PacketOwnedItemMakerSessionHiddenEntry> removals)
        {
            return TryDecodeHiddenTailTwoLists(payload, additionsFirst: true, out additions, out removals);
        }

        private static bool TryDecodeCompactHiddenTailSingleList(
            byte[] payload,
            out List<PacketOwnedItemMakerSessionHiddenEntry> additions,
            out List<PacketOwnedItemMakerSessionHiddenEntry> removals)
        {
            additions = null;
            removals = null;
            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                if (!TryReadHiddenRecipeRemovalEntries16(
                        reader,
                        "Maker-session compact delta hidden recipe entry",
                        out List<PacketOwnedItemMakerSessionHiddenEntry> entries)
                    && !TryReadHiddenRecipeRemovalEntries8(
                        reader,
                        "Maker-session compact delta hidden recipe entry",
                        out entries))
                {
                    return TryDecodeHiddenTailCountlessOutputOnlyList(
                        payload,
                        requirePositiveOutputItemId: true,
                        out additions);
                }

                if (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    return false;
                }

                if (entries == null || entries.Count == 0)
                {
                    return true;
                }

                bool allOutputOnlyEntries = entries.TrueForAll(entry =>
                    entry.BucketKey < 0
                    && entry.OutputItemId > 0);
                if (allOutputOnlyEntries)
                {
                    additions = entries;
                    return true;
                }

                bool containsRemovalWildcard = entries.Exists(entry =>
                    entry.OutputItemId <= 0
                    || entry.BucketKey < 0);
                if (!containsRemovalWildcard)
                {
                    additions = entries;
                    return true;
                }

                additions = new List<PacketOwnedItemMakerSessionHiddenEntry>(entries.Count);
                removals = new List<PacketOwnedItemMakerSessionHiddenEntry>(entries.Count);
                for (int i = 0; i < entries.Count; i++)
                {
                    PacketOwnedItemMakerSessionHiddenEntry entry = entries[i];
                    if (entry.OutputItemId > 0 && entry.BucketKey >= 0)
                    {
                        additions.Add(entry);
                    }
                    else
                    {
                        removals.Add(entry);
                    }
                }

                if (additions.Count == 0)
                {
                    additions = null;
                }

                if (removals.Count == 0)
                {
                    removals = null;
                }

                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        private static bool TryDecodeHiddenTailTwoLists(
            byte[] payload,
            bool additionsFirst,
            out List<PacketOwnedItemMakerSessionHiddenEntry> additions,
            out List<PacketOwnedItemMakerSessionHiddenEntry> removals)
        {
            additions = null;
            removals = null;
            if (payload == null)
            {
                return false;
            }

            HiddenRecipeEntryEncoding[] firstListEncodings = additionsFirst
                ? CreateHiddenRecipeEntryEncodingProbeOrder()
                : CreateHiddenRecipeEntryEncodingProbeOrder();
            HiddenRecipeEntryEncoding[] secondListEncodings = additionsFirst
                ? CreateHiddenRecipeEntryEncodingProbeOrder()
                : CreateHiddenRecipeEntryEncodingProbeOrder();

            for (int firstEncodingIndex = 0; firstEncodingIndex < firstListEncodings.Length; firstEncodingIndex++)
            {
                HiddenRecipeEntryEncoding firstEncoding = firstListEncodings[firstEncodingIndex];
                for (int secondEncodingIndex = 0; secondEncodingIndex < secondListEncodings.Length; secondEncodingIndex++)
                {
                    HiddenRecipeEntryEncoding secondEncoding = secondListEncodings[secondEncodingIndex];
                    if (!TryDecodeHiddenTailTwoListsWithEncodings(
                            payload,
                            additionsFirst,
                            firstEncoding,
                            secondEncoding,
                            useByteCount: false,
                            out additions,
                            out removals)
                        && !TryDecodeHiddenTailTwoListsWithEncodings(
                            payload,
                            additionsFirst,
                            firstEncoding,
                            secondEncoding,
                            useByteCount: true,
                            out additions,
                            out removals))
                    {
                        continue;
                    }

                    return true;
                }
            }

            additions = null;
            removals = null;
            return false;
        }

        private static HiddenRecipeEntryEncoding[] CreateHiddenRecipeEntryEncodingProbeOrder()
        {
            return new[]
            {
                HiddenRecipeEntryEncoding.Pair,
                HiddenRecipeEntryEncoding.CompactBucketUInt16,
                HiddenRecipeEntryEncoding.CompactBucketByte,
                HiddenRecipeEntryEncoding.OutputOnly
            };
        }

        private static bool TryDecodeHiddenTailTwoListsWithEncodings(
            byte[] payload,
            bool additionsFirst,
            HiddenRecipeEntryEncoding firstEncoding,
            HiddenRecipeEntryEncoding secondEncoding,
            bool useByteCount,
            out List<PacketOwnedItemMakerSessionHiddenEntry> additions,
            out List<PacketOwnedItemMakerSessionHiddenEntry> removals)
        {
            additions = null;
            removals = null;
            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                bool firstListRequiresPositiveOutput = additionsFirst;
                bool secondListRequiresPositiveOutput = !additionsFirst;
                if (!TryReadHiddenRecipeEntries16(
                        reader,
                        firstEncoding,
                        requirePositiveOutputItemId: firstListRequiresPositiveOutput,
                        "Maker-session compact delta hidden recipe first list",
                        out List<PacketOwnedItemMakerSessionHiddenEntry> firstList,
                        useByteCount))
                {
                    return false;
                }

                if (!TryReadHiddenRecipeEntries16(
                        reader,
                        secondEncoding,
                        requirePositiveOutputItemId: secondListRequiresPositiveOutput,
                        "Maker-session compact delta hidden recipe second list",
                        out List<PacketOwnedItemMakerSessionHiddenEntry> secondList,
                        useByteCount))
                {
                    return false;
                }

                if (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    return false;
                }

                if (additionsFirst)
                {
                    additions = firstList;
                    removals = secondList;
                }
                else
                {
                    removals = firstList;
                    additions = secondList;
                }

                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        private static bool TryDecodeHiddenTailSingleList(
            byte[] payload,
            bool isAdditionList,
            out List<PacketOwnedItemMakerSessionHiddenEntry> entries)
        {
            entries = null;
            if (payload == null)
            {
                return false;
            }

            HiddenRecipeEntryEncoding[] encodings = new[]
            {
                HiddenRecipeEntryEncoding.Pair,
                HiddenRecipeEntryEncoding.CompactBucketUInt16,
                HiddenRecipeEntryEncoding.CompactBucketByte,
                HiddenRecipeEntryEncoding.OutputOnly
            };
            for (int i = 0; i < encodings.Length; i++)
            {
                if (!TryDecodeHiddenTailSingleListWithEncoding(
                        payload,
                        isAdditionList,
                        encodings[i],
                        out entries))
                {
                    continue;
                }

                return true;
            }

            entries = null;
            return false;
        }

        private static bool TryDecodeHiddenTailSingleListWithEncoding(
            byte[] payload,
            bool isAdditionList,
            HiddenRecipeEntryEncoding encoding,
            out List<PacketOwnedItemMakerSessionHiddenEntry> entries)
        {
            entries = null;
            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                if (!TryReadHiddenRecipeEntries16(
                        reader,
                        encoding,
                        requirePositiveOutputItemId: isAdditionList,
                        "Maker-session compact delta hidden recipe list",
                        out entries,
                        useByteCount: false)
                    && !TryReadHiddenRecipeEntries16(
                        reader,
                        encoding,
                        requirePositiveOutputItemId: isAdditionList,
                        "Maker-session compact delta hidden recipe list",
                        out entries,
                        useByteCount: true))
                {
                    return encoding == HiddenRecipeEntryEncoding.OutputOnly
                           && TryDecodeHiddenTailCountlessOutputOnlyList(
                               payload,
                               requirePositiveOutputItemId: isAdditionList,
                               out entries);
                }

                return reader.BaseStream.Position == reader.BaseStream.Length;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        private static bool TryDecodeHiddenTailCountlessOutputOnlyList(
            byte[] payload,
            bool requirePositiveOutputItemId,
            out List<PacketOwnedItemMakerSessionHiddenEntry> entries)
        {
            entries = null;
            if (payload == null || payload.Length == 0 || payload.Length % sizeof(int) != 0)
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                int count = payload.Length / sizeof(int);
                entries = new List<PacketOwnedItemMakerSessionHiddenEntry>(count);
                for (int i = 0; i < count; i++)
                {
                    int outputItemId = reader.ReadInt32();
                    if (requirePositiveOutputItemId && outputItemId <= 0)
                    {
                        entries = null;
                        return false;
                    }

                    entries.Add(new PacketOwnedItemMakerSessionHiddenEntry(-1, outputItemId));
                }

                return true;
            }
            catch (EndOfStreamException)
            {
                entries = null;
                return false;
            }
            catch (IOException)
            {
                entries = null;
                return false;
            }
            catch (InvalidDataException)
            {
                entries = null;
                return false;
            }
        }

        private static bool TryReadRemainingCountlessOutputOnlyHiddenRecipeEntries(
            BinaryReader reader,
            bool requirePositiveOutputItemId,
            out List<PacketOwnedItemMakerSessionHiddenEntry> entries)
        {
            entries = null;
            if (reader == null)
            {
                return false;
            }

            long startPosition = reader.BaseStream.Position;
            try
            {
                long remainingBytes = reader.BaseStream.Length - reader.BaseStream.Position;
                if (remainingBytes <= 0 || remainingBytes % sizeof(int) != 0)
                {
                    reader.BaseStream.Position = startPosition;
                    return false;
                }

                int count = checked((int)(remainingBytes / sizeof(int)));
                entries = new List<PacketOwnedItemMakerSessionHiddenEntry>(count);
                for (int i = 0; i < count; i++)
                {
                    int outputItemId = reader.ReadInt32();
                    if (requirePositiveOutputItemId && outputItemId <= 0)
                    {
                        entries = null;
                        reader.BaseStream.Position = startPosition;
                        return false;
                    }

                    entries.Add(new PacketOwnedItemMakerSessionHiddenEntry(-1, outputItemId));
                }

                return true;
            }
            catch (EndOfStreamException)
            {
                entries = null;
                reader.BaseStream.Position = startPosition;
                return false;
            }
            catch (IOException)
            {
                entries = null;
                reader.BaseStream.Position = startPosition;
                return false;
            }
            catch (InvalidDataException)
            {
                entries = null;
                reader.BaseStream.Position = startPosition;
                return false;
            }
            catch (OverflowException)
            {
                entries = null;
                reader.BaseStream.Position = startPosition;
                return false;
            }
        }

        private static bool TryReadHiddenRecipeEntries16(
            BinaryReader reader,
            HiddenRecipeEntryEncoding encoding,
            bool requirePositiveOutputItemId,
            string label,
            out List<PacketOwnedItemMakerSessionHiddenEntry> entries,
            bool useByteCount = false)
        {
            entries = null;
            if (reader == null)
            {
                return false;
            }

            long startPosition = reader.BaseStream.Position;
            try
            {
                int count = useByteCount
                    ? ReadNonNegativeCount8(reader, $"{label} count is missing or negative.")
                    : ReadNonNegativeCount16(reader, $"{label} count is missing or negative.");
                if (count <= 0)
                {
                    return true;
                }

                int entryWidth = GetHiddenRecipeEntryWidth(encoding);
                long requiredBytes = (long)entryWidth * count;
                if (reader.BaseStream.Length - reader.BaseStream.Position < requiredBytes)
                {
                    reader.BaseStream.Position = startPosition;
                    return false;
                }

                entries = new List<PacketOwnedItemMakerSessionHiddenEntry>(count);
                for (int i = 0; i < count; i++)
                {
                    ReadHiddenRecipeEntry(reader, encoding, out int bucketKey, out int outputItemId);

                    if (requirePositiveOutputItemId && outputItemId <= 0)
                    {
                        reader.BaseStream.Position = startPosition;
                        return false;
                    }

                    entries.Add(new PacketOwnedItemMakerSessionHiddenEntry(bucketKey, outputItemId));
                }

                return true;
            }
            catch (EndOfStreamException)
            {
                reader.BaseStream.Position = startPosition;
                return false;
            }
            catch (IOException)
            {
                reader.BaseStream.Position = startPosition;
                return false;
            }
            catch (InvalidDataException)
            {
                reader.BaseStream.Position = startPosition;
                return false;
            }
        }

        private static bool TryReadHiddenRecipeEntries32(
            BinaryReader reader,
            HiddenRecipeEntryEncoding encoding,
            bool requirePositiveOutputItemId,
            string label,
            out List<PacketOwnedItemMakerSessionHiddenEntry> entries)
        {
            entries = null;
            if (reader == null)
            {
                return false;
            }

            long startPosition = reader.BaseStream.Position;
            try
            {
                int count = ReadNonNegativeCount(reader, $"{label} count is missing or negative.");
                if (count <= 0)
                {
                    return true;
                }

                int entryWidth = GetHiddenRecipeEntryWidth(encoding);
                long requiredBytes = (long)entryWidth * count;
                if (reader.BaseStream.Length - reader.BaseStream.Position < requiredBytes)
                {
                    reader.BaseStream.Position = startPosition;
                    return false;
                }

                entries = new List<PacketOwnedItemMakerSessionHiddenEntry>(count);
                for (int i = 0; i < count; i++)
                {
                    ReadHiddenRecipeEntry(reader, encoding, out int bucketKey, out int outputItemId);

                    if (requirePositiveOutputItemId && outputItemId <= 0)
                    {
                        reader.BaseStream.Position = startPosition;
                        return false;
                    }

                    entries.Add(new PacketOwnedItemMakerSessionHiddenEntry(bucketKey, outputItemId));
                }

                return true;
            }
            catch (EndOfStreamException)
            {
                reader.BaseStream.Position = startPosition;
                return false;
            }
            catch (IOException)
            {
                reader.BaseStream.Position = startPosition;
                return false;
            }
            catch (InvalidDataException)
            {
                reader.BaseStream.Position = startPosition;
                return false;
            }
        }

        private static int GetHiddenRecipeEntryWidth(HiddenRecipeEntryEncoding encoding)
        {
            return encoding switch
            {
                HiddenRecipeEntryEncoding.Pair => sizeof(int) * 2,
                HiddenRecipeEntryEncoding.CompactBucketUInt16 => sizeof(ushort) + sizeof(int),
                HiddenRecipeEntryEncoding.CompactBucketByte => sizeof(byte) + sizeof(int),
                _ => sizeof(int)
            };
        }

        private static void ReadHiddenRecipeEntry(
            BinaryReader reader,
            HiddenRecipeEntryEncoding encoding,
            out int bucketKey,
            out int outputItemId)
        {
            switch (encoding)
            {
                case HiddenRecipeEntryEncoding.Pair:
                    bucketKey = reader.ReadInt32();
                    outputItemId = reader.ReadInt32();
                    break;

                case HiddenRecipeEntryEncoding.CompactBucketUInt16:
                    bucketKey = reader.ReadUInt16();
                    outputItemId = reader.ReadInt32();
                    break;

                case HiddenRecipeEntryEncoding.CompactBucketByte:
                    bucketKey = reader.ReadByte();
                    outputItemId = reader.ReadInt32();
                    break;

                default:
                    bucketKey = -1;
                    outputItemId = reader.ReadInt32();
                    break;
            }
        }

        private static List<PacketOwnedItemMakerDisassemblyTargetEntry> ReadDisassemblyTargetAdditions(
            BinaryReader reader,
            string label)
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

        private static List<PacketOwnedItemMakerDisassemblyTargetEntry> ReadDisassemblyTargetRemovals(
            BinaryReader reader,
            string label)
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

                entries.Add(new PacketOwnedItemMakerDisassemblyTargetEntry(slotIndex, itemId));
            }

            return entries;
        }

        private static List<PacketOwnedItemMakerDisassemblyTargetEntry> ReadDisassemblyTargetAdditions16(
            BinaryReader reader,
            string label)
        {
            int count = ReadNonNegativeCount16(reader, $"{label} count is missing or negative.");
            if (count <= 0)
            {
                return null;
            }

            long bytesRemaining = reader.BaseStream.Length - reader.BaseStream.Position;
            bool useCompactSlotWidth = bytesRemaining == count * (sizeof(ushort) + sizeof(int));
            List<PacketOwnedItemMakerDisassemblyTargetEntry> entries = new(count);
            for (int i = 0; i < count; i++)
            {
                int slotIndex;
                if (useCompactSlotWidth)
                {
                    EnsureReadable(reader, sizeof(ushort) + sizeof(int), $"{label} entry is truncated.");
                    slotIndex = reader.ReadUInt16();
                }
                else
                {
                    EnsureReadable(reader, sizeof(int) * 2, $"{label} entry is truncated.");
                    slotIndex = reader.ReadInt32();
                }

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

        private static List<PacketOwnedItemMakerDisassemblyTargetEntry> ReadDisassemblyTargetRemovals16(
            BinaryReader reader,
            string label)
        {
            int count = ReadNonNegativeCount16(reader, $"{label} count is missing or negative.");
            if (count <= 0)
            {
                return null;
            }

            long bytesRemaining = reader.BaseStream.Length - reader.BaseStream.Position;
            int wideWidth = sizeof(int) * 2;
            int compactWidth = sizeof(ushort) + sizeof(int);
            int slotOnlyWideWidth = sizeof(int);
            int slotOnlyCompactWidth = sizeof(ushort);
            int entryWidth = wideWidth;
            if (bytesRemaining == count * compactWidth)
            {
                entryWidth = compactWidth;
            }
            else if (bytesRemaining == count * slotOnlyWideWidth)
            {
                entryWidth = slotOnlyWideWidth;
            }
            else if (bytesRemaining == count * slotOnlyCompactWidth)
            {
                entryWidth = slotOnlyCompactWidth;
            }

            List<PacketOwnedItemMakerDisassemblyTargetEntry> entries = new(count);
            for (int i = 0; i < count; i++)
            {
                int slotIndex;
                int itemId;
                if (entryWidth == compactWidth)
                {
                    EnsureReadable(reader, compactWidth, $"{label} entry is truncated.");
                    slotIndex = reader.ReadUInt16();
                    itemId = reader.ReadInt32();
                }
                else if (entryWidth == slotOnlyWideWidth)
                {
                    EnsureReadable(reader, slotOnlyWideWidth, $"{label} entry is truncated.");
                    slotIndex = reader.ReadInt32();
                    itemId = 0;
                }
                else if (entryWidth == slotOnlyCompactWidth)
                {
                    EnsureReadable(reader, slotOnlyCompactWidth, $"{label} entry is truncated.");
                    slotIndex = reader.ReadUInt16();
                    itemId = 0;
                }
                else
                {
                    EnsureReadable(reader, wideWidth, $"{label} entry is truncated.");
                    slotIndex = reader.ReadInt32();
                    itemId = reader.ReadInt32();
                }

                if (slotIndex < 0)
                {
                    throw new InvalidDataException($"{label} slots must be zero-based and non-negative.");
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

            long bytesRemaining = reader.BaseStream.Length - reader.BaseStream.Position;
            int entryWidth = sizeof(int) * 2;
            bool outputOnlyEncoding = false;
            if (bytesRemaining == count * sizeof(int))
            {
                outputOnlyEncoding = true;
                entryWidth = sizeof(int);
            }

            List<PacketOwnedItemMakerSessionHiddenEntry> entries = new(count);
            for (int i = 0; i < count; i++)
            {
                EnsureReadable(reader, entryWidth, $"{label} entry is truncated.");
                int bucketKey;
                int outputItemId;
                if (outputOnlyEncoding)
                {
                    bucketKey = -1;
                    outputItemId = reader.ReadInt32();
                }
                else
                {
                    bucketKey = reader.ReadInt32();
                    outputItemId = reader.ReadInt32();
                }

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

            long bytesRemaining = reader.BaseStream.Length - reader.BaseStream.Position;
            int entryWidth = sizeof(int) * 2;
            bool outputOnlyEncoding = false;
            if (bytesRemaining == count * sizeof(int))
            {
                outputOnlyEncoding = true;
                entryWidth = sizeof(int);
            }

            List<PacketOwnedItemMakerSessionHiddenEntry> entries = new(count);
            for (int i = 0; i < count; i++)
            {
                EnsureReadable(reader, entryWidth, $"{label} entry is truncated.");
                int bucketKey;
                int outputItemId;
                if (outputOnlyEncoding)
                {
                    bucketKey = -1;
                    outputItemId = reader.ReadInt32();
                }
                else
                {
                    bucketKey = reader.ReadInt32();
                    outputItemId = reader.ReadInt32();
                }

                entries.Add(new PacketOwnedItemMakerSessionHiddenEntry(bucketKey, outputItemId));
            }

            return entries;
        }

        private static bool TryReadHiddenRecipeRemovalEntries16(
            BinaryReader reader,
            string label,
            out List<PacketOwnedItemMakerSessionHiddenEntry> entries)
        {
            return TryReadHiddenRecipeRemovalEntries(
                reader,
                label,
                useByteCount: false,
                out entries);
        }

        private static bool TryReadHiddenRecipeRemovalEntries8(
            BinaryReader reader,
            string label,
            out List<PacketOwnedItemMakerSessionHiddenEntry> entries)
        {
            return TryReadHiddenRecipeRemovalEntries(
                reader,
                label,
                useByteCount: true,
                out entries);
        }

        private static bool TryReadHiddenRecipeRemovalEntries(
            BinaryReader reader,
            string label,
            bool useByteCount,
            out List<PacketOwnedItemMakerSessionHiddenEntry> entries)
        {
            entries = null;
            if (reader == null)
            {
                return false;
            }

            long startPosition = reader.BaseStream.Position;
            try
            {
                int count = useByteCount
                    ? ReadNonNegativeCount8(reader, $"{label} count is missing or negative.")
                    : ReadNonNegativeCount16(reader, $"{label} count is missing or negative.");
                if (count <= 0)
                {
                    return true;
                }

                long bytesRemaining = reader.BaseStream.Length - reader.BaseStream.Position;
                int entryWidth = sizeof(int) * 2;
                bool outputOnlyEncoding = false;
                if (bytesRemaining == count * sizeof(int))
                {
                    outputOnlyEncoding = true;
                    entryWidth = sizeof(int);
                }

                entries = new List<PacketOwnedItemMakerSessionHiddenEntry>(count);
                for (int i = 0; i < count; i++)
                {
                    EnsureReadable(reader, entryWidth, $"{label} entry is truncated.");
                    int bucketKey;
                    int outputItemId;
                    if (outputOnlyEncoding)
                    {
                        bucketKey = -1;
                        outputItemId = reader.ReadInt32();
                    }
                    else
                    {
                        bucketKey = reader.ReadInt32();
                        outputItemId = reader.ReadInt32();
                    }

                    entries.Add(new PacketOwnedItemMakerSessionHiddenEntry(bucketKey, outputItemId));
                }

                return true;
            }
            catch (EndOfStreamException)
            {
                reader.BaseStream.Position = startPosition;
                return false;
            }
            catch (IOException)
            {
                reader.BaseStream.Position = startPosition;
                return false;
            }
            catch (InvalidDataException)
            {
                reader.BaseStream.Position = startPosition;
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

        private static int ReadNonNegativeCount16(BinaryReader reader, string message)
        {
            EnsureReadable(reader, sizeof(ushort), message);
            return reader.ReadUInt16();
        }

        private static int ReadNonNegativeCount8(BinaryReader reader, string message)
        {
            EnsureReadable(reader, sizeof(byte), message);
            return reader.ReadByte();
        }
    }
}
