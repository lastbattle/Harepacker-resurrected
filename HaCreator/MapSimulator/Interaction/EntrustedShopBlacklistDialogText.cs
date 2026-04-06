namespace HaCreator.MapSimulator.Interaction
{
    internal static class EntrustedShopBlacklistDialogText
    {
        internal const int AddPromptStringPoolId = 0x2E5;
        internal const int OwnerNoticeStringPoolId = 742;
        internal const int InvalidNameNoticeStringPoolId = 743;
        internal const int DuplicateNoticeStringPoolId = 744;

        private const string AddPromptFallback = "Enter the character name to add to the blacklist.";
        private const string OwnerNoticeFallback = "You may not add your own name to the blacklist.";
        private const string InvalidNameNoticeFallback = "Please enter a valid character name.";
        private const string DuplicateNoticeFallback = "That character is already in the blacklist.";

        internal static string GetAddPromptText(bool appendFallbackSuffix = true)
        {
            return GetResolvedOrFallback(AddPromptStringPoolId, AddPromptFallback, appendFallbackSuffix);
        }

        internal static string GetOwnerNotice(bool appendFallbackSuffix = true)
        {
            return GetResolvedOrFallback(OwnerNoticeStringPoolId, OwnerNoticeFallback, appendFallbackSuffix);
        }

        internal static string GetInvalidNameNotice(bool appendFallbackSuffix = true)
        {
            return GetResolvedOrFallback(InvalidNameNoticeStringPoolId, InvalidNameNoticeFallback, appendFallbackSuffix);
        }

        internal static string GetDuplicateNotice(bool appendFallbackSuffix = true)
        {
            return GetResolvedOrFallback(DuplicateNoticeStringPoolId, DuplicateNoticeFallback, appendFallbackSuffix);
        }

        internal static bool TryResolve(int stringPoolId, out string text)
        {
            return MapleStoryStringPool.TryGet(stringPoolId, out text);
        }

        private static string GetResolvedOrFallback(int stringPoolId, string fallbackText, bool appendFallbackSuffix)
        {
            return MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackText, appendFallbackSuffix);
        }
    }
}
