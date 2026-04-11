namespace HaCreator.MapSimulator.Interaction
{
    internal static class EntrustedShopBlacklistDialogText
    {
        internal const int AddPromptStringPoolId = 0x2E5;
        internal const int OwnerNoticeStringPoolId = 742;
        internal const int InvalidNameNoticeStringPoolId = 743;
        internal const int DuplicateNoticeStringPoolId = 744;

        private const string AddPromptFallback = "Enter the name of the character you'd like to add to your blacklist.";
        private const string OwnerNoticeFallback = "You may not enter yourself in the Blacklist";
        private const string InvalidNameNoticeFallback = "That character cannot be added.";
        private const string DuplicateNoticeFallback = "This name has already been registered\r\nPlease check again";

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
