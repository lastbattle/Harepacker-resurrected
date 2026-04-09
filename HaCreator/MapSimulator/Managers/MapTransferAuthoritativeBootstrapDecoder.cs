using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Managers
{
    internal static class MapTransferAuthoritativeBootstrapDecoder
    {
        internal const ulong CharacterDataMapTransferFlag = 0x1000UL;
        private const int LogoutGiftConfigByteLength = 3 * sizeof(int);
        internal const int BootstrapBookByteLength =
            (MapTransferRuntimeManager.RegularCapacity + MapTransferRuntimeManager.ContinentCapacity) * sizeof(int);

        internal static bool TryFindBootstrapBooks(
            ReadOnlySpan<byte> payload,
            Func<int, bool> isPlausibleMapId,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset,
            out bool ignoredTrailingLogoutGiftConfig,
            out bool matchedExactTailBoundary)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;
            ignoredTrailingLogoutGiftConfig = false;
            matchedExactTailBoundary = false;

            if (payload.Length < BootstrapBookByteLength || isPlausibleMapId == null)
            {
                return false;
            }

            if (payload.Length >= BootstrapBookByteLength + LogoutGiftConfigByteLength)
            {
                ReadOnlySpan<byte> leadingPayload = payload[..^LogoutGiftConfigByteLength];
                if (TryFindBootstrapBooksAtExactTail(
                        leadingPayload,
                        isPlausibleMapId,
                        out regularFields,
                        out continentFields,
                        out matchedOffset))
                {
                    ignoredTrailingLogoutGiftConfig = true;
                    matchedExactTailBoundary = true;
                    return true;
                }

                if (TryFindBootstrapBooksCore(
                        leadingPayload,
                        isPlausibleMapId,
                        out regularFields,
                        out continentFields,
                        out matchedOffset))
                {
                    ignoredTrailingLogoutGiftConfig = true;
                    return true;
                }
            }

            if (TryFindBootstrapBooksAtExactTail(
                    payload,
                    isPlausibleMapId,
                    out regularFields,
                    out continentFields,
                    out matchedOffset))
            {
                matchedExactTailBoundary = true;
                return true;
            }

            return TryFindBootstrapBooksCore(
                payload,
                isPlausibleMapId,
                out regularFields,
                out continentFields,
                out matchedOffset);
        }

        private static bool TryFindBootstrapBooksAtExactTail(
            ReadOnlySpan<byte> payload,
            Func<int, bool> isPlausibleMapId,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;

            if (payload.Length < BootstrapBookByteLength)
            {
                return false;
            }

            int tailOffset = payload.Length - BootstrapBookByteLength;
            return TryReadBootstrapBooksAtOffset(
                payload,
                tailOffset,
                isPlausibleMapId,
                out regularFields,
                out continentFields,
                out matchedOffset);
        }

        private static bool TryFindBootstrapBooksCore(
            ReadOnlySpan<byte> payload,
            Func<int, bool> isPlausibleMapId,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;

            int slotCount = MapTransferRuntimeManager.RegularCapacity + MapTransferRuntimeManager.ContinentCapacity;
            if (payload.Length < BootstrapBookByteLength || isPlausibleMapId == null)
            {
                return false;
            }

            for (int offset = payload.Length - BootstrapBookByteLength; offset >= 0; offset--)
            {
                if (TryReadBootstrapBooksAtOffset(
                        payload,
                        offset,
                        isPlausibleMapId,
                        out regularFields,
                        out continentFields,
                        out matchedOffset))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadBootstrapBooksAtOffset(
            ReadOnlySpan<byte> payload,
            int offset,
            Func<int, bool> isPlausibleMapId,
            out int[] regularFields,
            out int[] continentFields,
            out int matchedOffset)
        {
            regularFields = null;
            continentFields = null;
            matchedOffset = -1;

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

            regularFields = candidateRegular;
            continentFields = candidateContinent;
            matchedOffset = offset;
            return true;
        }

        private static bool IsAcceptedBootstrapValue(int mapId, Func<int, bool> isPlausibleMapId)
        {
            return mapId == 0 ||
                   mapId == MapTransferRuntimeManager.EmptyDestinationMapId ||
                   isPlausibleMapId(mapId);
        }
    }
}
