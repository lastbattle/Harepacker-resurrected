using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class AnimationDisplayerQuestDeliveryStringPoolText
    {
        internal const int ArriveTemplateStringPoolId = 0x1A05;
        internal const int WaitTemplateStringPoolId = 0x1A06;
        internal const int LeaveTemplateStringPoolId = 0x1A07;

        private const string ArriveTemplateFallback = "Effect/ItemEff.img/%d/arrive";
        private const string WaitTemplateFallback = "Effect/ItemEff.img/%d/wait";
        private const string LeaveTemplateFallback = "Effect/ItemEff.img/%d/leave";

        public static string ResolveArrivePath(int itemId)
        {
            return ResolvePath(ArriveTemplateStringPoolId, ArriveTemplateFallback, itemId);
        }

        public static string ResolveWaitPath(int itemId)
        {
            return ResolvePath(WaitTemplateStringPoolId, WaitTemplateFallback, itemId);
        }

        public static string ResolveLeavePath(int itemId)
        {
            return ResolvePath(LeaveTemplateStringPoolId, LeaveTemplateFallback, itemId);
        }

        private static string ResolvePath(int stringPoolId, string fallbackFormat, int itemId)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormat,
                maxPlaceholderCount: 1,
                out _);
            return string.Format(CultureInfo.InvariantCulture, format, itemId);
        }
    }
}
