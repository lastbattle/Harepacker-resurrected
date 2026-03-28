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
        public Func<int, string> ResolveItemCountText { get; init; }
        public Func<int, string> ResolveQuestStateText { get; init; }
    }

    internal static class NpcDialogueTextFormatter
    {
        private static readonly Regex InlineSelectionRegex = new(@"#L(?<id>-?\d+)#(?<text>.*?)#l", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex SelectionRegex = new(@"#L\d+#", RegexOptions.Compiled);
        private static readonly Regex ItemCountRegex = new(@"#c(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex NpcRegex = new(@"#p(\d+):?#", RegexOptions.Compiled);
        private static readonly Regex ItemNameRegex = new(@"#t(\d+):?#", RegexOptions.Compiled);
        private static readonly Regex MobNameRegex = new(@"#o(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestNameRegex = new(@"#q(\d+):?#", RegexOptions.Compiled);
        private static readonly Regex QuestReferenceNameRegex = new(@"#y(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestStateRegex = new(@"#u(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SkillNameRegex = new(@"#s(\d+):?#", RegexOptions.Compiled);
        private static readonly Regex MapNameRegex = new(@"#m(\d+):?#", RegexOptions.Compiled);
        private static readonly Regex SelectedMobRegex = new(@"#M(\d+):?#", RegexOptions.Compiled);
        private static readonly Regex QuestAmountRegex = new(@"#a(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestValueRegex = new(@"#x(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ItemNameAliasRegex = new(@"#z(\d+):?#", RegexOptions.Compiled);
        private static readonly Regex ItemIconRegex = new(@"#(?:i|v)\d+:?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RewardCategoryRegex = new(@"#W[^#\s]*#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex PlayerNameRegex = new(@"#h\d*#", RegexOptions.Compiled);
        private static readonly Regex StyleTagRegex = new(@"#(?:[bkrgdenmc])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ClientPromptTagRegex = new(@"#(?:E|I)", RegexOptions.Compiled);

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
            formatted = PlayerNameRegex.Replace(formatted, "You");
            formatted = ItemIconRegex.Replace(formatted, string.Empty);
            formatted = RewardCategoryRegex.Replace(formatted, string.Empty);
            formatted = ItemCountRegex.Replace(formatted, match => ResolveItemCountText(match.Groups[1].Value, context));
            formatted = NpcRegex.Replace(formatted, static match => ResolveNpcName(match.Groups[1].Value));
            formatted = ItemNameRegex.Replace(formatted, static match => ResolveItemName(match.Groups[1].Value));
            formatted = MobNameRegex.Replace(formatted, static match => ResolveMobName(match.Groups[1].Value));
            formatted = ItemNameAliasRegex.Replace(formatted, static match => ResolveItemName(match.Groups[1].Value));
            formatted = QuestNameRegex.Replace(formatted, static match => ResolveQuestName(match.Groups[1].Value));
            formatted = QuestReferenceNameRegex.Replace(formatted, static match => ResolveQuestName(match.Groups[1].Value));
            formatted = QuestStateRegex.Replace(formatted, match => ResolveQuestStateText(match.Groups[1].Value, context));
            formatted = SkillNameRegex.Replace(formatted, static match => ResolveSkillName(match.Groups[1].Value));
            formatted = MapNameRegex.Replace(formatted, static match => ResolveMapName(match.Groups[1].Value));
            formatted = SelectedMobRegex.Replace(formatted, static match => ResolveSelectedMobText(match.Groups[1].Value));
            formatted = QuestAmountRegex.Replace(formatted, static match => ResolveQuestAmountText(match.Groups[1].Value));
            formatted = QuestValueRegex.Replace(formatted, static match => ResolveQuestValueText(match.Groups[1].Value));
            formatted = StyleTagRegex.Replace(formatted, string.Empty);
            formatted = ClientPromptTagRegex.Replace(formatted, string.Empty);

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

                string label = Format(match.Groups["text"].Value);
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

        private static NpcInteractionPage FormatPage(NpcInteractionPage page, NpcDialogueFormattingContext context)
        {
            if (page == null)
            {
                return null;
            }

            string rawText = !string.IsNullOrWhiteSpace(page.RawText) ? page.RawText : page.Text;
            string text = Format(rawText, context);
            IReadOnlyList<NpcInteractionChoice> choices = FormatChoices(page.Choices, context);
            if (string.IsNullOrWhiteSpace(text) && choices.Count == 0)
            {
                return null;
            }

            return new NpcInteractionPage
            {
                RawText = rawText ?? string.Empty,
                Text = text,
                Choices = choices
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

                formattedChoices[count++] = new NpcInteractionChoice
                {
                    Label = choice.Label,
                    Pages = FormatPages(choice.Pages, context)
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
