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

            int fallbackIndex = -1;
            for (int index = 0; index < pendingRequests.Count; index++)
            {
                MapTransferRuntimeRequest request = pendingRequests[index];
                if (request == null || request.Book != responseBook)
                {
                    continue;
                }

                fallbackIndex = index;
                if (!expectedType.HasValue || request.Type == expectedType.Value)
                {
                    return index;
                }
            }

            return fallbackIndex;
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
    }
}
