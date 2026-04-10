using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class NpcDialogueFormattingContext
    {
        public int ActiveQuestId { get; init; }
        public Func<string> ResolvePlayerNameText { get; init; }
        public Func<int, string> ResolveItemCountText { get; init; }
        public Func<int, string> ResolveQuestStateText { get; init; }
        public Func<string> ResolveJobNameText { get; init; }
        public Func<string> ResolveCurrentMapNameText { get; init; }
        public Func<int, string> ResolveQuestRecordText { get; init; }
        public Func<string, string> ResolveQuestDetailRecordText { get; init; }
    }

    internal static class NpcDialogueTextFormatter
    {
        private static readonly Regex InlineSelectionRegex = new(@"#L(?<id>-?\d+)#(?<text>.*?)#l", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex SelectionRegex = new(@"#L-?\d+#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ItemCountRegex = new(@"#c(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex NpcRegex = new(@"#p(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ItemNameRegex = new(@"#t(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MobNameRegex = new(@"#o(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestNameRegex = new(@"#q(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentQuestNameRegex = new(@"#q#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestReferenceNameRegex = new(@"#y(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentQuestReferenceNameRegex = new(@"#y#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestStateRegex = new(@"#u(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentQuestStateRegex = new(@"#u#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SkillNameRegex = new(@"#s(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MapNameRegex = new(@"#m(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentMapNameRegex = new(@"#m#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex JobNameRegex = new(@"#j(?![A-Za-z0-9_])#?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestDetailRecordRegex = new(@"#j(?<token>[A-Za-z0-9_]+)#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SelectedMobRegex = new(@"#M(\d+):?#", RegexOptions.Compiled);
        private static readonly Regex CurrentSelectedMobRegex = new(@"#M#", RegexOptions.Compiled);
        private static readonly Regex QuestAmountRegex = new(@"#a(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentQuestAmountRegex = new(@"#a#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestValueRegex = new(@"#x(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentQuestValueRegex = new(@"#x#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestRecordRegex = new(@"#R(\d+):?#", RegexOptions.Compiled);
        private static readonly Regex CurrentQuestRecordRegex = new(@"#R#", RegexOptions.Compiled);
        private static readonly Regex ItemNameAliasRegex = new(@"#z(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ItemIconRegex = new(@"#(?:i|v)\d+:?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RewardCategoryRegex = new(@"#W[^#\s]*#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestDetailStyleRegex = new(@"#(?<tag>[bkrgdenmc])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MalformedQuestDetailStyleRegex = new(@"#\d+(?<tag>[bkrgdenmc])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex PluralSuffixRegex = new(@"#s(?!\d)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex PlayerNameRegex = new(@"#h\d*#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex StyleTagRegex = new(@"#(?:[bkrgdenmc])", RegexOptions.Compiled);
        private static readonly Regex ClientPromptTagRegex = new(@"#(?:E|I)#?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex LiteralWordHashRegex = new(@"#(?=[A-Z][A-Za-z]+\b)", RegexOptions.Compiled);

        public static string Format(string text, NpcDialogueFormattingContext context = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string formatted = NormalizeLineBreaks(StripInlineSelections(text))
                .Replace("\r", string.Empty)
                .Replace("#l", string.Empty);

            formatted = SelectionRegex.Replace(formatted, string.Empty);
            formatted = PlayerNameRegex.Replace(formatted, match => ResolvePlayerNameText(context));
            formatted = ItemIconRegex.Replace(formatted, string.Empty);
            formatted = RewardCategoryRegex.Replace(formatted, string.Empty);
            formatted = ItemCountRegex.Replace(formatted, match => ResolveItemCountText(match.Groups[1].Value, context));
            formatted = NpcRegex.Replace(formatted, static match => ResolveNpcName(match.Groups[1].Value));
            formatted = ItemNameRegex.Replace(formatted, static match => ResolveItemName(match.Groups[1].Value));
            formatted = MobNameRegex.Replace(formatted, static match => ResolveMobName(match.Groups[1].Value));
            formatted = ItemNameAliasRegex.Replace(formatted, static match => ResolveItemName(match.Groups[1].Value));
            formatted = QuestNameRegex.Replace(formatted, static match => ResolveQuestName(match.Groups[1].Value));
            formatted = CurrentQuestNameRegex.Replace(formatted, match => ResolveActiveQuestNameText(context));
            formatted = QuestReferenceNameRegex.Replace(formatted, static match => ResolveQuestName(match.Groups[1].Value));
            formatted = CurrentQuestReferenceNameRegex.Replace(formatted, match => ResolveActiveQuestNameText(context));
            formatted = QuestStateRegex.Replace(formatted, match => ResolveQuestStateText(match.Groups[1].Value, context));
            formatted = CurrentQuestStateRegex.Replace(formatted, match => ResolveActiveQuestStateText(context));
            formatted = SkillNameRegex.Replace(formatted, static match => ResolveSkillName(match.Groups[1].Value));
            formatted = MapNameRegex.Replace(formatted, static match => ResolveMapName(match.Groups[1].Value));
            formatted = CurrentMapNameRegex.Replace(formatted, match => ResolveCurrentMapNameText(context));
            formatted = QuestDetailRecordRegex.Replace(formatted, match => ResolveQuestDetailRecordText(match.Groups["token"].Value, context));
            formatted = JobNameRegex.Replace(formatted, match => ResolveJobNameText(context));
            formatted = SelectedMobRegex.Replace(formatted, static match => ResolveSelectedMobText(match.Groups[1].Value));
            formatted = CurrentSelectedMobRegex.Replace(formatted, match => ResolveActiveSelectedMobText(context));
            formatted = QuestAmountRegex.Replace(formatted, static match => ResolveQuestAmountText(match.Groups[1].Value));
            formatted = CurrentQuestAmountRegex.Replace(formatted, match => ResolveActiveQuestAmountText(context));
            formatted = QuestValueRegex.Replace(formatted, static match => ResolveQuestValueText(match.Groups[1].Value));
            formatted = CurrentQuestValueRegex.Replace(formatted, match => ResolveActiveQuestValueText(context));
            formatted = QuestRecordRegex.Replace(formatted, match => ResolveQuestRecordText(match.Groups[1].Value, context));
            formatted = CurrentQuestRecordRegex.Replace(formatted, match => ResolveActiveQuestRecordText(context));
            formatted = StyleTagRegex.Replace(formatted, string.Empty);
            formatted = ClientPromptTagRegex.Replace(formatted, string.Empty);
            formatted = LiteralWordHashRegex.Replace(formatted, string.Empty);
            formatted = PluralSuffixRegex.Replace(formatted, "s");

            return NormalizeWhitespace(formatted);
        }

        public static string FormatPreservingItemIcons(string text, NpcDialogueFormattingContext context = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string preservedIcons = ItemIconRegex.Replace(
                text,
                match => int.TryParse(match.Value.TrimStart('#', 'i', 'I', 'v', 'V').TrimEnd('#', ':'), out int itemId) && itemId > 0
                    ? BuildItemIconMarker(itemId)
                    : string.Empty);
            return Format(preservedIcons, context);
        }

        public static string FormatPreservingQuestDetailMarkers(string text, NpcDialogueFormattingContext context = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string preservedMarkers = RewardCategoryRegex.Replace(
                text,
                match => BuildQuestSurfaceMarker(match.Value.TrimStart('#', 'W', 'w').TrimEnd('#')));
            preservedMarkers = ItemIconRegex.Replace(
                preservedMarkers,
                match => int.TryParse(match.Value.TrimStart('#', 'i', 'I', 'v', 'V').TrimEnd('#', ':'), out int itemId) && itemId > 0
                    ? BuildItemIconMarker(itemId)
                    : string.Empty);
            preservedMarkers = MalformedQuestDetailStyleRegex.Replace(
                preservedMarkers,
                match => BuildQuestStyleMarker(match.Groups["tag"].Value));
            preservedMarkers = Regex.Replace(
                preservedMarkers,
                "#c#",
                BuildQuestStyleMarker("c"),
                RegexOptions.IgnoreCase);
            string styleInput = preservedMarkers;
            preservedMarkers = QuestDetailStyleRegex.Replace(
                preservedMarkers,
                match => PreserveQuestDetailStyleTag(match, styleInput));
            return Format(preservedMarkers, context);
        }

        private static string PreserveQuestDetailStyleTag(Match match, string text)
        {
            string styleTag = match.Groups["tag"].Value;
            if (ShouldPreserveQuestDetailParameterizedToken(styleTag, text, match.Index + match.Length))
            {
                return match.Value;
            }

            return BuildQuestStyleMarker(styleTag);
        }

        private static bool ShouldPreserveQuestDetailParameterizedToken(string styleTag, string text, int nextIndex)
        {
            if (string.IsNullOrWhiteSpace(styleTag) ||
                string.IsNullOrEmpty(text) ||
                nextIndex < 0 ||
                nextIndex >= text.Length)
            {
                return false;
            }

            char nextCharacter = text[nextIndex];
            if (string.Equals(styleTag, "m", StringComparison.OrdinalIgnoreCase))
            {
                return nextCharacter == '#' ||
                       IsTerminatedQuestDetailNumericToken(text, nextIndex);
            }

            if (string.Equals(styleTag, "c", StringComparison.OrdinalIgnoreCase))
            {
                return IsTerminatedQuestDetailNumericToken(text, nextIndex);
            }

            return false;
        }

        private static bool IsTerminatedQuestDetailNumericToken(string text, int digitStartIndex)
        {
            if (string.IsNullOrEmpty(text) ||
                digitStartIndex < 0 ||
                digitStartIndex >= text.Length ||
                !char.IsDigit(text[digitStartIndex]))
            {
                return false;
            }

            int index = digitStartIndex + 1;
            while (index < text.Length && char.IsDigit(text[index]))
            {
                index++;
            }

            if (index < text.Length && text[index] == ':')
            {
                index++;
            }

            return index < text.Length && text[index] == '#';
        }

        public static string BuildItemIconMarker(int itemId)
        {
            return itemId > 0
                ? $"{{{{ITEMICON:{itemId}}}}}"
                : string.Empty;
        }

        public static string BuildQuestSurfaceMarker(string surfaceKey)
        {
            string normalizedKey = surfaceKey?.Trim();
            return string.IsNullOrWhiteSpace(normalizedKey)
                ? string.Empty
                : $"{{{{QUESTSURFACE:{normalizedKey}}}}}";
        }

        public static string BuildQuestStyleMarker(string styleTag)
        {
            string normalizedTag = styleTag?.Trim().ToLowerInvariant();
            return string.IsNullOrWhiteSpace(normalizedTag)
                ? string.Empty
                : $"{{{{QUESTSTYLE:{normalizedTag}}}}}";
        }

        public static IReadOnlyList<NpcInteractionPage> FormatPages(IReadOnlyList<NpcInteractionPage> pages, NpcDialogueFormattingContext context)
        {
            if (pages == null || pages.Count == 0)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            var formattedPages = new NpcInteractionPage[pages.Count];
            int count = 0;
            for (int i = 0; i < pages.Count; i++)
            {
                NpcInteractionPage formattedPage = FormatPage(pages[i], context);
                if (formattedPage != null)
                {
                    formattedPages[count++] = formattedPage;
                }
            }

            if (count == 0)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            if (count == formattedPages.Length)
            {
                return formattedPages;
            }

            var resizedPages = new NpcInteractionPage[count];
            Array.Copy(formattedPages, resizedPages, count);
            return resizedPages;
        }

        public static NpcInlineSelection[] ExtractInlineSelections(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return System.Array.Empty<NpcInlineSelection>();
            }

            MatchCollection matches = InlineSelectionRegex.Matches(NormalizeLineBreaks(text));
            if (matches.Count == 0)
            {
                return System.Array.Empty<NpcInlineSelection>();
            }

            var selections = new NpcInlineSelection[matches.Count];
            int count = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                if (!int.TryParse(match.Groups["id"].Value, out int selectionId))
                {
                    continue;
                }

                string label = NormalizeLineBreaks(match.Groups["text"].Value)
                    .Replace("\r", string.Empty)
                    .Trim();
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                selections[count++] = new NpcInlineSelection(selectionId, label);
            }

            if (count == selections.Length)
            {
                return selections;
            }

            var resizedSelections = new NpcInlineSelection[count];
            System.Array.Copy(selections, resizedSelections, count);
            return resizedSelections;
        }

        private static string StripInlineSelections(string text)
        {
            return InlineSelectionRegex.Replace(text, string.Empty);
        }

        private static string NormalizeLineBreaks(string text)
        {
            return text
                .Replace("\\r\\n", "\n")
                .Replace("\\n", "\n")
                .Replace("\\r", "\n");
        }

        private static void TrimTrailingSpace(StringBuilder builder)
        {
            while (builder.Length > 0 && builder[^1] == ' ')
            {
                builder.Length--;
            }
        }

        private static string NormalizeWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            int pendingSpaces = 0;
            bool lineHasContent = false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\n')
                {
                    TrimTrailingSpace(builder);
                    builder.Append('\n');
                    pendingSpaces = 0;
                    lineHasContent = false;
                    continue;
                }

                if (ch == ' ')
                {
                    if (lineHasContent)
                    {
                        pendingSpaces++;
                    }

                    continue;
                }

                if (char.IsWhiteSpace(ch))
                {
                    if (lineHasContent)
                    {
                        pendingSpaces = Math.Max(pendingSpaces, 1);
                    }

                    continue;
                }

                if (pendingSpaces > 0)
                {
                    builder.Append(' ', pendingSpaces);
                    pendingSpaces = 0;
                }

                builder.Append(ch);
                lineHasContent = true;
            }

            TrimTrailingSpace(builder);
            return builder.ToString().Trim('\n');
        }

        private static NpcInteractionPage FormatPage(NpcInteractionPage page, NpcDialogueFormattingContext context)
        {
            if (page == null)
            {
                return null;
            }

            string rawText = !string.IsNullOrWhiteSpace(page.RawText) ? page.RawText : page.Text;
            string text = Format(rawText, context);
            IReadOnlyList<NpcInteractionChoice> choices = FormatChoices(page.Choices, context);
            if (string.IsNullOrWhiteSpace(text) &&
                choices.Count == 0 &&
                page.InputRequest == null)
            {
                return null;
            }

            return new NpcInteractionPage
            {
                RawText = rawText ?? string.Empty,
                Text = text,
                Choices = choices,
                InputRequest = page.InputRequest,
                FlipSpeaker = page.FlipSpeaker
            };
        }

        private static IReadOnlyList<NpcInteractionChoice> FormatChoices(IReadOnlyList<NpcInteractionChoice> choices, NpcDialogueFormattingContext context)
        {
            if (choices == null || choices.Count == 0)
            {
                return Array.Empty<NpcInteractionChoice>();
            }

            var formattedChoices = new NpcInteractionChoice[choices.Count];
            int count = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                NpcInteractionChoice choice = choices[i];
                if (choice == null || string.IsNullOrWhiteSpace(choice.Label))
                {
                    continue;
                }

                string label = Format(choice.Label, context);
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                formattedChoices[count++] = new NpcInteractionChoice
                {
                    Label = label,
                    Pages = FormatPages(choice.Pages, context),
                    SubmitSelection = choice.SubmitSelection,
                    SubmissionKind = choice.SubmissionKind,
                    SubmissionValue = choice.SubmissionValue,
                    SubmissionNumericValue = choice.SubmissionNumericValue
                };
            }

            if (count == 0)
            {
                return Array.Empty<NpcInteractionChoice>();
            }

            if (count == formattedChoices.Length)
            {
                return formattedChoices;
            }

            var resizedChoices = new NpcInteractionChoice[count];
            Array.Copy(formattedChoices, resizedChoices, count);
            return resizedChoices;
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

        private static string ResolveQuestName(string questIdText)
        {
            return Program.InfoManager?.QuestInfos != null &&
                   Program.InfoManager.QuestInfos.TryGetValue(questIdText, out var questInfo) &&
                   questInfo?["name"] is WzStringProperty nameProperty &&
                   !string.IsNullOrWhiteSpace(nameProperty.Value)
                ? nameProperty.Value
                : $"Quest #{questIdText}";
        }

        private static string ResolveSkillName(string skillIdText)
        {
            return Program.InfoManager?.SkillNameCache != null &&
                   Program.InfoManager.SkillNameCache.TryGetValue(skillIdText, out var skillInfo) &&
                   !string.IsNullOrWhiteSpace(skillInfo?.Item1)
                ? skillInfo.Item1
                : $"Skill #{skillIdText}";
        }

        private static string ResolveMapName(string mapIdText)
        {
            if (Program.InfoManager?.MapsNameCache == null)
            {
                return $"Map #{mapIdText}";
            }

            string normalizedMapId = mapIdText.Length == 9 ? mapIdText : mapIdText.PadLeft(9, '0');
            return Program.InfoManager.MapsNameCache.TryGetValue(normalizedMapId, out var mapInfo) &&
                   !string.IsNullOrWhiteSpace(mapInfo?.Item2)
                ? mapInfo.Item2
                : $"Map #{mapIdText}";
        }

        private static string ResolveMobName(string mobIdText)
        {
            return Program.InfoManager?.MobNameCache != null &&
                   Program.InfoManager.MobNameCache.TryGetValue(mobIdText, out string mobName) &&
                   !string.IsNullOrWhiteSpace(mobName)
                ? mobName
                : $"Mob #{mobIdText}";
        }

        private static string ResolveItemCountText(string itemIdText, NpcDialogueFormattingContext context)
        {
            if (context?.ResolveItemCountText != null &&
                int.TryParse(itemIdText, out int itemId) &&
                itemId > 0)
            {
                string resolvedText = context.ResolveItemCountText(itemId);
                if (!string.IsNullOrWhiteSpace(resolvedText))
                {
                    return resolvedText;
                }
            }

            return "0";
        }

        private static string ResolvePlayerNameText(NpcDialogueFormattingContext context)
        {
            if (context?.ResolvePlayerNameText != null)
            {
                string resolvedText = context.ResolvePlayerNameText();
                if (!string.IsNullOrWhiteSpace(resolvedText))
                {
                    return resolvedText;
                }
            }

            return "You";
        }

        private static string ResolveQuestStateText(string questIdText, NpcDialogueFormattingContext context)
        {
            if (context?.ResolveQuestStateText != null &&
                int.TryParse(questIdText, out int questId) &&
                questId > 0)
            {
                string resolvedText = context.ResolveQuestStateText(questId);
                if (!string.IsNullOrWhiteSpace(resolvedText))
                {
                    return resolvedText;
                }
            }

            return "Not started";
        }

        private static string ResolveActiveQuestNameText(NpcDialogueFormattingContext context)
        {
            int activeQuestId = context?.ActiveQuestId ?? 0;
            return activeQuestId > 0
                ? ResolveQuestName(activeQuestId.ToString())
                : "current quest";
        }

        private static string ResolveActiveQuestStateText(NpcDialogueFormattingContext context)
        {
            int activeQuestId = context?.ActiveQuestId ?? 0;
            return activeQuestId > 0
                ? ResolveQuestStateText(activeQuestId.ToString(), context)
                : "Not started";
        }

        private static string ResolveCurrentMapNameText(NpcDialogueFormattingContext context)
        {
            if (context?.ResolveCurrentMapNameText != null)
            {
                string resolvedText = context.ResolveCurrentMapNameText();
                if (!string.IsNullOrWhiteSpace(resolvedText))
                {
                    return resolvedText;
                }
            }

            return "this map";
        }

        private static string ResolveJobNameText(NpcDialogueFormattingContext context)
        {
            if (context?.ResolveJobNameText != null)
            {
                string resolvedText = context.ResolveJobNameText();
                if (!string.IsNullOrWhiteSpace(resolvedText))
                {
                    return resolvedText;
                }
            }

            return "your job";
        }

        private static string ResolveSelectedMobText(string questIdText)
        {
            if (TryGetQuestMobRequirement(questIdText, out int mobId, out _))
            {
                return ResolveMobName(mobId.ToString());
            }

            return HasSelectedMobFlag(questIdText)
                ? "the selected monster"
                : $"Mob #{questIdText}";
        }

        private static string ResolveQuestAmountText(string questIdText)
        {
            if (TryGetQuestMobRequirement(questIdText, out _, out int mobCount))
            {
                return mobCount.ToString();
            }

            if (TryGetQuestItemRequirement(questIdText, out _, out int itemCount))
            {
                return itemCount.ToString();
            }

            return "the listed amount";
        }

        private static string ResolveQuestValueText(string questIdText)
        {
            return HasSelectedMobFlag(questIdText)
                ? "the selected bonus amount"
                : ResolveQuestAmountText(questIdText);
        }

        private static string ResolveActiveSelectedMobText(NpcDialogueFormattingContext context)
        {
            int activeQuestId = context?.ActiveQuestId ?? 0;
            return activeQuestId > 0
                ? ResolveSelectedMobText(activeQuestId.ToString())
                : "the selected monster";
        }

        private static string ResolveActiveQuestAmountText(NpcDialogueFormattingContext context)
        {
            int activeQuestId = context?.ActiveQuestId ?? 0;
            return activeQuestId > 0
                ? ResolveQuestAmountText(activeQuestId.ToString())
                : "the listed amount";
        }

        private static string ResolveActiveQuestValueText(NpcDialogueFormattingContext context)
        {
            int activeQuestId = context?.ActiveQuestId ?? 0;
            return activeQuestId > 0
                ? ResolveQuestValueText(activeQuestId.ToString())
                : "the listed amount";
        }

        private static string ResolveQuestRecordText(string questIdText, NpcDialogueFormattingContext context)
        {
            if (context?.ResolveQuestRecordText != null &&
                int.TryParse(questIdText, out int questId) &&
                questId > 0)
            {
                string resolvedText = context.ResolveQuestRecordText(questId);
                if (!string.IsNullOrWhiteSpace(resolvedText))
                {
                    return resolvedText;
                }
            }

            return "0";
        }

        private static string ResolveActiveQuestRecordText(NpcDialogueFormattingContext context)
        {
            int activeQuestId = context?.ActiveQuestId ?? 0;
            return activeQuestId > 0
                ? ResolveQuestRecordText(activeQuestId.ToString(), context)
                : "0";
        }

        private static string ResolveQuestDetailRecordText(string token, NpcDialogueFormattingContext context)
        {
            string normalizedToken = token?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return string.Empty;
            }

            if (context?.ResolveQuestDetailRecordText != null)
            {
                string resolvedText = context.ResolveQuestDetailRecordText(normalizedToken);
                if (resolvedText != null)
                {
                    return resolvedText;
                }
            }

            return normalizedToken.ToLowerInvariant() switch
            {
                "cmp" => "0",
                "min" => "0",
                "sec" => "0",
                "date" => "-",
                "rank" => "-",
                _ when normalizedToken.StartsWith("have", StringComparison.OrdinalIgnoreCase) => "0",
                _ => string.Empty
            };
        }

        private static bool HasSelectedMobFlag(string questIdText)
        {
            return Program.InfoManager?.QuestInfos != null &&
                   Program.InfoManager.QuestInfos.TryGetValue(questIdText, out WzSubProperty questInfo) &&
                   questInfo?["selectedMob"] is WzIntProperty selectedMob &&
                   selectedMob.GetInt() > 0;
        }

        private static bool TryGetQuestMobRequirement(string questIdText, out int mobId, out int count)
        {
            if (TryGetQuestRequirement(questIdText, "mob", out WzImageProperty requirementProperty) &&
                TryGetFirstRequirement(requirementProperty, out mobId, out count))
            {
                return true;
            }

            mobId = 0;
            count = 0;
            return false;
        }

        private static bool TryGetQuestItemRequirement(string questIdText, out int itemId, out int count)
        {
            if (TryGetQuestRequirement(questIdText, "item", out WzImageProperty requirementProperty) &&
                TryGetFirstRequirement(requirementProperty, out itemId, out count))
            {
                return true;
            }

            itemId = 0;
            count = 0;
            return false;
        }

        private static bool TryGetQuestRequirement(string questIdText, string requirementName, out WzImageProperty requirementProperty)
        {
            requirementProperty = null;
            if (Program.InfoManager?.QuestChecks == null ||
                !Program.InfoManager.QuestChecks.TryGetValue(questIdText, out WzSubProperty questCheck))
            {
                return false;
            }

            WzSubProperty completionCheck = questCheck?["1"] as WzSubProperty;
            if (completionCheck?[requirementName] is WzImageProperty completionRequirement)
            {
                requirementProperty = completionRequirement;
                return true;
            }

            WzSubProperty startCheck = questCheck?["0"] as WzSubProperty;
            if (startCheck?[requirementName] is WzImageProperty startRequirement)
            {
                requirementProperty = startRequirement;
                return true;
            }

            return false;
        }

        private static bool TryGetFirstRequirement(WzImageProperty requirementProperty, out int id, out int count)
        {
            if (requirementProperty?.WzProperties == null)
            {
                id = 0;
                count = 0;
                return false;
            }

            for (int i = 0; i < requirementProperty.WzProperties.Count; i++)
            {
                WzImageProperty child = requirementProperty.WzProperties[i];
                int resolvedId = GetIntValue(child?["id"]);
                int resolvedCount = System.Math.Abs(GetIntValue(child?["count"]));
                if (resolvedId <= 0 || resolvedCount <= 0)
                {
                    continue;
                }

                id = resolvedId;
                count = resolvedCount;
                return true;
            }

            id = 0;
            count = 0;
            return false;
        }

        private static int GetIntValue(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProp => intProp.GetInt(),
                WzShortProperty shortProp => shortProp.GetShort(),
                WzLongProperty longProp => checked((int)longProp.Value),
                _ => 0
            };
        }
    }
}
