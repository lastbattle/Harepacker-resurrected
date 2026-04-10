namespace HaCreator.MapSimulator.Interaction
{
    internal static class AccountMoreInfoOwnerStringPoolText
    {
        internal const int OkButtonUolStringPoolId = 0x512;
        internal const int CancelButtonUolStringPoolId = 0x513;
        internal const int BackgroundStringPoolId = 0x16AE;
        internal const int ExitWithoutInfoNoticeStringPoolId = 0x16B6;
        internal const int SaveFailedNoticeStringPoolId = 0x16B7;
        internal const int DefaultRegionItemStringPoolId = 0x16B8;
        internal const int FirstEntryPromptStringPoolId = 0x16B5;

        private const string OkButtonUolFallback = "UI/Basic.img/BtOK2";
        private const string CancelButtonUolFallback = "UI/Basic.img/BtCancel2";
        private const string BackgroundFallback = "UI/UIWindow.img/FriendRecommendations/UserInfo/back";
        private const string ExitWithoutInfoNoticeFallback = "Are you sure you want to exit without filling in any information? (You can fill out your info later by clicking My Info in the Friends window.)";
        private const string SaveFailedNoticeFallback = "Fail. Please try again later.";
        private const string DefaultRegionItemFallback = "Select";
        private const string FirstEntryPromptFallback = "Filling in your information will help us recommend friends who share your interests! \r\nDo you want to fill in your information now?";

        internal static string ResolveOkButtonResourcePath()
        {
            return MapleStoryStringPool.GetOrFallback(
                OkButtonUolStringPoolId,
                OkButtonUolFallback,
                appendFallbackSuffix: true);
        }

        internal static string ResolveCancelButtonResourcePath()
        {
            return MapleStoryStringPool.GetOrFallback(
                CancelButtonUolStringPoolId,
                CancelButtonUolFallback,
                appendFallbackSuffix: true);
        }

        internal static string ResolveBackgroundResourcePath()
        {
            return MapleStoryStringPool.GetOrFallback(
                BackgroundStringPoolId,
                BackgroundFallback,
                appendFallbackSuffix: true);
        }

        internal static string ResolveExitWithoutInfoNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                ExitWithoutInfoNoticeStringPoolId,
                ExitWithoutInfoNoticeFallback,
                appendFallbackSuffix: true);
        }

        internal static string ResolveSaveFailedNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                SaveFailedNoticeStringPoolId,
                SaveFailedNoticeFallback,
                appendFallbackSuffix: true);
        }

        internal static string ResolveDefaultRegionItem()
        {
            return MapleStoryStringPool.GetOrFallback(
                DefaultRegionItemStringPoolId,
                DefaultRegionItemFallback,
                appendFallbackSuffix: true);
        }

        internal static string ResolveFirstEntryPrompt()
        {
            return MapleStoryStringPool.GetOrFallback(
                FirstEntryPromptStringPoolId,
                FirstEntryPromptFallback,
                appendFallbackSuffix: true);
        }
    }
}
