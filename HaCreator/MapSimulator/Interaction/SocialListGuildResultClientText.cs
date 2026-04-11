using System;
using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class SocialListGuildResultClientText
    {
        internal const int SharedResultNoticeStringPoolId = 0x0176;
        private const int GuildQuestEnterNoticeStringPoolId = 0x0DFF;
        private const int GuildQuestWaitNoticeStringPoolId = 0x0E00;
        private const int GuildQuestWaitlistNoticeStringPoolId = 0x0E01;

        internal static string GetSharedResultNoticeFallback()
        {
            return MapleStoryStringPool.GetOrFallback(
                SharedResultNoticeStringPoolId,
                "The request has been processed.",
                appendFallbackSuffix: true,
                minimumHexWidth: 3);
        }

        internal static string FormatGuildQuestQueueNotice(int channel, int waitStatus)
        {
            if (waitStatus <= 0)
            {
                return "Guild Quest queue notice cleared.";
            }

            int stringPoolId = waitStatus switch
            {
                1 => GuildQuestEnterNoticeStringPoolId,
                2 => GuildQuestWaitNoticeStringPoolId,
                _ => GuildQuestWaitlistNoticeStringPoolId
            };
            string fallbackFormat = waitStatus switch
            {
                1 => "Please go see the Guild Quest NPC at Channel {0} immediately to enter.",
                2 => "Your guild is up next. Please head to the Guild Quest map at Channel {0} and wait.",
                _ => "There's currently 1 guild participating in the Guild Quest, and your guild is number {0} on the waitlist."
            };
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(stringPoolId, fallbackFormat, 1, out _);
            object argument = waitStatus <= 2
                ? Math.Max(0, channel).ToString(CultureInfo.InvariantCulture)
                : Math.Max(0, waitStatus - 1);

            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, argument);
            }
            catch (FormatException)
            {
                return string.Format(CultureInfo.InvariantCulture, fallbackFormat, argument);
            }
        }
    }
}
