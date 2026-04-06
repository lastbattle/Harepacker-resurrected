using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static partial class MapleStoryStringPool
    {
        public static int Count => Entries.Length;

        public static bool Contains(int stringPoolId)
        {
            return (uint)stringPoolId < (uint)Entries.Length;
        }

        public static bool TryGet(int stringPoolId, out string text)
        {
            if (Contains(stringPoolId))
            {
                text = Entries[stringPoolId];
                return true;
            }

            text = null;
            return false;
        }

        public static string GetOrFallback(int stringPoolId, string fallbackText, bool appendFallbackSuffix = false, int minimumHexWidth = 0)
        {
            if (TryGet(stringPoolId, out string resolvedText))
            {
                return resolvedText;
            }

            if (!appendFallbackSuffix)
            {
                return fallbackText;
            }

            return $"{fallbackText} ({FormatFallbackLabel(stringPoolId, minimumHexWidth)} fallback)";
        }

        public static string GetCompositeFormatOrFallback(
            int stringPoolId,
            string fallbackFormat,
            int maxPlaceholderCount,
            out bool usedResolvedText)
        {
            if (TryGet(stringPoolId, out string resolvedFormat))
            {
                usedResolvedText = true;
                return ConvertPrintfFormatToCompositeFormat(resolvedFormat, maxPlaceholderCount);
            }

            usedResolvedText = false;
            return fallbackFormat;
        }

        public static string FormatFallbackLabel(int stringPoolId, int minimumHexWidth = 0)
        {
            string format = minimumHexWidth > 0 ? $"X{minimumHexWidth}" : "X";
            return $"StringPool 0x{stringPoolId.ToString(format)}";
        }

        private static string ConvertPrintfFormatToCompositeFormat(string format, int maxPlaceholderCount)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return string.Empty;
            }

            int tokenIndex = 0;
            int searchStart = 0;
            while (tokenIndex < maxPlaceholderCount)
            {
                int markerIndex = FindNextPrintfPlaceholder(format, searchStart);
                if (markerIndex < 0)
                {
                    break;
                }

                string replacement = $"{{{tokenIndex}}}";
                format = format.Remove(markerIndex, 2).Insert(markerIndex, replacement);
                searchStart = markerIndex + replacement.Length;
                tokenIndex++;
            }

            return format;
        }

        private static int FindNextPrintfPlaceholder(string format, int searchStart)
        {
            int stringIndex = format.IndexOf("%s", searchStart, StringComparison.Ordinal);
            int digitIndex = format.IndexOf("%d", searchStart, StringComparison.Ordinal);

            if (stringIndex < 0)
            {
                return digitIndex;
            }

            if (digitIndex < 0)
            {
                return stringIndex;
            }

            return Math.Min(stringIndex, digitIndex);
        }
    }
}
