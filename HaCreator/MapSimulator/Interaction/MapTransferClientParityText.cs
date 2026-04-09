namespace HaCreator.MapSimulator.Interaction
{
    internal static class MapTransferClientParityText
    {
        internal const int NoEmptySlotStringPoolId = 0x0BD3;
        internal const int TargetUserFailureStringPoolId = 0x0BB0;
        internal const int AlreadyRegisteredStringPoolId = 0x0BB6;
        internal const int CannotSaveDestinationStringPoolId = 0x0BB3;
        internal const int RequestRejectedStringPoolId = 0x0BB7;

        private const string NoEmptySlotFallback = "All saved teleport slots are already filled.";
        private const string TargetUserFailureFallback = "That transfer target is unavailable.";
        private const string AlreadyRegisteredFallback = "That map is already registered in this destination book.";
        private const string CannotSaveDestinationFallback = "This destination cannot be saved in a teleport slot.";
        private const string RequestRejectedFallback = "Map transfer request failed.";

        public static string ResolveFailureMessage(Managers.MapTransferRuntimePacketResultCode packetResultCode, string targetUserName = null)
        {
            return packetResultCode switch
            {
                Managers.MapTransferRuntimePacketResultCode.NoEmptySlot => MapleStoryStringPool.GetOrFallback(
                    NoEmptySlotStringPoolId,
                    NoEmptySlotFallback),
                Managers.MapTransferRuntimePacketResultCode.OfficialFailure6 or Managers.MapTransferRuntimePacketResultCode.OfficialFailure7
                    => FormatTargetUserFailure(targetUserName),
                Managers.MapTransferRuntimePacketResultCode.OfficialFailure8 => MapleStoryStringPool.GetOrFallback(
                    NoEmptySlotStringPoolId,
                    NoEmptySlotFallback),
                Managers.MapTransferRuntimePacketResultCode.AlreadyRegistered => MapleStoryStringPool.GetOrFallback(
                    AlreadyRegisteredStringPoolId,
                    AlreadyRegisteredFallback),
                Managers.MapTransferRuntimePacketResultCode.CannotSaveDestination => MapleStoryStringPool.GetOrFallback(
                    CannotSaveDestinationStringPoolId,
                    CannotSaveDestinationFallback),
                Managers.MapTransferRuntimePacketResultCode.OfficialFailure11 => MapleStoryStringPool.GetOrFallback(
                    RequestRejectedStringPoolId,
                    RequestRejectedFallback),
                _ => null
            };
        }

        private static string FormatTargetUserFailure(string targetUserName)
        {
            string resolvedFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                TargetUserFailureStringPoolId,
                "{0}",
                maxPlaceholderCount: 1,
                out bool usedResolvedText);
            string safeTarget = string.IsNullOrWhiteSpace(targetUserName)
                ? "that character"
                : targetUserName.Trim();

            if (usedResolvedText)
            {
                return string.Format(resolvedFormat, safeTarget);
            }

            return string.IsNullOrWhiteSpace(targetUserName)
                ? TargetUserFailureFallback
                : $"{safeTarget}: {TargetUserFailureFallback}";
        }
    }
}
