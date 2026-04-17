using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Managers
{
    internal static class MapTransferOfficialSessionResultResolver
    {
        internal static MapTransferRuntimeResponse Resolve(
            MapTransferRuntimeResponse predictedResponse,
            MapTransferRuntimeResponse authoritativeResponse,
            MapTransferRuntimeRequest request = null)
        {
            if (authoritativeResponse == null)
            {
                return predictedResponse ?? new MapTransferRuntimeResponse();
            }

            int resolvedFocusMapId = authoritativeResponse.FocusMapId > 0
                ? authoritativeResponse.FocusMapId
                : ResolveAuthoritativeFocusMapId(authoritativeResponse, request, predictedResponse);
            int resolvedFocusSlotIndex = authoritativeResponse.FocusSlotIndex >= 0
                ? authoritativeResponse.FocusSlotIndex
                : ResolveAuthoritativeFocusSlotIndex(authoritativeResponse, request, predictedResponse);

            return new MapTransferRuntimeResponse
            {
                Applied = authoritativeResponse.Applied,
                FailureMessage = !string.IsNullOrWhiteSpace(authoritativeResponse.FailureMessage)
                    ? authoritativeResponse.FailureMessage
                    : predictedResponse?.FailureMessage,
                FocusMapId = resolvedFocusMapId,
                FocusSlotIndex = resolvedFocusSlotIndex,
                ResultType = authoritativeResponse.ResultType,
                PacketResultCode = authoritativeResponse.PacketResultCode,
                CanTransferContinent = authoritativeResponse.CanTransferContinent,
                FieldList = authoritativeResponse.FieldList ?? Array.Empty<int>()
            };
        }

        internal static int ResolvePendingRequestIndex(
            IReadOnlyList<MapTransferRuntimeRequest> pendingRequests,
            MapTransferRuntimeResponse authoritativeResponse)
        {
            if (pendingRequests == null || pendingRequests.Count == 0 || authoritativeResponse == null)
            {
                return -1;
            }

            MapTransferDestinationBook responseBook = authoritativeResponse.CanTransferContinent
                ? MapTransferDestinationBook.Continent
                : MapTransferDestinationBook.Regular;
            MapTransferRuntimeRequestType? expectedType = authoritativeResponse.ResultType switch
            {
                MapTransferRuntimeResultType.RegisterApplied => MapTransferRuntimeRequestType.Register,
                MapTransferRuntimeResultType.DeleteApplied => MapTransferRuntimeRequestType.Delete,
                _ => null
            };
            expectedType ??= InferRequestTypeFromFailureResultCode(authoritativeResponse.PacketResultCode);
            if (!expectedType.HasValue)
            {
                return -1;
            }

            int typeFallbackIndex = -1;
            for (int index = 0; index < pendingRequests.Count; index++)
            {
                MapTransferRuntimeRequest request = pendingRequests[index];
                if (request == null || request.Book != responseBook)
                {
                    continue;
                }

                if (request.Type != expectedType)
                {
                    continue;
                }

                if (typeFallbackIndex < 0)
                {
                    typeFallbackIndex = index;
                }

                if (MatchesAuthoritativeFieldList(request, authoritativeResponse))
                {
                    return index;
                }
            }

            return typeFallbackIndex;
        }

        internal static MapTransferRuntimeRequest InferRequestFromAuthoritativeDelta(
            MapTransferRuntimeResponse authoritativeResponse,
            IReadOnlyList<int> previousFieldList)
        {
            if (authoritativeResponse?.Applied != true ||
                authoritativeResponse.ResultType == MapTransferRuntimeResultType.None)
            {
                return null;
            }

            IReadOnlyList<int> currentFieldList = authoritativeResponse.FieldList ?? Array.Empty<int>();
            if (currentFieldList.Count == 0)
            {
                return null;
            }

            int[] previousSlots = BuildNormalizedSlotArray(previousFieldList, currentFieldList.Count);
            int[] currentSlots = BuildNormalizedSlotArray(currentFieldList, currentFieldList.Count);
            List<int> changedSlots = new();
            for (int slotIndex = 0; slotIndex < currentSlots.Length; slotIndex++)
            {
                if (previousSlots[slotIndex] != currentSlots[slotIndex])
                {
                    changedSlots.Add(slotIndex);
                }
            }

            if (changedSlots.Count == 0)
            {
                return null;
            }

            MapTransferRuntimeRequestType requestType = authoritativeResponse.ResultType switch
            {
                MapTransferRuntimeResultType.RegisterApplied => MapTransferRuntimeRequestType.Register,
                MapTransferRuntimeResultType.DeleteApplied => MapTransferRuntimeRequestType.Delete,
                _ => (MapTransferRuntimeRequestType)(-1)
            };
            if (requestType != MapTransferRuntimeRequestType.Register &&
                requestType != MapTransferRuntimeRequestType.Delete)
            {
                return null;
            }

            int slot = requestType == MapTransferRuntimeRequestType.Register
                ? ResolveRegisterSlot(changedSlots, previousSlots, currentSlots)
                : ResolveDeleteSlot(changedSlots, previousSlots, currentSlots);
            if (slot < 0 || slot >= currentSlots.Length)
            {
                return null;
            }

            int mapId = requestType == MapTransferRuntimeRequestType.Register
                ? currentSlots[slot]
                : previousSlots[slot];
            if (mapId <= 0 || mapId == MapTransferRuntimeManager.EmptyDestinationMapId)
            {
                return null;
            }

            return new MapTransferRuntimeRequest
            {
                Type = requestType,
                Book = authoritativeResponse.CanTransferContinent
                    ? MapTransferDestinationBook.Continent
                    : MapTransferDestinationBook.Regular,
                MapId = mapId,
                SlotIndex = slot
            };
        }

        private static MapTransferRuntimeRequestType? InferRequestTypeFromFailureResultCode(
            MapTransferRuntimePacketResultCode packetResultCode)
        {
            return packetResultCode switch
            {
                MapTransferRuntimePacketResultCode.NoEmptySlot => MapTransferRuntimeRequestType.Register,
                MapTransferRuntimePacketResultCode.AlreadyRegistered => MapTransferRuntimeRequestType.Register,
                MapTransferRuntimePacketResultCode.OfficialFailure8 => MapTransferRuntimeRequestType.Register,
                MapTransferRuntimePacketResultCode.CannotSaveDestination => MapTransferRuntimeRequestType.Register,
                MapTransferRuntimePacketResultCode.OfficialFailure11 => MapTransferRuntimeRequestType.Register,
                _ => null
            };
        }

        private static bool MatchesAuthoritativeFieldList(
            MapTransferRuntimeRequest request,
            MapTransferRuntimeResponse authoritativeResponse)
        {
            if (request == null || authoritativeResponse?.Applied != true || request.MapId <= 0)
            {
                return true;
            }

            bool fieldListContainsRequestMap = FindMapIdSlot(authoritativeResponse.FieldList, request.MapId) >= 0;
            return request.Type switch
            {
                MapTransferRuntimeRequestType.Register => MatchesAuthoritativeRegisterFieldList(request, authoritativeResponse.FieldList, fieldListContainsRequestMap),
                MapTransferRuntimeRequestType.Delete => !fieldListContainsRequestMap,
                _ => true
            };
        }

        private static bool MatchesAuthoritativeRegisterFieldList(
            MapTransferRuntimeRequest request,
            IReadOnlyList<int> fieldList,
            bool fieldListContainsRequestMap)
        {
            if (request?.SlotIndex >= 0 &&
                fieldList != null &&
                request.SlotIndex < fieldList.Count)
            {
                return fieldList[request.SlotIndex] == request.MapId;
            }

            return fieldListContainsRequestMap;
        }

        private static int ResolveAuthoritativeFocusMapId(
            MapTransferRuntimeResponse authoritativeResponse,
            MapTransferRuntimeRequest request,
            MapTransferRuntimeResponse predictedResponse)
        {
            if (authoritativeResponse?.Applied == true && request?.MapId > 0)
            {
                if (request.Type == MapTransferRuntimeRequestType.Register)
                {
                    return request.MapId;
                }

                if (request.Type == MapTransferRuntimeRequestType.Delete)
                {
                    return request.MapId;
                }
            }

            return predictedResponse?.FocusMapId ?? 0;
        }

        private static int ResolveAuthoritativeFocusSlotIndex(
            MapTransferRuntimeResponse authoritativeResponse,
            MapTransferRuntimeRequest request,
            MapTransferRuntimeResponse predictedResponse)
        {
            if (authoritativeResponse?.Applied == true && request != null)
            {
                if (request.Type == MapTransferRuntimeRequestType.Register && request.MapId > 0)
                {
                    int authoritativeSlotIndex = FindMapIdSlot(authoritativeResponse.FieldList, request.MapId);
                    if (authoritativeSlotIndex >= 0)
                    {
                        return authoritativeSlotIndex;
                    }
                }

                if (request.Type == MapTransferRuntimeRequestType.Delete && request.SlotIndex >= 0)
                {
                    return request.SlotIndex;
                }
            }

            return predictedResponse?.FocusSlotIndex ?? -1;
        }

        private static int FindMapIdSlot(IReadOnlyList<int> fieldList, int mapId)
        {
            if (fieldList == null || mapId <= 0)
            {
                return -1;
            }

            for (int index = 0; index < fieldList.Count; index++)
            {
                if (fieldList[index] == mapId)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int[] BuildNormalizedSlotArray(IReadOnlyList<int> source, int count)
        {
            int[] slots = new int[Math.Max(0, count)];
            Array.Fill(slots, MapTransferRuntimeManager.EmptyDestinationMapId);
            if (source == null || source.Count == 0)
            {
                return slots;
            }

            int maxCount = Math.Min(slots.Length, source.Count);
            for (int index = 0; index < maxCount; index++)
            {
                int mapId = source[index];
                if (mapId > 0)
                {
                    slots[index] = mapId;
                }
            }

            return slots;
        }

        private static int ResolveRegisterSlot(
            IReadOnlyList<int> changedSlots,
            IReadOnlyList<int> previousSlots,
            IReadOnlyList<int> currentSlots)
        {
            for (int i = 0; i < changedSlots.Count; i++)
            {
                int slot = changedSlots[i];
                if (currentSlots[slot] > 0 &&
                    currentSlots[slot] != MapTransferRuntimeManager.EmptyDestinationMapId &&
                    currentSlots[slot] != previousSlots[slot])
                {
                    return slot;
                }
            }

            return changedSlots[0];
        }

        private static int ResolveDeleteSlot(
            IReadOnlyList<int> changedSlots,
            IReadOnlyList<int> previousSlots,
            IReadOnlyList<int> currentSlots)
        {
            for (int i = 0; i < changedSlots.Count; i++)
            {
                int slot = changedSlots[i];
                if (previousSlots[slot] > 0 &&
                    previousSlots[slot] != MapTransferRuntimeManager.EmptyDestinationMapId &&
                    (currentSlots[slot] <= 0 || currentSlots[slot] == MapTransferRuntimeManager.EmptyDestinationMapId))
                {
                    return slot;
                }
            }

            return changedSlots[0];
        }
    }
}
