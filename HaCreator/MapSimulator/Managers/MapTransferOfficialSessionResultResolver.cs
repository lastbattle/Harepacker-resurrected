using System;

namespace HaCreator.MapSimulator.Managers
{
    internal static class MapTransferOfficialSessionResultResolver
    {
        internal static MapTransferRuntimeResponse Resolve(
            MapTransferRuntimeResponse predictedResponse,
            MapTransferRuntimeResponse authoritativeResponse)
        {
            if (authoritativeResponse == null)
            {
                return predictedResponse ?? new MapTransferRuntimeResponse();
            }

            return new MapTransferRuntimeResponse
            {
                Applied = authoritativeResponse.Applied,
                FailureMessage = !string.IsNullOrWhiteSpace(authoritativeResponse.FailureMessage)
                    ? authoritativeResponse.FailureMessage
                    : predictedResponse?.FailureMessage,
                FocusMapId = authoritativeResponse.FocusMapId > 0
                    ? authoritativeResponse.FocusMapId
                    : predictedResponse?.FocusMapId ?? 0,
                FocusSlotIndex = authoritativeResponse.FocusSlotIndex >= 0
                    ? authoritativeResponse.FocusSlotIndex
                    : predictedResponse?.FocusSlotIndex ?? -1,
                ResultType = authoritativeResponse.ResultType,
                PacketResultCode = authoritativeResponse.PacketResultCode,
                CanTransferContinent = authoritativeResponse.CanTransferContinent,
                FieldList = authoritativeResponse.FieldList ?? Array.Empty<int>()
            };
        }
    }
}
