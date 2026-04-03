using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    internal static class QuestAlarmTextLayout
    {
        private static readonly char[] PreferredTokenBreakCharacters =
        {
            '/',
            '\\',
            '-',
            '_',
            ':',
            '.',
            ',',
            ')',
            ']',
            '>',
            '+',
            '='
        };

        internal static string ResolveRegistrationLimitMessage(string tooltipDescription)
        {
            if (string.IsNullOrWhiteSpace(tooltipDescription))
            {
                return "Up to 5 quests can be registered in this alarm.";
            }

            string[] sentences = tooltipDescription
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            string registrationSentence = sentences.LastOrDefault(sentence =>
                sentence.Contains("register", StringComparison.OrdinalIgnoreCase) &&
                sentence.Contains("quest", StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrWhiteSpace(registrationSentence)
                ? tooltipDescription.Trim()
                : $"{registrationSentence}.";
        }

        internal static IReadOnlyList<string> WrapText(string text, float maxWidth, Func<string, float> measureWidth)
        {
            if (string.IsNullOrWhiteSpace(text) || measureWidth == null || maxWidth <= 0f)
            {
                return Array.Empty<string>();
            }

            List<string> lines = new();
            foreach (string block in text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
            {
                string trimmedBlock = block.Trim();
                if (trimmedBlock.Length == 0)
                {
                    continue;
                }

                string currentLine = string.Empty;
                foreach (string token in TokenizeBlock(trimmedBlock, maxWidth, measureWidth))
                {
                    string candidate = string.IsNullOrEmpty(currentLine) ? token : $"{currentLine} {token}";
                    if (!string.IsNullOrEmpty(currentLine) && measureWidth(candidate) > maxWidth)
                    {
                        lines.Add(currentLine);
                        currentLine = token;
                    }
                    else
                    {
                        currentLine = candidate;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                }
            }

            return lines;
        }

        internal static string TruncateToWidth(string text, float maxWidth, Func<string, float> measureWidth)
        {
            if (string.IsNullOrWhiteSpace(text) || measureWidth == null || maxWidth <= 0f)
            {
                return string.Empty;
            }

            string trimmed = text.Trim();
            if (measureWidth(trimmed) <= maxWidth)
            {
                return trimmed;
            }

            const string ellipsis = "...";
            float ellipsisWidth = measureWidth(ellipsis);
            if (ellipsisWidth >= maxWidth)
            {
                return ellipsis;
            }

            int preferredBreakIndex = ResolvePreferredTruncationIndex(trimmed, maxWidth, measureWidth, ellipsisWidth);
            if (preferredBreakIndex > 0)
            {
                return trimmed[..preferredBreakIndex].TrimEnd() + ellipsis;
            }

            StringBuilder builder = new();
            foreach (char character in trimmed)
            {
                string candidate = builder.ToString() + character + ellipsis;
                if (builder.Length > 0 && measureWidth(candidate) > maxWidth)
                {
                    break;
                }

                builder.Append(character);
            }

            return builder.Length == 0 ? ellipsis : builder + ellipsis;
        }

        private static int ResolvePreferredTruncationIndex(string text, float maxWidth, Func<string, float> measureWidth, float ellipsisWidth)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int bestIndex = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (!PreferredTokenBreakCharacters.Contains(text[i]))
                {
                    continue;
                }

                int candidateIndex = i + 1;
                string candidate = text[..candidateIndex].TrimEnd();
                if (candidate.Length == 0)
                {
                    continue;
                }

                if (measureWidth(candidate) + ellipsisWidth <= maxWidth)
                {
                    bestIndex = candidateIndex;
                }
            }

            return bestIndex;
        }

        private static IEnumerable<string> TokenizeBlock(string block, float maxWidth, Func<string, float> measureWidth)
        {
            foreach (string word in block.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (string segment in SplitTokenToWidth(word, maxWidth, measureWidth))
                {
                    yield return segment;
                }
            }
        }

        private static IEnumerable<string> SplitTokenToWidth(string token, float maxWidth, Func<string, float> measureWidth)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                yield break;
            }

            if (measureWidth(token) <= maxWidth)
            {
                yield return token;
                yield break;
            }

            List<string> preferredSegments = SplitAtPreferredBreaks(token);
            if (preferredSegments.Count > 1)
            {
                foreach (string segment in preferredSegments.SelectMany(segment => SplitTokenToWidth(segment, maxWidth, measureWidth)))
                {
                    yield return segment;
                }

                yield break;
            }

            foreach (string segment in SplitByCharacterWidth(token, maxWidth, measureWidth))
            {
                yield return segment;
            }
        }

        private static List<string> SplitAtPreferredBreaks(string token)
        {
            List<string> segments = new();
            StringBuilder current = new();
            for (int i = 0; i < token.Length; i++)
            {
                char currentChar = token[i];
                current.Append(currentChar);
                if (PreferredTokenBreakCharacters.Contains(currentChar))
                {
                    segments.Add(current.ToString());
                    current.Clear();
                }
            }

            if (current.Length > 0)
            {
                segments.Add(current.ToString());
            }

            return segments;
        }

        private static IEnumerable<string> SplitByCharacterWidth(string token, float maxWidth, Func<string, float> measureWidth)
        {
            StringBuilder current = new();
            for (int i = 0; i < token.Length; i++)
            {
                string candidate = current.ToString() + token[i];
                if (current.Length > 0 && measureWidth(candidate) > maxWidth)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                current.Append(token[i]);
            }

            if (current.Length > 0)
            {
                yield return current.ToString();
            }
        }

    }
}
