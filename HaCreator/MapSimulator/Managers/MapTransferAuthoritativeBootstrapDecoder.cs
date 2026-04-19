using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator.Managers
{
    internal static class MapTransferAuthoritativeBootstrapDecoder
    {
        private const ulong CharacterDataMesoFlag = 0x2UL;
        private const ulong CharacterDataEquipInventoryFlag = 0x4UL;
        private const ulong CharacterDataUseInventoryFlag = 0x8UL;
        private const ulong CharacterDataSetupInventoryFlag = 0x10UL;
        private const ulong CharacterDataEtcInventoryFlag = 0x20UL;
        private const ulong CharacterDataCashInventoryFlag = 0x40UL;
        private const ulong CharacterDataInventorySlotLimitsFlag = 0x80UL;
        private const ulong CharacterDataSkillRecordFlag = 0x100UL;
        private const ulong CharacterDataSkillExpirationFlag = 0x200UL;
        private const ulong CharacterDataMiniGameRecordFlag = 0x400UL;
        private const ulong CharacterDataRelationshipRecordFlag = 0x800UL;
        internal const ulong CharacterDataMapTransferFlag = 0x1000UL;
        private const ulong CharacterDataSkillCooldownFlag = 0x4000UL;
        private const ulong CharacterDataInt16ValueRecordFlag = 0x8000UL;
        private const ulong CharacterDataQuestRecordFlag = 0x10000UL;
        private const ulong CharacterDataShortFileTimeRecordFlag = 0x20000UL;
        private const ulong CharacterDataTwoIntValueRecordFlag = 0x100000UL;
        private const int LogoutGiftConfigByteLength =
            sizeof(int) + (PacketStageTransitionRuntime.LogoutGiftEntryCount * sizeof(int));
        private const int SkillRecordBaseByteLength = sizeof(int) + sizeof(int);
        private const int SkillExpirationRecordByteLength = sizeof(int) + sizeof(long);
        private const int Int16ValueRecordByteLength = sizeof(int) + sizeof(ushort);
        private const int TwoIntValueRecordByteLength = sizeof(int) + sizeof(int);
        private const int MesoRecordByteLength = sizeof(int);
        private const int InventorySlotLimitRecordByteLength = 5 * sizeof(byte);
        private const int ShortFileTimeRecordByteLength = sizeof(ushort) + sizeof(long);
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
            out ulong matchedKnownLeadingSectionFlags,
            out int matchedOpaquePreMapTransferByteCount,
            out bool matchedKnownCharacterDataTail)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            ignoredTrailingLogoutGiftConfig = false;
            matchedExactTailBoundary = false;
            matchedKnownLeadingCharacterDataTail = false;
            matchedKnownLeadingSectionFlags = 0;
            matchedOpaquePreMapTransferByteCount = -1;
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
                        requireRealMap: false,
                        out regularFields,
                        out continentFields,
                        out matchedOffset,
                        out matchedKnownLeadingCharacterDataTail,
                        out matchedKnownLeadingSectionFlags,
                        out matchedOpaquePreMapTransferByteCount,
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
                        requireRealMap: true,
                        out regularFields,
                        out continentFields,
                        out matchedOffset,
                        out matchedKnownLeadingCharacterDataTail,
                        out matchedKnownLeadingSectionFlags,
                        out matchedOpaquePreMapTransferByteCount,
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
                        out matchedKnownLeadingSectionFlags,
                        out matchedOpaquePreMapTransferByteCount,
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
                    requireRealMap: false,
                    out regularFields,
                    out continentFields,
                    out matchedOffset,
                    out matchedKnownLeadingCharacterDataTail,
                    out matchedKnownLeadingSectionFlags,
                    out matchedOpaquePreMapTransferByteCount,
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
                    out matchedKnownLeadingSectionFlags,
                    out matchedOpaquePreMapTransferByteCount,
                    out matchedKnownCharacterDataTail))
            {
                return true;
            }

            return TryFindBootstrapBooksCore(
                payload,
                characterDataFlags,
                characterJobId,
                isPlausibleMapId,
                requireRealMap: true,
                out regularFields,
                out continentFields,
                out matchedOffset,
                out matchedKnownLeadingCharacterDataTail,
                out matchedKnownLeadingSectionFlags,
                out matchedOpaquePreMapTransferByteCount,
                out matchedKnownCharacterDataTail);
        }

        private static bool TryFindBootstrapBooksAtExactTail(
            ReadOnlySpan<byte> payload,
            ulong characterDataFlags,
            short characterJobId,
            Func<int, bool> isPlausibleMapId,
            bool requireRealMap,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset,
            out bool matchedKnownLeadingCharacterDataTail,
            out ulong matchedKnownLeadingSectionFlags,
            out int matchedOpaquePreMapTransferByteCount,
            out bool matchedKnownCharacterDataTail)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            matchedKnownLeadingCharacterDataTail = false;
            matchedKnownLeadingSectionFlags = 0;
            matchedOpaquePreMapTransferByteCount = -1;
            matchedKnownCharacterDataTail = false;

            if (payload.Length < BootstrapBookByteLength)
            {
                return false;
            }

            int tailOffset = payload.Length - BootstrapBookByteLength;
            if (!TryReadBootstrapBooksAtOffset(
                payload,
                tailOffset,
                characterDataFlags,
                characterJobId,
                isPlausibleMapId,
                requireRealMap,
                out regularFields,
                out continentFields,
                out matchedOffset,
                out matchedKnownLeadingCharacterDataTail,
                out matchedOpaquePreMapTransferByteCount,
                out matchedKnownCharacterDataTail))
            {
                return false;
            }

            if (TryMatchKnownLeadingLayoutOffset(
                    payload,
                    characterDataFlags,
                    tailOffset,
                    out KnownLeadingOffsetCandidate candidate))
            {
                matchedKnownLeadingCharacterDataTail = true;
                matchedKnownLeadingSectionFlags = candidate.MatchedSectionFlags;
                matchedOpaquePreMapTransferByteCount = candidate.OpaquePreMapTransferByteCount;
            }

            return true;
        }

        private static bool TryFindBootstrapBooksCore(
            ReadOnlySpan<byte> payload,
            ulong characterDataFlags,
            short characterJobId,
            Func<int, bool> isPlausibleMapId,
            bool requireRealMap,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset,
            out bool matchedKnownLeadingCharacterDataTail,
            out ulong matchedKnownLeadingSectionFlags,
            out int matchedOpaquePreMapTransferByteCount,
            out bool matchedKnownCharacterDataTail)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            matchedKnownLeadingCharacterDataTail = false;
            matchedKnownLeadingSectionFlags = 0;
            matchedOpaquePreMapTransferByteCount = -1;
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
                        requireRealMap,
                        out regularFields,
                        out continentFields,
                        out matchedOffset,
                        out matchedKnownLeadingCharacterDataTail,
                        out matchedOpaquePreMapTransferByteCount,
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
            bool requireRealMap,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset,
            out bool matchedKnownLeadingCharacterDataTail,
            out int matchedOpaquePreMapTransferByteCount,
            out bool matchedKnownCharacterDataTail)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            matchedKnownLeadingCharacterDataTail = false;
            matchedOpaquePreMapTransferByteCount = -1;
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

            if (requireRealMap && seenMaps.Count == 0)
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
            out ulong matchedKnownLeadingSectionFlags,
            out int matchedOpaquePreMapTransferByteCount,
            out bool matchedKnownCharacterDataTail)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            matchedKnownLeadingCharacterDataTail = false;
            matchedKnownLeadingSectionFlags = 0;
            matchedOpaquePreMapTransferByteCount = -1;
            matchedKnownCharacterDataTail = false;

            if (payload.Length < BootstrapBookByteLength || isPlausibleMapId == null)
            {
                return false;
            }

            Dictionary<int, KnownLeadingOffsetCandidate> candidateOffsets = new();
            AddKnownLeadingLayoutOffsets(payload, characterDataFlags, candidateOffsets);
            foreach (KeyValuePair<int, KnownLeadingOffsetCandidate> candidate in candidateOffsets)
            {
                int candidateOffset = candidate.Key;
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
                        requireRealMap: false,
                        out regularFields,
                        out continentFields,
                        out matchedOffset,
                        out _,
                        out _,
                        out matchedKnownCharacterDataTail))
                {
                    matchedKnownLeadingCharacterDataTail = true;
                    matchedKnownLeadingSectionFlags = candidate.Value.MatchedSectionFlags;
                    matchedOpaquePreMapTransferByteCount = candidate.Value.OpaquePreMapTransferByteCount;
                    return true;
                }
            }

            return false;
        }

        private static bool TryMatchKnownLeadingLayoutOffset(
            ReadOnlySpan<byte> payload,
            ulong characterDataFlags,
            int offset,
            out KnownLeadingOffsetCandidate candidate)
        {
            candidate = default;
            if (offset <= 0)
            {
                return false;
            }

            Dictionary<int, KnownLeadingOffsetCandidate> candidateOffsets = new();
            AddKnownLeadingLayoutOffsets(payload, characterDataFlags, candidateOffsets);
            return candidateOffsets.TryGetValue(offset, out candidate);
        }

        private static void AddKnownLeadingLayoutOffsets(ReadOnlySpan<byte> payload, ulong characterDataFlags, IDictionary<int, KnownLeadingOffsetCandidate> offsets)
        {
            if (offsets == null || payload.Length < sizeof(ushort) + BootstrapBookByteLength)
            {
                return;
            }

            HashSet<KnownLeadingOffsetCandidate> candidateStarts = new()
            {
                new(0, -1, 0)
            };

            if ((characterDataFlags & CharacterDataMesoFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetExactNextOffsets(TrySkipMesoRecord),
                    CharacterDataMesoFlag);
            }

            if ((characterDataFlags & CharacterDataInventorySlotLimitsFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetExactNextOffsets(TrySkipInventorySlotLimitsRecord),
                    CharacterDataInventorySlotLimitsFlag);
            }

            if ((characterDataFlags & CharacterDataTwoIntValueRecordFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetExactNextOffsets(TrySkipTwoIntValueRecord),
                    CharacterDataTwoIntValueRecordFlag);
            }

            if ((characterDataFlags & CharacterDataEquipInventoryFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetExactNextOffsets(TrySkipInventorySection),
                    CharacterDataEquipInventoryFlag);
            }

            if ((characterDataFlags & CharacterDataUseInventoryFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetExactNextOffsets(TrySkipInventorySection),
                    CharacterDataUseInventoryFlag);
            }

            if ((characterDataFlags & CharacterDataSetupInventoryFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetExactNextOffsets(TrySkipInventorySection),
                    CharacterDataSetupInventoryFlag);
            }

            if ((characterDataFlags & CharacterDataEtcInventoryFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetExactNextOffsets(TrySkipInventorySection),
                    CharacterDataEtcInventoryFlag);
            }

            if ((characterDataFlags & CharacterDataCashInventoryFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetExactNextOffsets(TrySkipInventorySection),
                    CharacterDataCashInventoryFlag);
            }

            if ((characterDataFlags & CharacterDataSkillRecordFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetPossibleSkillRecordGroupOffsets,
                    CharacterDataSkillRecordFlag);
            }

            if ((characterDataFlags & CharacterDataSkillExpirationFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetExactNextOffsets(TrySkipSkillExpirationRecordGroup),
                    CharacterDataSkillExpirationFlag);
            }

            if ((characterDataFlags & CharacterDataSkillCooldownFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetExactNextOffsets(TrySkipInt16ValueRecordGroup),
                    CharacterDataSkillCooldownFlag);
            }

            if ((characterDataFlags & CharacterDataInt16ValueRecordFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetExactNextOffsets(TrySkipInt16ValueRecordGroup),
                    CharacterDataInt16ValueRecordFlag);
                candidateStarts = ExpandOpaquePreMapTransferOffsets(
                    candidateStarts,
                    payload,
                    CharacterDataInt16ValueRecordFlag);
            }

            if ((characterDataFlags & CharacterDataQuestRecordFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetExactNextOffsets(TrySkipQuestRecordGroup),
                    CharacterDataQuestRecordFlag);
            }

            if ((characterDataFlags & CharacterDataShortFileTimeRecordFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetExactNextOffsets(TrySkipShortFileTimeRecordGroup),
                    CharacterDataShortFileTimeRecordFlag);
            }

            if ((characterDataFlags & CharacterDataMiniGameRecordFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetExactNextOffsets(TrySkipMiniGameRecordGroup),
                    CharacterDataMiniGameRecordFlag);
            }

            if ((characterDataFlags & CharacterDataRelationshipRecordFlag) != 0)
            {
                candidateStarts = ExtendKnownLeadingOffsets(
                    candidateStarts,
                    payload,
                    GetExactNextOffsets(TrySkipRelationshipRecordGroups),
                    CharacterDataRelationshipRecordFlag);
            }

            foreach (KnownLeadingOffsetCandidate candidate in candidateStarts)
            {
                if (!offsets.TryGetValue(candidate.Offset, out KnownLeadingOffsetCandidate existingCandidate) ||
                    ShouldPreferKnownLeadingOffsetCandidate(candidate.OpaquePreMapTransferByteCount, existingCandidate.OpaquePreMapTransferByteCount))
                {
                    offsets[candidate.Offset] = candidate;
                }
            }
        }

        private static bool ShouldPreferKnownLeadingOffsetCandidate(int candidateOpaqueByteCount, int existingOpaqueByteCount)
        {
            if (candidateOpaqueByteCount < 0)
            {
                return true;
            }

            if (existingOpaqueByteCount < 0)
            {
                return false;
            }

            return candidateOpaqueByteCount < existingOpaqueByteCount;
        }

        private static HashSet<KnownLeadingOffsetCandidate> ExtendKnownLeadingOffsets(
            IEnumerable<KnownLeadingOffsetCandidate> sourceOffsets,
            ReadOnlySpan<byte> payload,
            CollectNextOffsets collectNextOffsets,
            ulong sectionFlag)
        {
            HashSet<KnownLeadingOffsetCandidate> extended = new();
            foreach (KnownLeadingOffsetCandidate source in sourceOffsets)
            {
                extended.Add(source);
                foreach (int nextOffset in collectNextOffsets(payload, source.Offset))
                {
                    extended.Add(source with
                    {
                        Offset = nextOffset,
                        MatchedSectionFlags = source.MatchedSectionFlags | sectionFlag
                    });
                }
            }

            return extended;
        }

        private static HashSet<KnownLeadingOffsetCandidate> ExpandOpaquePreMapTransferOffsets(
            IEnumerable<KnownLeadingOffsetCandidate> sourceOffsets,
            ReadOnlySpan<byte> payload,
            ulong sectionFlag)
        {
            HashSet<KnownLeadingOffsetCandidate> expanded = new();
            int maximumOffset = Math.Max(0, payload.Length - BootstrapBookByteLength);
            foreach (KnownLeadingOffsetCandidate source in sourceOffsets)
            {
                if ((uint)source.Offset > maximumOffset)
                {
                    continue;
                }

                for (int nextOffset = source.Offset; nextOffset <= maximumOffset; nextOffset++)
                {
                    expanded.Add(new KnownLeadingOffsetCandidate(
                        nextOffset,
                        nextOffset - source.Offset,
                        nextOffset > source.Offset
                            ? source.MatchedSectionFlags | sectionFlag
                            : source.MatchedSectionFlags));
                }
            }

            return expanded;
        }

        private readonly record struct KnownLeadingOffsetCandidate(
            int Offset,
            int OpaquePreMapTransferByteCount,
            ulong MatchedSectionFlags);

        private delegate IEnumerable<int> CollectNextOffsets(ReadOnlySpan<byte> payload, int offset);
        private delegate bool TrySkipRecordGroup(ReadOnlySpan<byte> payload, int offset, out int nextOffset);

        private static CollectNextOffsets GetExactNextOffsets(TrySkipRecordGroup skipGroup)
        {
            return (payload, offset) =>
            {
                if (skipGroup(payload, offset, out int nextOffset))
                {
                    return new[] { nextOffset };
                }

                return Array.Empty<int>();
            };
        }

        private static IEnumerable<int> GetPossibleSkillRecordGroupOffsets(ReadOnlySpan<byte> payload, int offset)
        {
            List<int> offsets = new();
            if ((uint)offset > payload.Length || payload.Length - offset < sizeof(ushort))
            {
                return offsets;
            }

            ushort count = BitConverter.ToUInt16(payload.Slice(offset, sizeof(ushort)));
            int minimumSectionByteLength = sizeof(ushort) + (count * SkillRecordBaseByteLength);
            if (payload.Length - offset < minimumSectionByteLength)
            {
                return offsets;
            }

            int nextOffset = offset + sizeof(ushort);
            for (int i = 0; i < count; i++)
            {
                int skillId = BitConverter.ToInt32(payload.Slice(nextOffset, sizeof(int)));
                nextOffset += SkillRecordBaseByteLength;
                if (PacketStageTransitionRuntime.IsSkillNeedMasterLevel(skillId))
                {
                    if (payload.Length - nextOffset < sizeof(int))
                    {
                        return offsets;
                    }

                    nextOffset += sizeof(int);
                }
            }

            offsets.Add(nextOffset);
            return offsets;
        }

        private static bool TrySkipMesoRecord(ReadOnlySpan<byte> payload, int offset, out int nextOffset)
        {
            nextOffset = offset;
            if ((uint)offset > payload.Length || payload.Length - offset < MesoRecordByteLength)
            {
                return false;
            }

            nextOffset = offset + MesoRecordByteLength;
            return true;
        }

        private static bool TrySkipInventorySlotLimitsRecord(ReadOnlySpan<byte> payload, int offset, out int nextOffset)
        {
            nextOffset = offset;
            if ((uint)offset > payload.Length || payload.Length - offset < InventorySlotLimitRecordByteLength)
            {
                return false;
            }

            nextOffset = offset + InventorySlotLimitRecordByteLength;
            return true;
        }

        private static bool TrySkipInventorySection(ReadOnlySpan<byte> payload, int offset, out int nextOffset)
        {
            nextOffset = offset;
            if ((uint)offset > payload.Length)
            {
                return false;
            }

            int cursor = offset;
            while (true)
            {
                if (payload.Length - cursor < sizeof(short))
                {
                    return false;
                }

                short inventoryPosition = BitConverter.ToInt16(payload.Slice(cursor, sizeof(short)));
                cursor += sizeof(short);
                if (inventoryPosition == 0)
                {
                    nextOffset = cursor;
                    return true;
                }

                if (!TrySkipInventoryItemSlot(payload, cursor, out cursor))
                {
                    return false;
                }
            }
        }

        private static bool TrySkipInventoryItemSlot(ReadOnlySpan<byte> payload, int offset, out int nextOffset)
        {
            nextOffset = offset;
            if ((uint)offset > payload.Length || payload.Length - offset < sizeof(byte) + sizeof(int) + sizeof(byte))
            {
                return false;
            }

            int cursor = offset;
            byte itemType = payload[cursor];
            cursor += sizeof(byte);
            int itemId = BitConverter.ToInt32(payload.Slice(cursor, sizeof(int)));
            cursor += sizeof(int);
            bool hasCashSerial = payload[cursor] != 0;
            cursor += sizeof(byte);

            if (hasCashSerial)
            {
                if (payload.Length - cursor < sizeof(long))
                {
                    return false;
                }

                cursor += sizeof(long);
            }

            if (payload.Length - cursor < sizeof(long))
            {
                return false;
            }

            cursor += sizeof(long); // dateExpire
            switch (itemType)
            {
                case 1:
                    if (payload.Length - cursor < sizeof(byte) + sizeof(byte))
                    {
                        return false;
                    }

                    cursor += sizeof(byte) + sizeof(byte);
                    if (payload.Length - cursor < 14 * sizeof(short))
                    {
                        return false;
                    }

                    cursor += 14 * sizeof(short);
                    if (!TrySkipMapleString(payload, cursor, out cursor))
                    {
                        return false;
                    }

                    int equipTrailerByteLength = sizeof(short) + sizeof(byte) + sizeof(byte) + (3 * sizeof(int))
                        + (2 * sizeof(byte)) + (5 * sizeof(short)) + (2 * sizeof(long)) + sizeof(int);
                    if (payload.Length - cursor < equipTrailerByteLength)
                    {
                        return false;
                    }

                    cursor += equipTrailerByteLength;
                    break;
                case 2:
                    int useBaseByteLength = sizeof(ushort);
                    if (payload.Length - cursor < useBaseByteLength)
                    {
                        return false;
                    }

                    cursor += useBaseByteLength;
                    if (!TrySkipMapleString(payload, cursor, out cursor))
                    {
                        return false;
                    }

                    if (payload.Length - cursor < sizeof(short))
                    {
                        return false;
                    }

                    cursor += sizeof(short);
                    if ((itemId / 10000) is 207 or 233)
                    {
                        if (payload.Length - cursor < sizeof(long))
                        {
                            return false;
                        }

                        cursor += sizeof(long);
                    }

                    break;
                case 3:
                    int petByteLength = 13 + sizeof(byte) + sizeof(short) + sizeof(byte) + sizeof(long)
                        + sizeof(short) + sizeof(ushort) + sizeof(int) + sizeof(short);
                    if (payload.Length - cursor < petByteLength)
                    {
                        return false;
                    }

                    cursor += petByteLength;
                    break;
                default:
                    return false;
            }

            nextOffset = cursor;
            return true;
        }

        private static bool TrySkipMapleString(ReadOnlySpan<byte> payload, int offset, out int nextOffset)
        {
            nextOffset = offset;
            if ((uint)offset > payload.Length || payload.Length - offset < sizeof(ushort))
            {
                return false;
            }

            ushort length = BitConverter.ToUInt16(payload.Slice(offset, sizeof(ushort)));
            int cursor = offset + sizeof(ushort);
            if (payload.Length - cursor < length)
            {
                return false;
            }

            nextOffset = cursor + length;
            return true;
        }

        private static bool TrySkipSkillExpirationRecordGroup(ReadOnlySpan<byte> payload, int offset, out int nextOffset)
        {
            return TrySkipFixedRecordGroup(payload, offset, SkillExpirationRecordByteLength, out nextOffset);
        }

        private static bool TrySkipInt16ValueRecordGroup(ReadOnlySpan<byte> payload, int offset, out int nextOffset)
        {
            return TrySkipFixedRecordGroup(payload, offset, Int16ValueRecordByteLength, out nextOffset);
        }

        private static bool TrySkipTwoIntValueRecord(ReadOnlySpan<byte> payload, int offset, out int nextOffset)
        {
            nextOffset = offset;
            if ((uint)offset > payload.Length || payload.Length - offset < TwoIntValueRecordByteLength)
            {
                return false;
            }

            nextOffset = offset + TwoIntValueRecordByteLength;
            return true;
        }

        private static bool TrySkipQuestRecordGroup(ReadOnlySpan<byte> payload, int offset, out int nextOffset)
        {
            nextOffset = offset;
            if ((uint)offset > payload.Length || payload.Length - offset < sizeof(ushort))
            {
                return false;
            }

            ushort count = BitConverter.ToUInt16(payload.Slice(offset, sizeof(ushort)));
            nextOffset += sizeof(ushort);
            for (int i = 0; i < count; i++)
            {
                if (payload.Length - nextOffset < sizeof(ushort))
                {
                    return false;
                }

                nextOffset += sizeof(ushort);
                if (payload.Length - nextOffset < sizeof(ushort))
                {
                    return false;
                }

                ushort stringLength = BitConverter.ToUInt16(payload.Slice(nextOffset, sizeof(ushort)));
                nextOffset += sizeof(ushort);
                if (payload.Length - nextOffset < stringLength)
                {
                    return false;
                }

                nextOffset += stringLength;
            }

            return true;
        }

        private static bool TrySkipShortFileTimeRecordGroup(ReadOnlySpan<byte> payload, int offset, out int nextOffset)
        {
            return TrySkipFixedRecordGroup(payload, offset, ShortFileTimeRecordByteLength, out nextOffset);
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
