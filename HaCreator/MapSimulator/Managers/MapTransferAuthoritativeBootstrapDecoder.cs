using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    internal static class MapTransferAuthoritativeBootstrapDecoder
    {
        internal const ulong CharacterDataMapTransferFlag = 0x1000UL;
        private const int LogoutGiftConfigByteLength = 3 * sizeof(int);
        private const int MiniGameRecordByteLength = 0x14;
        private const int CoupleRecordByteLength = 0x21;
        private const int FriendRecordByteLength = 0x25;
        private const int MarriageRecordByteLength = 0x30;
        internal const int BootstrapBookByteLength =
            (MapTransferRuntimeManager.RegularCapacity + MapTransferRuntimeManager.ContinentCapacity) * sizeof(int);

        internal static bool TryFindBootstrapBooks(
            ReadOnlySpan<byte> payload,
            ulong characterDataFlags,
            short characterJobId,
            Func<int, bool> isPlausibleMapId,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset,
            out bool ignoredTrailingLogoutGiftConfig,
            out bool matchedExactTailBoundary,
            out bool matchedKnownLeadingCharacterDataTail,
            out bool matchedKnownCharacterDataTail)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            ignoredTrailingLogoutGiftConfig = false;
            matchedExactTailBoundary = false;
            matchedKnownLeadingCharacterDataTail = false;
            matchedKnownCharacterDataTail = false;

            if (payload.Length < BootstrapBookByteLength || isPlausibleMapId == null)
            {
                return false;
            }

            if (payload.Length >= BootstrapBookByteLength + LogoutGiftConfigByteLength)
            {
                ReadOnlySpan<byte> leadingPayload = payload[..^LogoutGiftConfigByteLength];
                if (TryFindBootstrapBooksAtExactTail(
                        leadingPayload,
                        characterDataFlags,
                        characterJobId,
                        isPlausibleMapId,
                        out regularFields,
                        out continentFields,
                        out matchedOffset,
                        out matchedKnownLeadingCharacterDataTail,
                        out matchedKnownCharacterDataTail))
                {
                    ignoredTrailingLogoutGiftConfig = true;
                    matchedExactTailBoundary = true;
                    return true;
                }

                if (TryFindBootstrapBooksCore(
                        leadingPayload,
                        characterDataFlags,
                        characterJobId,
                        isPlausibleMapId,
                        out regularFields,
                        out continentFields,
                        out matchedOffset,
                        out matchedKnownLeadingCharacterDataTail,
                        out matchedKnownCharacterDataTail))
                {
                    ignoredTrailingLogoutGiftConfig = true;
                    return true;
                }

                if (TryFindBootstrapBooksFromKnownLeadingLayouts(
                        leadingPayload,
                        characterDataFlags,
                        characterJobId,
                        isPlausibleMapId,
                        out regularFields,
                        out continentFields,
                        out matchedOffset,
                        out matchedKnownLeadingCharacterDataTail,
                        out matchedKnownCharacterDataTail))
                {
                    ignoredTrailingLogoutGiftConfig = true;
                    return true;
                }
            }

            if (TryFindBootstrapBooksAtExactTail(
                    payload,
                    characterDataFlags,
                    characterJobId,
                    isPlausibleMapId,
                    out regularFields,
                    out continentFields,
                    out matchedOffset,
                    out matchedKnownLeadingCharacterDataTail,
                    out matchedKnownCharacterDataTail))
            {
                matchedExactTailBoundary = true;
                return true;
            }

            if (TryFindBootstrapBooksFromKnownLeadingLayouts(
                    payload,
                    characterDataFlags,
                    characterJobId,
                    isPlausibleMapId,
                    out regularFields,
                    out continentFields,
                    out matchedOffset,
                    out matchedKnownLeadingCharacterDataTail,
                    out matchedKnownCharacterDataTail))
            {
                return true;
            }

            return TryFindBootstrapBooksCore(
                payload,
                characterDataFlags,
                characterJobId,
                isPlausibleMapId,
                out regularFields,
                out continentFields,
                out matchedOffset,
                out matchedKnownLeadingCharacterDataTail,
                out matchedKnownCharacterDataTail);
        }

        private static bool TryFindBootstrapBooksAtExactTail(
            ReadOnlySpan<byte> payload,
            ulong characterDataFlags,
            short characterJobId,
            Func<int, bool> isPlausibleMapId,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset,
            out bool matchedKnownLeadingCharacterDataTail,
            out bool matchedKnownCharacterDataTail)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            matchedKnownLeadingCharacterDataTail = false;
            matchedKnownCharacterDataTail = false;

            if (payload.Length < BootstrapBookByteLength)
            {
                return false;
            }

            int tailOffset = payload.Length - BootstrapBookByteLength;
            return TryReadBootstrapBooksAtOffset(
                payload,
                tailOffset,
                characterDataFlags,
                characterJobId,
                isPlausibleMapId,
                out regularFields,
                out continentFields,
                out matchedOffset,
                out matchedKnownLeadingCharacterDataTail,
                out matchedKnownCharacterDataTail);
        }

        private static bool TryFindBootstrapBooksCore(
            ReadOnlySpan<byte> payload,
            ulong characterDataFlags,
            short characterJobId,
            Func<int, bool> isPlausibleMapId,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset,
            out bool matchedKnownLeadingCharacterDataTail,
            out bool matchedKnownCharacterDataTail)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            matchedKnownLeadingCharacterDataTail = false;
            matchedKnownCharacterDataTail = false;

            if (payload.Length < BootstrapBookByteLength || isPlausibleMapId == null)
            {
                return false;
            }

            for (int offset = payload.Length - BootstrapBookByteLength; offset >= 0; offset--)
            {
                if (TryReadBootstrapBooksAtOffset(
                        payload,
                        offset,
                        characterDataFlags,
                        characterJobId,
                        isPlausibleMapId,
                        out regularFields,
                        out continentFields,
                        out matchedOffset,
                        out matchedKnownLeadingCharacterDataTail,
                        out matchedKnownCharacterDataTail))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadBootstrapBooksAtOffset(
            ReadOnlySpan<byte> payload,
            int offset,
            ulong characterDataFlags,
            short characterJobId,
            Func<int, bool> isPlausibleMapId,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset,
            out bool matchedKnownLeadingCharacterDataTail,
            out bool matchedKnownCharacterDataTail)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            matchedKnownLeadingCharacterDataTail = false;
            matchedKnownCharacterDataTail = false;

            int slotCount = MapTransferRuntimeManager.RegularCapacity + MapTransferRuntimeManager.ContinentCapacity;
            if (offset < 0 || payload.Length - offset < BootstrapBookByteLength || isPlausibleMapId == null)
            {
                return false;
            }

            int[] candidateRegular = new int[MapTransferRuntimeManager.RegularCapacity];
            int[] candidateContinent = new int[MapTransferRuntimeManager.ContinentCapacity];
            HashSet<int> seenMaps = new();

            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                int mapId = BitConverter.ToInt32(payload.Slice(offset + (slotIndex * sizeof(int)), sizeof(int)));
                if (!IsAcceptedBootstrapValue(mapId, isPlausibleMapId))
                {
                    return false;
                }

                if (slotIndex < MapTransferRuntimeManager.RegularCapacity)
                {
                    candidateRegular[slotIndex] = mapId;
                }
                else
                {
                    candidateContinent[slotIndex - MapTransferRuntimeManager.RegularCapacity] = mapId;
                }

                if (mapId > 0 && mapId != MapTransferRuntimeManager.EmptyDestinationMapId)
                {
                    seenMaps.Add(mapId);
                }
            }

            if (seenMaps.Count == 0)
            {
                return false;
            }

            if (!TryValidatePostMapTransferTail(
                    payload[(offset + BootstrapBookByteLength)..],
                    characterDataFlags,
                    characterJobId,
                    out bool matchedKnownTail))
            {
                return false;
            }

            regularFields = candidateRegular;
            continentFields = candidateContinent;
            matchedOffset = offset;
            matchedKnownCharacterDataTail = matchedKnownTail;
            return true;
        }

        private static bool TryFindBootstrapBooksFromKnownLeadingLayouts(
            ReadOnlySpan<byte> payload,
            ulong characterDataFlags,
            short characterJobId,
            Func<int, bool> isPlausibleMapId,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset,
            out bool matchedKnownLeadingCharacterDataTail,
            out bool matchedKnownCharacterDataTail)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            matchedKnownLeadingCharacterDataTail = false;
            matchedKnownCharacterDataTail = false;

            if (payload.Length < BootstrapBookByteLength || isPlausibleMapId == null)
            {
                return false;
            }

            HashSet<int> candidateOffsets = new();
            AddKnownLeadingLayoutOffsets(payload, candidateOffsets);
            foreach (int candidateOffset in candidateOffsets)
            {
                if (candidateOffset <= 0)
                {
                    continue;
                }

                if (TryReadBootstrapBooksAtOffset(
                        payload,
                        candidateOffset,
                        characterDataFlags,
                        characterJobId,
                        isPlausibleMapId,
                        out regularFields,
                        out continentFields,
                        out matchedOffset,
                        out _,
                        out matchedKnownCharacterDataTail))
                {
                    matchedKnownLeadingCharacterDataTail = true;
                    return true;
                }
            }

            return false;
        }

        private static void AddKnownLeadingLayoutOffsets(ReadOnlySpan<byte> payload, ISet<int> offsets)
        {
            if (offsets == null || payload.Length < sizeof(ushort) + BootstrapBookByteLength)
            {
                return;
            }

            if (TrySkipMiniGameRecordGroup(payload, 0, out int miniGameOffset))
            {
                offsets.Add(miniGameOffset);

                if (TrySkipRelationshipRecordGroups(payload, miniGameOffset, out int miniGameRelationshipOffset))
                {
                    offsets.Add(miniGameRelationshipOffset);
                }
            }

            if (TrySkipRelationshipRecordGroups(payload, 0, out int relationshipOffset))
            {
                offsets.Add(relationshipOffset);
            }
        }

        private static bool TrySkipMiniGameRecordGroup(ReadOnlySpan<byte> payload, int offset, out int nextOffset)
        {
            return TrySkipFixedRecordGroup(payload, offset, MiniGameRecordByteLength, out nextOffset);
        }

        private static bool TrySkipRelationshipRecordGroups(ReadOnlySpan<byte> payload, int offset, out int nextOffset)
        {
            nextOffset = offset;
            if (!TrySkipFixedRecordGroup(payload, nextOffset, CoupleRecordByteLength, out nextOffset))
            {
                return false;
            }

            if (!TrySkipFixedRecordGroup(payload, nextOffset, FriendRecordByteLength, out nextOffset))
            {
                return false;
            }

            return TrySkipFixedRecordGroup(payload, nextOffset, MarriageRecordByteLength, out nextOffset);
        }

        private static bool TrySkipFixedRecordGroup(ReadOnlySpan<byte> payload, int offset, int recordByteLength, out int nextOffset)
        {
            nextOffset = offset;
            if ((uint)offset > payload.Length || recordByteLength <= 0 || payload.Length - offset < sizeof(ushort))
            {
                return false;
            }

            ushort count = BitConverter.ToUInt16(payload.Slice(offset, sizeof(ushort)));
            int sectionByteLength = sizeof(ushort) + (count * recordByteLength);
            if (payload.Length - offset < sectionByteLength)
            {
                return false;
            }

            nextOffset = offset + sectionByteLength;
            return true;
        }

        private static bool IsAcceptedBootstrapValue(int mapId, Func<int, bool> isPlausibleMapId)
        {
            return mapId == 0 ||
                   mapId == MapTransferRuntimeManager.EmptyDestinationMapId ||
                   isPlausibleMapId(mapId);
        }

        private static bool TryValidatePostMapTransferTail(
            ReadOnlySpan<byte> payload,
            ulong characterDataFlags,
            short characterJobId,
            out bool matchedKnownTail)
        {
            matchedKnownTail = false;
            if (payload.Length == 0)
            {
                matchedKnownTail = true;
                return true;
            }

            try
            {
                using MemoryStream stream = new(payload.ToArray(), writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);

                if ((characterDataFlags & 0x40000UL) != 0)
                {
                    int newYearCardCount = reader.ReadUInt16();
                    for (int i = 0; i < newYearCardCount; i++)
                    {
                        _ = reader.ReadInt32();
                        _ = reader.ReadInt32();
                        _ = ReadMapleString(reader);
                        _ = reader.ReadByte();
                        _ = reader.ReadInt64();
                        _ = reader.ReadInt32();
                        _ = ReadMapleString(reader);
                        _ = reader.ReadByte();
                        _ = reader.ReadByte();
                        _ = reader.ReadInt64();
                        _ = ReadMapleString(reader);
                    }
                }

                if ((characterDataFlags & 0x80000UL) != 0)
                {
                    int questExCount = reader.ReadUInt16();
                    for (int i = 0; i < questExCount; i++)
                    {
                        _ = reader.ReadUInt16();
                        _ = ReadMapleString(reader);
                    }
                }

                if ((characterDataFlags & 0x200000UL) != 0 &&
                    characterJobId / 100 == 33)
                {
                    _ = reader.ReadByte();
                    for (int i = 0; i < 5; i++)
                    {
                        _ = reader.ReadInt32();
                    }
                }

                if ((characterDataFlags & 0x400000UL) != 0)
                {
                    int questCompleteCount = reader.ReadUInt16();
                    for (int i = 0; i < questCompleteCount; i++)
                    {
                        _ = reader.ReadUInt16();
                        _ = reader.ReadInt64();
                    }
                }

                if ((characterDataFlags & 0x800000UL) != 0)
                {
                    int visitorQuestCount = reader.ReadUInt16();
                    for (int i = 0; i < visitorQuestCount; i++)
                    {
                        _ = reader.ReadUInt16();
                        _ = reader.ReadUInt16();
                    }
                }

                matchedKnownTail = stream.Position == stream.Length;
                return matchedKnownTail;
            }
            catch (Exception) when (payload.Length > 0)
            {
                return false;
            }
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            int length = reader.ReadUInt16();
            if (length <= 0)
            {
                return string.Empty;
            }

            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Maple string exceeded the remaining post-map-transfer tail payload.");
            }

            return Encoding.Default.GetString(bytes);
        }
    }
}
