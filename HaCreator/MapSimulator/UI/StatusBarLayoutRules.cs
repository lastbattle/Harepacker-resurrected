using System;

namespace HaCreator.MapSimulator.UI
{
    internal static class StatusBarLayoutRules
    {
        private const float BaseLevelSlotX = 44f;
        private const int LevelDigitSlotStep = 6;

        public static float ResolveLevelSlotX(string levelText)
        {
            int digitCount = Math.Clamp(string.IsNullOrEmpty(levelText) ? 1 : levelText.Length, 1, 3);
            return BaseLevelSlotX - ((digitCount - 1) * LevelDigitSlotStep);
        }

        public static string FormatJobLabel(string jobName)
        {
            string resolvedName = SanitizeSingleLineText(jobName);
            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                resolvedName = "Beginner";
            }

            int openParen = resolvedName.IndexOf('(');
            int closeParen = resolvedName.IndexOf(')', openParen + 1);
            if (openParen >= 0 && closeParen > openParen + 1)
            {
                resolvedName = resolvedName.Substring(openParen + 1, closeParen - openParen - 1).Trim();
            }

            return resolvedName;
        }

        public static string FormatNameLabel(string name)
        {
            string resolvedName = SanitizeSingleLineText(name);
            return string.IsNullOrWhiteSpace(resolvedName) ? "Player" : resolvedName;
        }

        private static string SanitizeSingleLineText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return string.Join(" ",
                text
                    .Replace("\r", " ")
                    .Replace("\n", " ")
                    .Replace("\t", " ")
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
