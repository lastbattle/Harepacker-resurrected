namespace HaCreator.MapSimulator.Interaction
{
    internal static class SkillMacroOwnerStringPoolText
    {
        internal const int SaveButtonTooltipStringPoolId = 0x1108;
        internal const int SaveNoticeStringPoolId = 0x0D01;

        private const string SaveButtonTooltipFallback = SaveButtonTooltipResolved;
        private const string SaveNoticeFallback = SaveNoticeResolved;
        private const string SaveButtonTooltipResolved = "Saving the skill name.";
        private const string SaveNoticeResolved = "It is saved.";

        public static string GetSaveButtonTooltip(bool appendFallbackSuffix = true)
        {
            return GetResolvedOrFallback(SaveButtonTooltipStringPoolId, SaveButtonTooltipFallback, appendFallbackSuffix);
        }

        public static string GetSaveNotice(bool appendFallbackSuffix = true)
        {
            return GetResolvedOrFallback(SaveNoticeStringPoolId, SaveNoticeFallback, appendFallbackSuffix);
        }

        public static bool TryResolve(int stringPoolId, out string text)
        {
            return MapleStoryStringPool.TryGet(stringPoolId, out text);
        }

        private static string GetResolvedOrFallback(int stringPoolId, string fallbackText, bool appendFallbackSuffix)
        {
            return MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackText, appendFallbackSuffix, minimumHexWidth: 3);
        }
    }
}
