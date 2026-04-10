namespace HaCreator.MapSimulator.Interaction
{
    internal static class SocialListGuildResultClientText
    {
        internal const int SharedResultNoticeStringPoolId = 0x0176;

        internal static string GetSharedResultNoticeFallback()
        {
            return MapleStoryStringPool.GetOrFallback(
                SharedResultNoticeStringPoolId,
                "The request has been processed.",
                appendFallbackSuffix: true,
                minimumHexWidth: 3);
        }
    }
}
