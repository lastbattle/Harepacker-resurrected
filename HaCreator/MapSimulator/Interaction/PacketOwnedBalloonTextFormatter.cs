using System;
using System.Text.RegularExpressions;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketOwnedBalloonTextFormattingContext
    {
        public string PlayerName { get; init; }
        public int? CurrentMapId { get; init; }
        public Func<int, string> ResolveItemCountText { get; init; }
        public Func<int, string> ResolveQuestStateText { get; init; }
        public Func<int, string> ResolveQuestRecordText { get; init; }
        public Func<string, string> ResolveQuestDetailRecordText { get; init; }
        public Func<string> ResolveJobNameText { get; init; }
        public Func<string, string> ResolvePlaceholderText { get; init; }
    }

    internal enum PacketOwnedBalloonFontControlKind
    {
        None = 0,
        FontName,
        FontSize,
        FontTable
    }

    internal static class PacketOwnedBalloonTextFormatter
    {
        private const string ItemIconMarkerPrefix = "{{ITEMICON:";
        private const string ItemIconMarkerSuffix = "}}";
        private const string UiCanvasMarkerPrefix = "{{UICANVAS:";
        private const string UiCanvasMarkerSuffix = "}}";
        private const string FontControlMarkerPrefix = "{{FONTCTRL:";
        private const string FontControlMarkerSuffix = "}}";

        private static readonly Regex ItemCountRegex = new(@"#c(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex PlayerNameRegex = new(@"#h\d*#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex NpcRegex = new(@"#p(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ItemNameRegex = new(@"#(?:t|z)(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MobNameRegex = new(@"#o(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestNameRegex = new(@"#(?:q|y)(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestReferenceNameRegex = new(@"#y(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestStateRegex = new(@"#u(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestRecordRegex = new(@"#R(\d+):?#", RegexOptions.Compiled);
        private static readonly Regex SkillNameRegex = new(@"#s(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MapNameRegex = new(@"#m(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentMapNameRegex = new(@"#m#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestDetailRecordRegex = new(@"#j(?<token>[A-Za-z0-9_]+)#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex JobNameRegex = new(@"#j(?![A-Za-z0-9_])#?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SelectedMobRegex = new(@"#M(\d+):?#", RegexOptions.Compiled);
        private static readonly Regex QuestAmountRegex = new(@"#a(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestValueRegex = new(@"#x(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ItemIconRegex = new(@"#(?:i|v)(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex UiCanvasRegex = new(@"#(?:f|F)([^#]+)#", RegexOptions.Compiled);
        private static readonly Regex RewardCategoryRegex = new(@"#W(?<category>[^#\s]*)#", RegexOptions.Compiled);
        private static readonly Regex FontNameRegex = new(@"#fn[^#]*#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FontSizeRegex = new(@"#fs-?\d+#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FontTableRegex = new(@"#w(?:[^#\s]*#)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ClientPromptTagRegex = new(@"#(?:E|I)#?", RegexOptions.Compiled);
        private static readonly Regex NumericPrefixedStyleRegex = new(@"#\d+(?<tag>[bkrgdenmc])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex InlineSelectionRegex = new(@"#L(?<id>-?\d+)#(?<text>.*?)#l", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex SelectionRegex = new(@"#L\d+#", RegexOptions.Compiled);
        private static readonly Regex PluralSuffixRegex = new(@"#s(?!\d)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex PlaceholderRegex = new(@"#(?<token>[A-Za-z][A-Za-z0-9_]*)#", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string Format(string text, PacketOwnedBalloonTextFormattingContext context = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string formatted = NormalizeLineBreaks(StripInlineSelections(text))
                .Replace("\r", string.Empty)
                .Replace("#l", string.Empty);
            formatted = SelectionRegex.Replace(formatted, string.Empty);
            formatted = ItemCountRegex.Replace(formatted, match => ResolveItemCountText(match.Groups[1].Value, context));
            formatted = PlayerNameRegex.Replace(formatted, _ => ResolvePlayerName(context));
            formatted = NpcRegex.Replace(formatted, static match => ResolveNpcName(match.Groups[1].Value));
            formatted = ItemNameRegex.Replace(formatted, static match => ResolveItemName(match.Groups[1].Value));
            formatted = MobNameRegex.Replace(formatted, static match => ResolveMobName(match.Groups[1].Value));
            formatted = QuestNameRegex.Replace(formatted, static match => ResolveQuestName(match.Groups[1].Value));
            formatted = QuestReferenceNameRegex.Replace(formatted, static match => ResolveQuestName(match.Groups[1].Value));
            formatted = QuestStateRegex.Replace(formatted, match => ResolveQuestStateText(match.Groups[1].Value, context));
            formatted = QuestRecordRegex.Replace(formatted, match => ResolveQuestRecordText(match.Groups[1].Value, context));
            formatted = SkillNameRegex.Replace(formatted, static match => ResolveSkillName(match.Groups[1].Value));
            formatted = MapNameRegex.Replace(formatted, static match => ResolveMapName(match.Groups[1].Value));
            formatted = CurrentMapNameRegex.Replace(formatted, _ => ResolveCurrentMapName(context));
            formatted = QuestDetailRecordRegex.Replace(formatted, match => ResolveQuestDetailRecordText(match.Groups["token"].Value, context, match.Value));
            formatted = JobNameRegex.Replace(formatted, _ => ResolveJobNameText(context));
            formatted = SelectedMobRegex.Replace(formatted, static match => ResolveSelectedMobText(match.Groups[1].Value));
            formatted = QuestAmountRegex.Replace(formatted, static match => ResolveQuestAmountText(match.Groups[1].Value));
            formatted = QuestValueRegex.Replace(formatted, static match => ResolveQuestValueText(match.Groups[1].Value));
            formatted = ItemIconRegex.Replace(formatted, static match => BuildItemIconMarker(match.Groups[1].Value));
            formatted = UiCanvasRegex.Replace(formatted, static match => BuildUiCanvasMarker(match.Groups[1].Value));
            formatted = RewardCategoryRegex.Replace(formatted, static match => ResolveRewardCategoryMarker(match.Groups["category"].Value));
            formatted = FontNameRegex.Replace(formatted, static match => BuildFontControlMarker(PacketOwnedBalloonFontControlKind.FontName, match.Value.Length > 3 ? match.Value[3..^1] : string.Empty));
            formatted = FontSizeRegex.Replace(formatted, static match => BuildFontControlMarker(PacketOwnedBalloonFontControlKind.FontSize, match.Value.Length > 3 ? match.Value[3..^1] : string.Empty));
            formatted = FontTableRegex.Replace(formatted, static match =>
            {
                string value = match.Value.Length > 2 && match.Value[^1] == '#'
                    ? match.Value[2..^1]
                    : string.Empty;
                return BuildFontControlMarker(PacketOwnedBalloonFontControlKind.FontTable, value);
            });
            formatted = ClientPromptTagRegex.Replace(formatted, string.Empty);
            formatted = NumericPrefixedStyleRegex.Replace(formatted, static match => "#" + match.Groups["tag"].Value);
            formatted = PluralSuffixRegex.Replace(formatted, "s");
            formatted = PlaceholderRegex.Replace(formatted, match => ResolvePlaceholderText(match.Groups["token"].Value, context, match.Value));
            return formatted;
        }

        internal static bool TryParseFontControlMarker(
            string text,
            int startIndex,
            out PacketOwnedBalloonFontControlKind kind,
            out string value,
            out int markerLength)
        {
            kind = PacketOwnedBalloonFontControlKind.None;
            value = string.Empty;
            markerLength = 0;
            if (string.IsNullOrEmpty(text)
                || startIndex < 0
                || startIndex >= text.Length
                || !text.AsSpan(startIndex).StartsWith(FontControlMarkerPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            int payloadStart = startIndex + FontControlMarkerPrefix.Length;
            int suffixIndex = text.IndexOf(FontControlMarkerSuffix, payloadStart, StringComparison.Ordinal);
            if (suffixIndex <= payloadStart)
            {
                return false;
            }

            string payload = text.Substring(payloadStart, suffixIndex - payloadStart);
            int separatorIndex = payload.IndexOf(':');
            string kindText = separatorIndex >= 0 ? payload[..separatorIndex] : payload;
            value = separatorIndex >= 0 ? payload[(separatorIndex + 1)..] : string.Empty;
            if (!Enum.TryParse(kindText, ignoreCase: true, out kind)
                || kind == PacketOwnedBalloonFontControlKind.None)
            {
                kind = PacketOwnedBalloonFontControlKind.None;
                value = string.Empty;
                return false;
            }

            markerLength = (suffixIndex - startIndex) + FontControlMarkerSuffix.Length;
            return true;
        }

        private static string StripInlineSelections(string text)
        {
            return InlineSelectionRegex.Replace(text, static match => match.Groups["text"].Value);
        }

        private static string NormalizeLineBreaks(string text)
        {
            return text
                .Replace("\\r\\n", "\n")
                .Replace("\\n", "\n")
                .Replace("\\r", "\n");
        }

        private static string ResolvePlayerName(PacketOwnedBalloonTextFormattingContext context)
        {
            return !string.IsNullOrWhiteSpace(context?.PlayerName)
                ? context.PlayerName.Trim()
                : "You";
        }

        private static string ResolveCurrentMapName(PacketOwnedBalloonTextFormattingContext context)
        {
            return context?.CurrentMapId is int mapId && mapId > 0
                ? ResolveMapName(mapId.ToString())
                : "this map";
        }

        private static string ResolveJobNameText(PacketOwnedBalloonTextFormattingContext context)
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

        private static string ResolvePlaceholderText(string token, PacketOwnedBalloonTextFormattingContext context, string fallbackText)
        {
            string normalizedToken = token?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return fallbackText ?? string.Empty;
            }

            if (IsReservedControlPlaceholder(normalizedToken))
            {
                return fallbackText ?? string.Empty;
            }

            if (context?.ResolvePlaceholderText != null)
            {
                string resolvedText = context.ResolvePlaceholderText(normalizedToken);
                if (!string.IsNullOrWhiteSpace(resolvedText))
                {
                    return resolvedText;
                }
            }

            return normalizedToken;
        }

        private static bool IsReservedControlPlaceholder(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (token.Length == 1)
            {
                switch (char.ToLowerInvariant(token[0]))
                {
                    case 'b':
                    case 'k':
                    case 'r':
                    case 'g':
                    case 'd':
                    case 'e':
                    case 'n':
                    case 'm':
                    case 'c':
                    case 'i':
                    case 'v':
                    case 'f':
                    case 'w':
                    case 'j':
                        return true;
                }
            }

            return token.StartsWith("fn", StringComparison.OrdinalIgnoreCase)
                || token.StartsWith("fs", StringComparison.OrdinalIgnoreCase)
                || token.StartsWith("w", StringComparison.OrdinalIgnoreCase)
                || token.StartsWith("j", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveQuestDetailRecordText(string token, PacketOwnedBalloonTextFormattingContext context, string fallbackText)
        {
            string normalizedToken = token?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return fallbackText ?? string.Empty;
            }

            if (context?.ResolveQuestDetailRecordText != null)
            {
                string resolvedText = context.ResolveQuestDetailRecordText(normalizedToken);
                if (!string.IsNullOrWhiteSpace(resolvedText))
                {
                    return resolvedText;
                }
            }

            return fallbackText ?? string.Empty;
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

        private static string BuildItemIconMarker(string itemIdText)
        {
            return int.TryParse(itemIdText, out int itemId) && itemId > 0
                ? $"{ItemIconMarkerPrefix}{itemId}{ItemIconMarkerSuffix}"
                : string.Empty;
        }

        private static string BuildUiCanvasMarker(string canvasPath)
        {
            string normalizedPath = canvasPath?.Trim().Replace('\\', '/');
            return string.IsNullOrWhiteSpace(normalizedPath)
                ? string.Empty
                : $"{UiCanvasMarkerPrefix}{normalizedPath}{UiCanvasMarkerSuffix}";
        }

        private static string ResolveRewardCategoryMarker(string category)
        {
            string normalizedCategory = category?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedCategory))
            {
                return string.Empty;
            }

            normalizedCategory = normalizedCategory.ToLowerInvariant();
            return normalizedCategory switch
            {
                "summary" or "basic" or "reward" or "select" or "prob" =>
                    BuildUiCanvasMarker($"UI/UIWindow2.img/Quest/quest_info/summary_icon/{normalizedCategory}"),
                _ => string.Empty
            };
        }

        private static string BuildFontControlMarker(PacketOwnedBalloonFontControlKind kind, string value)
        {
            return kind == PacketOwnedBalloonFontControlKind.None
                ? string.Empty
                : $"{FontControlMarkerPrefix}{kind}:{value ?? string.Empty}{FontControlMarkerSuffix}";
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

        private static string ResolveItemCountText(string itemIdText, PacketOwnedBalloonTextFormattingContext context)
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

        private static string ResolveQuestStateText(string questIdText, PacketOwnedBalloonTextFormattingContext context)
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

        private static string ResolveQuestRecordText(string questIdText, PacketOwnedBalloonTextFormattingContext context)
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
            return Program.InfoManager?.QuestInfos != null &&
                   Program.InfoManager.QuestInfos.TryGetValue(questIdText, out WzSubProperty questInfo) &&
                   questInfo?["0"] is WzSubProperty stateZero &&
                   stateZero[requirementName] is WzImageProperty property &&
                   (requirementProperty = property) != null;
        }

        private static bool TryGetFirstRequirement(WzImageProperty property, out int id, out int count)
        {
            if (property is not WzSubProperty subProperty || subProperty.WzProperties == null)
            {
                id = 0;
                count = 0;
                return false;
            }

            for (int i = 0; i < subProperty.WzProperties.Count; i++)
            {
                if (subProperty.WzProperties[i] is not WzSubProperty requirement)
                {
                    continue;
                }

                int resolvedId = GetRequirementValue(requirement["id"]);
                int resolvedCount = GetRequirementValue(requirement["count"]);
                if (resolvedId <= 0)
                {
                    continue;
                }

                id = resolvedId;
                count = Math.Max(0, resolvedCount);
                return true;
            }

            id = 0;
            count = 0;
            return false;
        }

        private static int GetRequirementValue(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.GetInt(),
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => (int)longProperty.Value,
                _ => 0
            };
        }
    }
}
