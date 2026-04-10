namespace HaCreator.MapSimulator.Interaction
{
    internal static class MapTransferClientParityText
    {
        internal const int RegisterListFullStringPoolId = 0x0BB4;
        internal const int CurrentMapAlreadyRegisteredStringPoolId = 0x0BB5;
        internal const int NoEmptySlotStringPoolId = 0x0BD3;
        internal const int TargetUserFailureStringPoolId = 0x0BB0;
        internal const int AlreadyRegisteredStringPoolId = 0x0BB6;
        internal const int CannotSaveDestinationStringPoolId = 0x0BB3;
        internal const int RequestRejectedStringPoolId = 0x0BB7;
        internal const int RegisterPromptStringPoolId = 0x0BB8;
        internal const int DeletePromptStringPoolId = 0x0BB9;
        internal const int MovePromptStringPoolId = 0x0BBA;

        private const string RegisterListFullFallback = "Your teleport list is full.\r\nPlease delete an entry before trying again.";
        private const string CurrentMapAlreadyRegisteredFallback = "You have already entered this map.";
        private const string NoEmptySlotFallback = "All saved teleport slots are already filled.";
        private const string TargetUserFailureFallback = "That transfer target is unavailable.";
        private const string AlreadyRegisteredFallback = "That map is already registered in this destination book.";
        private const string CannotSaveDestinationFallback = "This destination cannot be saved in a teleport slot.";
        private const string RequestRejectedFallback = "Map transfer request failed.";
        private const string RegisterPromptFallback = "Will you enter this map\r\nin your teleport list?\r\n[{0}]";
        private const string DeletePromptFallback = "Will you delete this map from the\r\nteleport list?\r\n[{0}]";
        private const string MovePromptFallback = "Will you teleport to this map?\r\n[{0}]";

        public static string ResolveRegisterListFullNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                RegisterListFullStringPoolId,
                RegisterListFullFallback);
        }

        public static string ResolveCurrentMapAlreadyRegisteredNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                CurrentMapAlreadyRegisteredStringPoolId,
                CurrentMapAlreadyRegisteredFallback);
        }

        public static string BuildRegisterConfirmationPrompt(string mapLabel)
        {
            return FormatSingleMapPrompt(
                RegisterPromptStringPoolId,
                RegisterPromptFallback,
                mapLabel);
        }

        public static string BuildDeleteConfirmationPrompt(string mapLabel)
        {
            return FormatSingleMapPrompt(
                DeletePromptStringPoolId,
                DeletePromptFallback,
                mapLabel);
        }

        public static string BuildMoveConfirmationPrompt(string mapLabel)
        {
            return FormatSingleMapPrompt(
                MovePromptStringPoolId,
                MovePromptFallback,
                mapLabel);
        }

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

        private static string FormatSingleMapPrompt(int stringPoolId, string fallbackFormat, string mapLabel)
        {
            string safeLabel = string.IsNullOrWhiteSpace(mapLabel)
                ? "Unknown Map"
                : mapLabel.Trim();
            string compositeFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormat,
                maxPlaceholderCount: 1,
                out _);
            return string.Format(compositeFormat, safeLabel);
        }
    }
}
