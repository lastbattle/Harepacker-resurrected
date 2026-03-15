using System.Text;
using System.Text.RegularExpressions;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class NpcDialogueTextFormatter
    {
        private static readonly Regex SelectionRegex = new(@"#L\d+#", RegexOptions.Compiled);
        private static readonly Regex NpcRegex = new(@"#p(\d+)#", RegexOptions.Compiled);
        private static readonly Regex ItemNameRegex = new(@"#t(\d+)#", RegexOptions.Compiled);
        private static readonly Regex ItemIconRegex = new(@"#i\d+#", RegexOptions.Compiled);
        private static readonly Regex PlayerNameRegex = new(@"#h\d*#", RegexOptions.Compiled);
        private static readonly Regex StyleTagRegex = new(@"#(?:[bkrgdenmc])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string Format(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string formatted = text
                .Replace("\\r\\n", "\n")
                .Replace("\\n", "\n")
                .Replace("\\r", "\n")
                .Replace("\r", string.Empty)
                .Replace("#l", string.Empty);

            formatted = SelectionRegex.Replace(formatted, string.Empty);
            formatted = PlayerNameRegex.Replace(formatted, "You");
            formatted = ItemIconRegex.Replace(formatted, string.Empty);
            formatted = NpcRegex.Replace(formatted, static match => ResolveNpcName(match.Groups[1].Value));
            formatted = ItemNameRegex.Replace(formatted, static match => ResolveItemName(match.Groups[1].Value));
            formatted = StyleTagRegex.Replace(formatted, string.Empty);

            var builder = new StringBuilder(formatted.Length);
            bool previousWasSpace = false;
            for (int i = 0; i < formatted.Length; i++)
            {
                char ch = formatted[i];
                if (ch == '\n')
                {
                    TrimTrailingSpace(builder);
                    builder.Append('\n');
                    previousWasSpace = false;
                    continue;
                }

                if (char.IsWhiteSpace(ch))
                {
                    if (!previousWasSpace)
                    {
                        builder.Append(' ');
                        previousWasSpace = true;
                    }

                    continue;
                }

                builder.Append(ch);
                previousWasSpace = false;
            }

            return builder.ToString().Trim();
        }

        private static void TrimTrailingSpace(StringBuilder builder)
        {
            while (builder.Length > 0 && builder[^1] == ' ')
            {
                builder.Length--;
            }
        }

        private static string ResolveNpcName(string npcIdText)
        {
            return Program.InfoManager?.NpcNameCache != null &&
                   Program.InfoManager.NpcNameCache.TryGetValue(npcIdText, out var info) &&
                   !string.IsNullOrWhiteSpace(info?.Item1)
                ? info.Item1
                : $"NPC #{npcIdText}";
        }

        private static string ResolveItemName(string itemIdText)
        {
            return int.TryParse(itemIdText, out int itemId) &&
                   Program.InfoManager?.ItemNameCache != null &&
                   Program.InfoManager.ItemNameCache.TryGetValue(itemId, out var itemInfo) &&
                   !string.IsNullOrWhiteSpace(itemInfo?.Item2)
                ? itemInfo.Item2
                : $"Item #{itemIdText}";
        }
    }
}
