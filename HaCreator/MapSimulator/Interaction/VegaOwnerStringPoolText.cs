namespace HaCreator.MapSimulator.Interaction
{
    internal static class VegaOwnerStringPoolText
    {
        private const int MissingSelectionStringPoolId = 5422;
        private const int ExclusiveRequestStringPoolId = 278;
        private const int BlockedStateStringPoolId = 66;
        private const int IncompatiblePairStringPoolId = 5423;
        private const int WhiteScrollPromptStringPoolId = 0x1160;
        private const int UnknownResultStringPoolId = 5424;
        private const int UnexpectedResultStringPoolId = 6764;

        public static string GetMissingSelectionNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                MissingSelectionStringPoolId,
                "Select both equipment and a compatible scroll before casting Vega's Spell.");
        }

        public static string GetExclusiveRequestNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                ExclusiveRequestStringPoolId,
                "Another exclusive item request is already pending.");
        }

        public static string GetBlockedStateNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                BlockedStateStringPoolId,
                "Vega's Spell is unavailable in the current character state.");
        }

        public static string GetIncompatiblePairNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                IncompatiblePairStringPoolId,
                "The selected equipment and scroll are not compatible.");
        }

        public static string GetWhiteScrollPrompt()
        {
            return MapleStoryStringPool.GetOrFallback(
                WhiteScrollPromptStringPoolId,
                "Use White Scroll protection for this Vega request?");
        }

        public static string FormatUnknownResultNotice(int resultCode)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                UnknownResultStringPoolId,
                "Vega returned result code {0}.",
                1,
                out _);
            return string.Format(format, resultCode);
        }

        public static string GetUnexpectedResultNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                UnexpectedResultStringPoolId,
                "Vega's Spell returned an unexpected result.");
        }
    }
}
