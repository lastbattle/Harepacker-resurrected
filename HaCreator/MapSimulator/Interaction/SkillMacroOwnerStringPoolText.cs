namespace HaCreator.MapSimulator.Interaction
{
    internal static class SkillMacroOwnerStringPoolText
    {
        internal const int SaveButtonTooltipStringPoolId = 0x1108;
        internal const int SaveNoticeStringPoolId = 0x0D01;

        // Recovered from MapleStory.exe StringPool::ms_aString with StringPool::GetString decode path:
        // - 0x1108 seed 0x3F raw "3F A7 12 4B 80 F7 FF 53 E3 03 0A 1A B0 48 5B 3D D4 D4 1D 5C 84 FC B6"
        // - 0x0D01 seed 0x38 raw "38 38 9C C6 12 A0 13 43 86 58 B3 BA 5B"
        private const string SaveButtonTooltipResolved = "Saving the skill name.";
        private const string SaveButtonTooltipFallback = SaveButtonTooltipResolved;
        private const string SaveNoticeResolved = "It is saved.";
        private const string SaveNoticeFallback = SaveNoticeResolved;

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
            text = stringPoolId switch
            {
                SaveButtonTooltipStringPoolId => SaveButtonTooltipResolved,
                SaveNoticeStringPoolId => SaveNoticeResolved,
                _ => null,
            };

            return text != null;
        }

        private static string GetResolvedOrFallback(int stringPoolId, string fallbackText, bool appendFallbackSuffix)
        {
            if (TryResolve(stringPoolId, out string resolvedText))
            {
                return resolvedText;
            }

            if (!appendFallbackSuffix)
            {
                return fallbackText;
            }

            return $"{fallbackText} (StringPool 0x{stringPoolId:X3} fallback)";
        }
    }
}
