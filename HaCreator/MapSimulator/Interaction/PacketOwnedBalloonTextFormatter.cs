using System;
using System.Globalization;
using System.Text.RegularExpressions;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketOwnedBalloonTextFormattingContext
    {
        public string PlayerName { get; init; }
        public int ActiveQuestId { get; init; }
        public int? CurrentMapId { get; init; }
        public Func<int, string> ResolveItemCountText { get; init; }
        public Func<int, string> ResolveQuestStateText { get; init; }
        public Func<int, string> ResolveQuestRecordText { get; init; }
        public Func<string, string> ResolveQuestDetailRecordText { get; init; }
        public Func<string> ResolveJobNameText { get; init; }
        public Func<string> ResolveCurrentLevelText { get; init; }
        public Func<string> ResolveCurrentFameText { get; init; }
        public Func<string> ResolveCurrentMesoText { get; init; }
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
        private static readonly Regex MalformedItemNameRegex = new(@"#(?<id>\d{7,8}):?#", RegexOptions.Compiled);
        private static readonly Regex MobNameRegex = new(@"#o(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestRecordOrNameRegex = new(@"#Q(\d+):?#", RegexOptions.Compiled);
        private static readonly Regex QuestNameRegex = new(@"#(?:q|y)(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentQuestNameRegex = new(@"#q#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestReferenceNameRegex = new(@"#y(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentQuestReferenceNameRegex = new(@"#y#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestStateRegex = new(@"#u(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentQuestStateRegex = new(@"#u#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestRecordRegex = new(@"#R(\d+):?#", RegexOptions.Compiled);
        private static readonly Regex CurrentQuestRecordRegex = new(@"#R#", RegexOptions.Compiled);
        private static readonly Regex QuestTimerRecordRegex = new(@"#Q(?<token>[A-Za-z0-9_]+)#", RegexOptions.Compiled);
        private static readonly Regex DailyTimerRecordRegex = new(@"#D(?<token>[A-Za-z0-9_]+)#", RegexOptions.Compiled);
        private static readonly Regex SkillNameRegex = new(@"#s(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MapNameRegex = new(@"#m(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentMapNameRegex = new(@"#m#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestDetailRecordRegex = new(@"#j(?<token>[A-Za-z0-9_]+)#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex JobNameRegex = new(@"#j(?![A-Za-z0-9_])#?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SelectedMobRegex = new(@"#M(\d+):?#", RegexOptions.Compiled);
        private static readonly Regex CurrentSelectedMobRegex = new(@"#M#", RegexOptions.Compiled);
        private static readonly Regex QuestAmountRegex = new(@"#a(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentQuestAmountRegex = new(@"#a#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestValueRegex = new(@"#x(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentQuestValueRegex = new(@"#x#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ItemIconRegex = new(@"#(?:i|v)(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex UiCanvasRegex = new(@"#(?:f|F)([^#]+)#", RegexOptions.Compiled);
        private static readonly Regex StandaloneColorBlockRegex = new(@"#c(?!\d)(?<text>[^#]*)#", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex RewardCategoryRegex = new(@"#W(?<category>[^#\s]*)#", RegexOptions.Compiled);
        private static readonly Regex FontNameRegex = new(@"#fn[^#]*#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FontSizeRegex = new(@"#fs[+-]?\d+(?:\.\d+)?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FontSizeResetRegex = new(@"#fs#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FontTableRegex = new(@"#w(?:(?<value>basic|summary|select|reward|prob|default|black|red|green|blue|yellow|orange|gray|grey|purple|violet|magenta|0x[0-9a-fA-F]+|-?\d+)#|#|(?=$|\s))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ClientPromptTagRegex = new(@"#(?:E|I)#?", RegexOptions.Compiled);
        private static readonly Regex NumericPrefixedStyleRegex = new(@"#\d+(?<tag>[bkrgdeonymc])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TerminatedInlineStyleRegex = new(@"#(?<tag>[bkrgdeonymc])#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex InlineStyleBlockRegex = new(@"#(?<tag>[bkrgdeoymc])(?<text>[A-Z0-9][^#\r\n]*)#", RegexOptions.Compiled);
        private static readonly Regex MalformedPunctuationTagRegex = new(@"#(?<punct>[!?,.;:)])#?", RegexOptions.Compiled);
        private static readonly Regex InlineSelectionRegex = new(@"#L(?<id>-?\d+)#(?<text>.*?)(?:#l|(?=(?:\s|#(?:[A-Za-z])#?|#W[^#\s]*#|#(?:[!?,.;:)])#?)*#L-?\d+#)|\z)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex SelectionRegex = new(@"#L-?\d+#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex PluralSuffixRegex = new(@"#s(?!\d)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex LiteralPhraseHashRegex = new(@"#(?<text>[A-Z][^#\r\n]*\s[^#\r\n]*)#", RegexOptions.Compiled);
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
            formatted = MalformedItemNameRegex.Replace(formatted, static match => ResolveItemName(match.Groups["id"].Value));
            formatted = MobNameRegex.Replace(formatted, static match => ResolveMobName(match.Groups[1].Value));
            formatted = QuestRecordOrNameRegex.Replace(formatted, match => ResolveQuestRecordOrNameText(match.Groups[1].Value, context));
            formatted = QuestNameRegex.Replace(formatted, static match => ResolveQuestName(match.Groups[1].Value));
            formatted = CurrentQuestNameRegex.Replace(formatted, _ => ResolveActiveQuestNameText(context));
            formatted = QuestReferenceNameRegex.Replace(formatted, static match => ResolveQuestName(match.Groups[1].Value));
            formatted = CurrentQuestReferenceNameRegex.Replace(formatted, _ => ResolveActiveQuestNameText(context));
            formatted = QuestStateRegex.Replace(formatted, match => ResolveQuestStateText(match.Groups[1].Value, context));
            formatted = CurrentQuestStateRegex.Replace(formatted, _ => ResolveActiveQuestStateText(context));
            formatted = QuestRecordRegex.Replace(formatted, match => ResolveQuestRecordText(match.Groups[1].Value, context));
            formatted = CurrentQuestRecordRegex.Replace(formatted, _ => ResolveActiveQuestRecordText(context));
            formatted = QuestTimerRecordRegex.Replace(formatted, match => ResolveQuestTimerRecordText(match.Groups["token"].Value, context, match.Value));
            formatted = DailyTimerRecordRegex.Replace(formatted, match => ResolveQuestTimerRecordText(match.Groups["token"].Value, context, match.Value));
            formatted = SkillNameRegex.Replace(formatted, static match => ResolveSkillName(match.Groups[1].Value));
            formatted = MapNameRegex.Replace(formatted, static match => ResolveMapName(match.Groups[1].Value));
            formatted = CurrentMapNameRegex.Replace(formatted, _ => ResolveCurrentMapName(context));
            formatted = QuestDetailRecordRegex.Replace(formatted, match => ResolveQuestDetailRecordText(match.Groups["token"].Value, context, match.Value));
            formatted = JobNameRegex.Replace(formatted, _ => ResolveJobNameText(context));
            formatted = SelectedMobRegex.Replace(formatted, static match => ResolveSelectedMobText(match.Groups[1].Value));
            formatted = CurrentSelectedMobRegex.Replace(formatted, _ => ResolveActiveSelectedMobText(context));
            formatted = QuestAmountRegex.Replace(formatted, static match => ResolveQuestAmountText(match.Groups[1].Value));
            formatted = CurrentQuestAmountRegex.Replace(formatted, _ => ResolveActiveQuestAmountText(context));
            formatted = QuestValueRegex.Replace(formatted, static match => ResolveQuestValueText(match.Groups[1].Value));
            formatted = CurrentQuestValueRegex.Replace(formatted, _ => ResolveActiveQuestValueText(context));
            formatted = ItemIconRegex.Replace(formatted, static match => BuildItemIconMarker(match.Groups[1].Value));
            formatted = UiCanvasRegex.Replace(formatted, static match => BuildUiCanvasMarker(match.Groups[1].Value));
            formatted = StandaloneColorBlockRegex.Replace(formatted, static match => match.Groups["text"].Value);
            formatted = RewardCategoryRegex.Replace(formatted, static match => ResolveRewardCategoryMarker(match.Groups["category"].Value));
            formatted = FontNameRegex.Replace(formatted, static match => BuildFontControlMarker(PacketOwnedBalloonFontControlKind.FontName, match.Value.Length > 3 ? match.Value[3..^1] : string.Empty));
            formatted = FontSizeRegex.Replace(formatted, static match => BuildFontControlMarker(PacketOwnedBalloonFontControlKind.FontSize, match.Value.Length > 3 ? match.Value[3..^1] : string.Empty));
            formatted = FontSizeResetRegex.Replace(formatted, static _ => BuildFontControlMarker(PacketOwnedBalloonFontControlKind.FontSize, string.Empty));
            formatted = FontTableRegex.Replace(formatted, static match =>
            {
                if (TryResolveFontTableIndex(match.Groups["value"].Value, out int tableId))
                {
                    return BuildFontControlMarker(PacketOwnedBalloonFontControlKind.FontTable, tableId.ToString(CultureInfo.InvariantCulture));
                }

                return BuildFontControlMarker(PacketOwnedBalloonFontControlKind.FontTable, match.Groups["value"].Value);
            });
            formatted = ClientPromptTagRegex.Replace(formatted, string.Empty);
            formatted = NumericPrefixedStyleRegex.Replace(formatted, static match => "#" + match.Groups["tag"].Value);
            formatted = TerminatedInlineStyleRegex.Replace(
                formatted,
                static match => "#" + match.Groups["tag"].Value.ToLowerInvariant());
            formatted = InlineStyleBlockRegex.Replace(formatted, static match => ExpandInlineStyleBlock(match));
            formatted = MalformedPunctuationTagRegex.Replace(formatted, "${punct}");
            formatted = LiteralPhraseHashRegex.Replace(formatted, static match => match.Groups["text"].Value);
            formatted = PluralSuffixRegex.Replace(formatted, "s");
            formatted = PlaceholderRegex.Replace(formatted, match => ResolvePlaceholderText(match.Groups["token"].Value, context, match.Value));
            return formatted;
        }

        private static string ExpandInlineStyleBlock(Match match)
        {
            if (match == null)
            {
                return string.Empty;
            }

            string tag = match.Groups["tag"].Value;
            string text = match.Groups["text"].Value;
            if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrEmpty(text))
            {
                return match.Value;
            }

            return "#" + tag.ToLowerInvariant() + text + "#n";
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

        internal static bool TryResolveFontTableIndex(string value, out int tableId)
        {
            string normalized = value?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                tableId = 0;
                return true;
            }

            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(normalized[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out tableId))
            {
                return tableId >= 0 && tableId <= 11;
            }

            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out tableId))
            {
                return tableId >= 0 && tableId <= 11;
            }

            tableId = normalized.ToLowerInvariant() switch
            {
                "basic" => 0,
                "summary" => 0,
                "default" => 0,
                "black" => 10,
                "red" => 2,
                "green" => 4,
                "blue" => 6,
                "prob" => 6,
                "yellow" => 10,
                "orange" => 10,
                "gray" => 10,
                "grey" => 10,
                "purple" => 8,
                "violet" => 8,
                "magenta" => 8,
                "reward" => 8,
                "select" => 3,
                _ => -1
            };

            return tableId >= 0 && tableId <= 11;
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
                    case 'o':
                    case 'y':
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

            string statText = ResolveQuestDetailBuildStatText(normalizedToken, context);
            if (!string.IsNullOrWhiteSpace(statText))
            {
                return statText;
            }

            return fallbackText ?? string.Empty;
        }

        private static string ResolveQuestDetailBuildStatText(string token, PacketOwnedBalloonTextFormattingContext context)
        {
            string normalizedToken = token?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return null;
            }

            return normalizedToken switch
            {
                "level" or "lv" => ResolveContextText(context?.ResolveCurrentLevelText),
                "pop" or "fame" => ResolveContextText(context?.ResolveCurrentFameText),
                "money" or "meso" or "mesos" => ResolveContextText(context?.ResolveCurrentMesoText),
                _ => null
            };
        }

        private static string ResolveContextText(Func<string> resolver)
        {
            if (resolver == null)
            {
                return null;
            }

            string resolvedText = resolver();
            return string.IsNullOrWhiteSpace(resolvedText)
                ? null
                : resolvedText;
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

        private static string ResolveActiveQuestStateText(PacketOwnedBalloonTextFormattingContext context)
        {
            int activeQuestId = context?.ActiveQuestId ?? 0;
            return activeQuestId > 0
                ? ResolveQuestStateText(activeQuestId.ToString(CultureInfo.InvariantCulture), context)
                : "Not started";
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

        private static string ResolveQuestRecordOrNameText(string questIdText, PacketOwnedBalloonTextFormattingContext context)
        {
            string recordText = ResolveQuestRecordText(questIdText, context);
            return string.IsNullOrWhiteSpace(recordText) || string.Equals(recordText, "0", StringComparison.Ordinal)
                ? ResolveQuestName(questIdText)
                : recordText;
        }

        private static string ResolveActiveQuestNameText(PacketOwnedBalloonTextFormattingContext context)
        {
            int activeQuestId = context?.ActiveQuestId ?? 0;
            return activeQuestId > 0
                ? ResolveQuestName(activeQuestId.ToString(CultureInfo.InvariantCulture))
                : "current quest";
        }

        private static string ResolveActiveQuestRecordText(PacketOwnedBalloonTextFormattingContext context)
        {
            int activeQuestId = context?.ActiveQuestId ?? 0;
            return activeQuestId > 0
                ? ResolveQuestRecordText(activeQuestId.ToString(CultureInfo.InvariantCulture), context)
                : "0";
        }

        private static string ResolveQuestTimerRecordText(
            string token,
            PacketOwnedBalloonTextFormattingContext context,
            string fallbackText)
        {
            string normalizedToken = token?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedToken))
            {
                return fallbackText ?? string.Empty;
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
                _ when normalizedToken.EndsWith("limit", StringComparison.OrdinalIgnoreCase) => "0",
                _ when normalizedToken.StartsWith("gauge", StringComparison.OrdinalIgnoreCase) => "0",
                _ when normalizedToken.StartsWith("per", StringComparison.OrdinalIgnoreCase) => "0",
                _ when normalizedToken.StartsWith("have", StringComparison.OrdinalIgnoreCase) => "0",
                _ => fallbackText ?? string.Empty
            };
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

        private static string ResolveActiveSelectedMobText(PacketOwnedBalloonTextFormattingContext context)
        {
            int activeQuestId = context?.ActiveQuestId ?? 0;
            return activeQuestId > 0
                ? ResolveSelectedMobText(activeQuestId.ToString(CultureInfo.InvariantCulture))
                : "the selected monster";
        }

        private static string ResolveActiveQuestAmountText(PacketOwnedBalloonTextFormattingContext context)
        {
            int activeQuestId = context?.ActiveQuestId ?? 0;
            return activeQuestId > 0
                ? ResolveQuestAmountText(activeQuestId.ToString(CultureInfo.InvariantCulture))
                : "the listed amount";
        }

        private static string ResolveActiveQuestValueText(PacketOwnedBalloonTextFormattingContext context)
        {
            int activeQuestId = context?.ActiveQuestId ?? 0;
            return activeQuestId > 0
                ? ResolveQuestValueText(activeQuestId.ToString(CultureInfo.InvariantCulture))
                : "the listed amount";
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
