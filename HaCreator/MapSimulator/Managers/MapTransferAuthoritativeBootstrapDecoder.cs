using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HaCreator.MapSimulator.Interaction;

using BinaryReader = MapleLib.PacketLib.PacketReader;
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
        private const int SetFieldServerFileTimeByteLength = sizeof(long);
        private const int MaximumOpaquePostMapTransferTailByteLength = 64;
        private const int MaximumOpaqueBetweenLogoutGiftAndServerFileTimeByteLength = 256;
        private const int MaximumOpaqueBeforeServerFileTimeByteLength = 256;
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
                out matchedKnownCharacterDataTail,
                out _))
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

            int bestTailCandidateScore = int.MinValue;
            int bestMatchedOffset = -1;
            int[] bestRegularFields = null;
            int[] bestContinentFields = null;
            bool bestMatchedKnownLeadingCharacterDataTail = false;
            int bestMatchedOpaquePreMapTransferByteCount = -1;
            bool bestMatchedKnownCharacterDataTail = false;
            for (int offset = payload.Length - BootstrapBookByteLength; offset >= 0; offset--)
            {
                if (TryReadBootstrapBooksAtOffset(
                        payload,
                        offset,
                        characterDataFlags,
                        characterJobId,
                        isPlausibleMapId,
                        requireRealMap,
                        out int[] candidateRegularFields,
                        out int[] candidateContinentFields,
                        out int candidateMatchedOffset,
                        out bool candidateMatchedKnownLeadingCharacterDataTail,
                        out int candidateMatchedOpaquePreMapTransferByteCount,
                        out bool candidateMatchedKnownCharacterDataTail,
                        out int tailCandidateScore))
                {
                    if (tailCandidateScore > bestTailCandidateScore ||
                        (tailCandidateScore == bestTailCandidateScore &&
                         candidateMatchedOffset > bestMatchedOffset))
                    {
                        bestTailCandidateScore = tailCandidateScore;
                        bestMatchedOffset = candidateMatchedOffset;
                        bestRegularFields = candidateRegularFields;
                        bestContinentFields = candidateContinentFields;
                        bestMatchedKnownLeadingCharacterDataTail = candidateMatchedKnownLeadingCharacterDataTail;
                        bestMatchedOpaquePreMapTransferByteCount = candidateMatchedOpaquePreMapTransferByteCount;
                        bestMatchedKnownCharacterDataTail = candidateMatchedKnownCharacterDataTail;
                    }
                }
            }

            if (bestMatchedOffset < 0 || bestRegularFields == null || bestContinentFields == null)
            {
                return false;
            }

            regularFields = bestRegularFields;
            continentFields = bestContinentFields;
            matchedOffset = bestMatchedOffset;
            matchedKnownLeadingCharacterDataTail = bestMatchedKnownLeadingCharacterDataTail;
            matchedOpaquePreMapTransferByteCount = bestMatchedOpaquePreMapTransferByteCount;
            matchedKnownCharacterDataTail = bestMatchedKnownCharacterDataTail;
            return true;
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
            out bool matchedKnownCharacterDataTail,
            out int tailCandidateScore)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            matchedKnownLeadingCharacterDataTail = false;
            matchedOpaquePreMapTransferByteCount = -1;
            matchedKnownCharacterDataTail = false;
            tailCandidateScore = int.MinValue;

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
                    out bool matchedKnownTail,
                    out tailCandidateScore))
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
            int bestTailCandidateScore = int.MinValue;
            int bestKnownLeadingScore = int.MinValue;
            int bestMatchedOffset = -1;
            int[] bestRegularFields = null;
            int[] bestContinentFields = null;
            ulong bestMatchedKnownLeadingSectionFlags = 0;
            int bestMatchedOpaquePreMapTransferByteCount = -1;
            bool bestMatchedKnownCharacterDataTail = false;
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
                        out matchedKnownCharacterDataTail,
                        out int tailCandidateScore))
                {
                    int knownLeadingScore = GetKnownLeadingCandidateScore(candidate.Value);
                    bool shouldReplace = tailCandidateScore > bestTailCandidateScore ||
                                         (tailCandidateScore == bestTailCandidateScore &&
                                          knownLeadingScore > bestKnownLeadingScore) ||
                                         (tailCandidateScore == bestTailCandidateScore &&
                                          knownLeadingScore == bestKnownLeadingScore &&
                                          matchedOffset > bestMatchedOffset);
                    if (!shouldReplace)
                    {
                        continue;
                    }

                    bestTailCandidateScore = tailCandidateScore;
                    bestKnownLeadingScore = knownLeadingScore;
                    bestMatchedOffset = matchedOffset;
                    bestRegularFields = regularFields;
                    bestContinentFields = continentFields;
                    bestMatchedKnownLeadingSectionFlags = candidate.Value.MatchedSectionFlags;
                    bestMatchedOpaquePreMapTransferByteCount = candidate.Value.OpaquePreMapTransferByteCount;
                    bestMatchedKnownCharacterDataTail = matchedKnownCharacterDataTail;
                }
            }

            if (bestMatchedOffset < 0 || bestRegularFields == null || bestContinentFields == null)
            {
                return false;
            }

            regularFields = bestRegularFields;
            continentFields = bestContinentFields;
            matchedOffset = bestMatchedOffset;
            matchedKnownLeadingCharacterDataTail = true;
            matchedKnownLeadingSectionFlags = bestMatchedKnownLeadingSectionFlags;
            matchedOpaquePreMapTransferByteCount = bestMatchedOpaquePreMapTransferByteCount;
            matchedKnownCharacterDataTail = bestMatchedKnownCharacterDataTail;
            return true;
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

        private static int GetKnownLeadingCandidateScore(KnownLeadingOffsetCandidate candidate)
        {
            int sectionScore = PopCount(candidate.MatchedSectionFlags) * 1024;
            int opaqueScore = candidate.OpaquePreMapTransferByteCount < 0
                ? 0
                : -candidate.OpaquePreMapTransferByteCount;
            return sectionScore + opaqueScore;
        }

        private static int PopCount(ulong value)
        {
            int count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }

            return count;
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

            short encodedLength = BitConverter.ToInt16(payload.Slice(offset, sizeof(short)));
            int cursor = offset + sizeof(short);
            int byteLength;
            if (encodedLength < 0)
            {
                int charLength = -encodedLength;
                byteLength = checked(charLength * sizeof(char));
            }
            else
            {
                byteLength = encodedLength;
            }

            if (payload.Length - cursor < byteLength)
            {
                return false;
            }

            nextOffset = cursor + byteLength;
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
            out bool matchedKnownTail,
            out int tailCandidateScore)
        {
            matchedKnownTail = false;
            tailCandidateScore = int.MinValue;
            if (payload.Length == 0)
            {
                matchedKnownTail = true;
                tailCandidateScore = GetKnownCharacterDataTailCandidateScore(0);
                return true;
            }

            if (TrySkipKnownPostMapTransferCharacterDataTail(
                    payload,
                    characterDataFlags,
                    characterJobId,
                    out int knownTailOffset))
            {
                ReadOnlySpan<byte> trailingTail = payload[knownTailOffset..];
                if (trailingTail.Length == 0)
                {
                    matchedKnownTail = true;
                    tailCandidateScore = GetKnownCharacterDataTailCandidateScore(0);
                    return true;
                }

                if (TryValidateExactKnownTrailingTail(trailingTail, out bool matchedExactTail, out int exactTailCandidateScore))
                {
                    matchedKnownTail = matchedExactTail;
                    tailCandidateScore = exactTailCandidateScore;
                    return true;
                }

                if (TryValidateKnownTrailingLogoutGiftTail(trailingTail, out bool matchedKnownLogoutGiftTail))
                {
                    matchedKnownTail = matchedKnownLogoutGiftTail;
                    tailCandidateScore = matchedKnownLogoutGiftTail
                        ? GetKnownCharacterDataTailCandidateScore(trailingTail.Length)
                        : GetRecognizedLogoutGiftTailCandidateScore(trailingTail.Length);
                    return true;
                }

                if (TryValidateKnownTrailingServerFileTimeTail(trailingTail, out bool matchedKnownServerFileTimeTail))
                {
                    matchedKnownTail = matchedKnownServerFileTimeTail;
                    tailCandidateScore = matchedKnownServerFileTimeTail
                        ? GetKnownCharacterDataTailCandidateScore(trailingTail.Length)
                        : GetBoundedOpaqueTailCandidateScore(trailingTail.Length);
                    return true;
                }

                if (trailingTail.Length <= MaximumOpaquePostMapTransferTailByteLength)
                {
                    tailCandidateScore = GetBoundedOpaqueTailCandidateScore(trailingTail.Length);
                    return true;
                }
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

                long trailingByteCount = stream.Length - stream.Position;
                if (trailingByteCount == 0)
                {
                    matchedKnownTail = true;
                    tailCandidateScore = GetKnownCharacterDataTailCandidateScore(0);
                    return true;
                }

                ReadOnlySpan<byte> trailingTail = payload[(int)stream.Position..];
                if (TryValidateExactKnownTrailingTail(trailingTail, out bool matchedExactTail, out int exactTailCandidateScore))
                {
                    matchedKnownTail = matchedExactTail;
                    tailCandidateScore = exactTailCandidateScore;
                    return true;
                }

                if (TryValidateKnownTrailingLogoutGiftTail(trailingTail, out bool matchedKnownLogoutGiftTail))
                {
                    matchedKnownTail = matchedKnownLogoutGiftTail;
                    tailCandidateScore = matchedKnownLogoutGiftTail
                        ? GetKnownCharacterDataTailCandidateScore(trailingByteCount)
                        : GetRecognizedLogoutGiftTailCandidateScore(trailingByteCount);
                    return true;
                }

                if (TryValidateKnownTrailingServerFileTimeTail(trailingTail, out bool matchedKnownServerFileTimeTail))
                {
                    matchedKnownTail = matchedKnownServerFileTimeTail;
                    tailCandidateScore = matchedKnownServerFileTimeTail
                        ? GetKnownCharacterDataTailCandidateScore(trailingByteCount)
                        : GetBoundedOpaqueTailCandidateScore(trailingByteCount);
                    return true;
                }

                // Keep map-transfer bootstrap recovery resilient when additional
                // CharacterData tail bytes follow known sections.
                if (trailingByteCount > 0 && trailingByteCount <= MaximumOpaquePostMapTransferTailByteLength)
                {
                    tailCandidateScore = GetBoundedOpaqueTailCandidateScore(trailingByteCount);
                    return true;
                }

                return false;
            }
            catch (Exception) when (payload.Length > 0)
            {
                if (payload.Length <= MaximumOpaquePostMapTransferTailByteLength)
                {
                    tailCandidateScore = GetBoundedOpaqueTailCandidateScore(payload.Length);
                    return true;
                }

                return false;
            }
        }

        private static bool TrySkipKnownPostMapTransferCharacterDataTail(
            ReadOnlySpan<byte> payload,
            ulong characterDataFlags,
            short characterJobId,
            out int offset)
        {
            offset = 0;

            if ((characterDataFlags & CharacterDataSkillCooldownFlag) != 0 &&
                !TrySkipInt16ValueRecordGroup(payload, offset, out offset))
            {
                return false;
            }

            if ((characterDataFlags & CharacterDataInt16ValueRecordFlag) != 0 &&
                !TrySkipInt16ValueRecordGroup(payload, offset, out offset))
            {
                return false;
            }

            if ((characterDataFlags & CharacterDataQuestRecordFlag) != 0 &&
                !TrySkipQuestRecordGroup(payload, offset, out offset))
            {
                return false;
            }

            if ((characterDataFlags & CharacterDataShortFileTimeRecordFlag) != 0 &&
                !TrySkipShortFileTimeRecordGroup(payload, offset, out offset))
            {
                return false;
            }

            if ((characterDataFlags & 0x40000UL) != 0 &&
                !TrySkipNewYearCardRecordGroup(payload, offset, out offset))
            {
                return false;
            }

            if ((characterDataFlags & 0x80000UL) != 0 &&
                !TrySkipQuestExRecordGroup(payload, offset, out offset))
            {
                return false;
            }

            if ((characterDataFlags & CharacterDataTwoIntValueRecordFlag) != 0 &&
                !TrySkipTwoIntValueRecord(payload, offset, out offset))
            {
                return false;
            }

            if ((characterDataFlags & 0x200000UL) != 0 &&
                characterJobId / 100 == 33)
            {
                if ((uint)offset > payload.Length || payload.Length - offset < sizeof(byte) + (5 * sizeof(int)))
                {
                    return false;
                }

                offset += sizeof(byte) + (5 * sizeof(int));
            }

            if ((characterDataFlags & 0x400000UL) != 0 &&
                !TrySkipFixedRecordGroup(payload, offset, sizeof(ushort) + sizeof(long), out offset))
            {
                return false;
            }

            if ((characterDataFlags & 0x800000UL) != 0 &&
                !TrySkipFixedRecordGroup(payload, offset, sizeof(ushort) + sizeof(ushort), out offset))
            {
                return false;
            }

            return offset > 0;
        }

        private static bool TrySkipNewYearCardRecordGroup(ReadOnlySpan<byte> payload, int offset, out int nextOffset)
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
                if ((uint)nextOffset > payload.Length || payload.Length - nextOffset < 2 * sizeof(int))
                {
                    return false;
                }

                nextOffset += 2 * sizeof(int);
                if (!TrySkipMapleString(payload, nextOffset, out nextOffset))
                {
                    return false;
                }

                if ((uint)nextOffset > payload.Length || payload.Length - nextOffset < sizeof(byte) + sizeof(long) + sizeof(int))
                {
                    return false;
                }

                nextOffset += sizeof(byte) + sizeof(long) + sizeof(int);
                if (!TrySkipMapleString(payload, nextOffset, out nextOffset))
                {
                    return false;
                }

                if ((uint)nextOffset > payload.Length || payload.Length - nextOffset < (2 * sizeof(byte)) + sizeof(long))
                {
                    return false;
                }

                nextOffset += (2 * sizeof(byte)) + sizeof(long);
                if (!TrySkipMapleString(payload, nextOffset, out nextOffset))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TrySkipQuestExRecordGroup(ReadOnlySpan<byte> payload, int offset, out int nextOffset)
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
                if ((uint)nextOffset > payload.Length || payload.Length - nextOffset < sizeof(ushort))
                {
                    return false;
                }

                nextOffset += sizeof(ushort);
                if (!TrySkipMapleString(payload, nextOffset, out nextOffset))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryValidateExactKnownTrailingTail(
            ReadOnlySpan<byte> trailingTail,
            out bool matchedKnownTail,
            out int tailCandidateScore)
        {
            matchedKnownTail = false;
            tailCandidateScore = int.MinValue;
            if (trailingTail.Length == SetFieldServerFileTimeByteLength &&
                TryMatchTrailingServerFileTimeSuffix(trailingTail.ToArray(), out int opaqueBeforeServerFileTimeLength) &&
                opaqueBeforeServerFileTimeLength == 0)
            {
                matchedKnownTail = true;
                tailCandidateScore = GetKnownCharacterDataTailCandidateScore(trailingTail.Length);
                return true;
            }

            if (trailingTail.Length == LogoutGiftConfigByteLength ||
                trailingTail.Length == LogoutGiftConfigByteLength + SetFieldServerFileTimeByteLength)
            {
                if (TryValidateKnownTrailingLogoutGiftTail(trailingTail, out bool matchedKnownLogoutGiftTail))
                {
                    matchedKnownTail = matchedKnownLogoutGiftTail;
                    tailCandidateScore = matchedKnownLogoutGiftTail
                        ? GetKnownCharacterDataTailCandidateScore(trailingTail.Length)
                        : GetRecognizedLogoutGiftTailCandidateScore(trailingTail.Length);
                    return true;
                }

                if (trailingTail.Length == LogoutGiftConfigByteLength + SetFieldServerFileTimeByteLength &&
                    TryValidateKnownTrailingServerFileTimeTail(trailingTail, out bool matchedKnownServerFileTimeTail))
                {
                    matchedKnownTail = matchedKnownServerFileTimeTail;
                    tailCandidateScore = matchedKnownServerFileTimeTail
                        ? GetKnownCharacterDataTailCandidateScore(trailingTail.Length)
                        : GetBoundedOpaqueTailCandidateScore(trailingTail.Length);
                    return true;
                }
            }

            return false;
        }

        private static int GetKnownCharacterDataTailCandidateScore(long trailingByteCount)
        {
            if (trailingByteCount == LogoutGiftConfigByteLength + SetFieldServerFileTimeByteLength)
            {
                return int.MaxValue;
            }

            if (trailingByteCount == SetFieldServerFileTimeByteLength)
            {
                return int.MaxValue - 1;
            }

            if (trailingByteCount == LogoutGiftConfigByteLength)
            {
                return int.MaxValue - 2;
            }

            if (trailingByteCount == 0)
            {
                return int.MaxValue - 3;
            }

            return int.MaxValue - 4;
        }

        private static int GetRecognizedLogoutGiftTailCandidateScore(long trailingByteCount)
        {
            if (trailingByteCount <= 0)
            {
                return int.MaxValue - 5;
            }

            int boundedTrailing = trailingByteCount > int.MaxValue
                ? int.MaxValue
                : (int)trailingByteCount;
            return int.MaxValue - 5 - boundedTrailing;
        }

        private static int GetBoundedOpaqueTailCandidateScore(long trailingByteCount)
        {
            if (trailingByteCount <= 0)
            {
                return int.MinValue + 1;
            }

            return trailingByteCount >= int.MaxValue
                ? int.MinValue + 1
                : -((int)trailingByteCount);
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            short encodedLength = reader.ReadInt16();
            if (encodedLength == 0)
            {
                return string.Empty;
            }

            if (encodedLength > 0)
            {
                int byteLength = encodedLength;
                byte[] bytes = reader.ReadBytes(byteLength);
                if (bytes.Length != byteLength)
                {
                    throw new EndOfStreamException("Maple string exceeded the remaining post-map-transfer tail payload.");
                }

                return Encoding.Default.GetString(bytes);
            }

            int unicodeByteLength = checked((-encodedLength) * sizeof(char));
            byte[] unicodeBytes = reader.ReadBytes(unicodeByteLength);
            if (unicodeBytes.Length != unicodeByteLength)
            {
                throw new EndOfStreamException("Unicode maple string exceeded the remaining post-map-transfer tail payload.");
            }

            return Encoding.Unicode.GetString(unicodeBytes);
        }

        private static bool TryValidateKnownTrailingLogoutGiftTail(
            ReadOnlySpan<byte> trailingTail,
            out bool matchedKnownTail)
        {
            matchedKnownTail = false;
            if (trailingTail.Length <= 0 || trailingTail.Length < LogoutGiftConfigByteLength)
            {
                return false;
            }

            if (!PacketStageTransitionRuntime.TryDecodeTrailingLogoutGiftConfigPayload(
                    trailingTail.ToArray(),
                    out int predictQuitRawValue,
                    out int[] commoditySerialNumbers,
                    out byte[] leadingOpaqueBytes,
                    out _,
                    out byte[] trailingOpaqueBytes,
                    out _,
                    out _,
                    out _))
            {
                return false;
            }

            if (!IsLikelyLogoutGiftConfig(predictQuitRawValue, commoditySerialNumbers))
            {
                return false;
            }

            int leadingOpaqueLength = leadingOpaqueBytes?.Length ?? 0;
            int trailingOpaqueLength = trailingOpaqueBytes?.Length ?? 0;
            bool hasTrailingServerFileTimeSuffix = TryMatchTrailingServerFileTimeSuffix(
                trailingOpaqueBytes,
                out int opaqueBeforeServerFileTimeLength);
            bool hasBoundedOpaqueSegments = leadingOpaqueLength <= MaximumOpaquePostMapTransferTailByteLength &&
                                            (trailingOpaqueLength <= MaximumOpaquePostMapTransferTailByteLength
                                             || (hasTrailingServerFileTimeSuffix &&
                                                 opaqueBeforeServerFileTimeLength <= MaximumOpaqueBetweenLogoutGiftAndServerFileTimeByteLength));
            if (!hasBoundedOpaqueSegments)
            {
                return false;
            }

            matchedKnownTail = trailingOpaqueLength == 0 ||
                               trailingOpaqueLength == SetFieldServerFileTimeByteLength ||
                               hasTrailingServerFileTimeSuffix;
            return true;
        }

        private static bool TryValidateKnownTrailingServerFileTimeTail(
            ReadOnlySpan<byte> trailingTail,
            out bool matchedKnownTail)
        {
            matchedKnownTail = false;
            if (trailingTail.Length < SetFieldServerFileTimeByteLength)
            {
                return false;
            }

            if (!TryMatchTrailingServerFileTimeSuffix(
                    trailingTail.ToArray(),
                    out int opaqueBeforeServerFileTimeLength))
            {
                return false;
            }

            if (trailingTail.Length >= LogoutGiftConfigByteLength + SetFieldServerFileTimeByteLength &&
                PacketStageTransitionRuntime.TryDecodeTrailingLogoutGiftConfigPayload(
                    trailingTail.ToArray(),
                    out int predictQuitRawValue,
                    out int[] commoditySerialNumbers,
                    out byte[] leadingOpaqueBytes,
                    out _,
                    out byte[] trailingOpaqueBytes,
                    out _,
                    out _,
                    out _) &&
                IsLikelyLogoutGiftConfig(predictQuitRawValue, commoditySerialNumbers))
            {
                int leadingOpaqueLength = leadingOpaqueBytes?.Length ?? 0;
                if (!TryMatchTrailingServerFileTimeSuffix(trailingOpaqueBytes, out int opaqueBetweenLogoutGiftAndServerFileTimeLength) ||
                    leadingOpaqueLength > MaximumOpaquePostMapTransferTailByteLength ||
                    opaqueBetweenLogoutGiftAndServerFileTimeLength > MaximumOpaqueBetweenLogoutGiftAndServerFileTimeByteLength)
                {
                    return false;
                }
            }

            if (opaqueBeforeServerFileTimeLength > MaximumOpaqueBeforeServerFileTimeByteLength)
            {
                return false;
            }

            matchedKnownTail = true;
            return true;
        }

        private static bool IsLikelyLogoutGiftConfig(int predictQuitRawValue, IReadOnlyList<int> commoditySerialNumbers)
        {
            if (predictQuitRawValue is not 0 and not 1 ||
                commoditySerialNumbers == null ||
                commoditySerialNumbers.Count != PacketStageTransitionRuntime.LogoutGiftEntryCount)
            {
                return false;
            }

            for (int i = 0; i < commoditySerialNumbers.Count; i++)
            {
                if (commoditySerialNumbers[i] < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryMatchTrailingServerFileTimeSuffix(
            byte[] trailingOpaqueBytes,
            out int opaqueBeforeServerFileTimeLength)
        {
            opaqueBeforeServerFileTimeLength = 0;
            if (trailingOpaqueBytes == null || trailingOpaqueBytes.Length < SetFieldServerFileTimeByteLength)
            {
                return false;
            }

            opaqueBeforeServerFileTimeLength = trailingOpaqueBytes.Length - SetFieldServerFileTimeByteLength;
            long rawServerFileTime = BitConverter.ToInt64(trailingOpaqueBytes, opaqueBeforeServerFileTimeLength);
            if (rawServerFileTime <= 0 || rawServerFileTime == long.MaxValue)
            {
                return false;
            }

            try
            {
                _ = DateTime.FromFileTimeUtc(rawServerFileTime);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }
    }
}
