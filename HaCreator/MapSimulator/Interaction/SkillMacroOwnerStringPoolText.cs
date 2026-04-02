namespace HaCreator.MapSimulator.Interaction
{
    internal static class SkillMacroOwnerStringPoolText
    {
        internal const int SaveButtonTooltipStringPoolId = 0x1108;
        internal const int SaveNoticeStringPoolId = 0x0D01;

        private const string SaveButtonTooltipFallback = "Save the selected macro.";
        private const string SaveNoticeFallback = "The selected macro has been saved.";

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
