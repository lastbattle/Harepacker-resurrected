using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Managers
{
    internal static class MapTransferAuthoritativeBootstrapDecoder
    {
        internal const ulong CharacterDataMapTransferFlag = 0x1000UL;

        internal static bool TryFindBootstrapBooks(
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
            int sequenceLength = slotCount * sizeof(int);
            if (payload.Length < sequenceLength || isPlausibleMapId == null)
            {
                return false;
            }

            for (int offset = 0; offset <= payload.Length - sequenceLength; offset++)
            {
                int[] candidateRegular = new int[MapTransferRuntimeManager.RegularCapacity];
                int[] candidateContinent = new int[MapTransferRuntimeManager.ContinentCapacity];
                HashSet<int> seenMaps = new();
                bool valid = true;

                for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                {
                    int mapId = BitConverter.ToInt32(payload.Slice(offset + (slotIndex * sizeof(int)), sizeof(int)));
                    if (!IsAcceptedBootstrapValue(mapId, isPlausibleMapId))
                    {
                        valid = false;
                        break;
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

                if (!valid || seenMaps.Count == 0)
                {
                    continue;
                }

                regularFields = candidateRegular;
                continentFields = candidateContinent;
                matchedOffset = offset;
                return true;
            }

            return false;
        }

        private static bool IsAcceptedBootstrapValue(int mapId, Func<int, bool> isPlausibleMapId)
        {
            return mapId == 0 ||
                   mapId == MapTransferRuntimeManager.EmptyDestinationMapId ||
                   isPlausibleMapId(mapId);
        }
    }
}
