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
    }

    internal static class PacketOwnedBalloonTextFormatter
    {
        private static readonly Regex PlayerNameRegex = new(@"#h\d*#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex NpcRegex = new(@"#p(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ItemNameRegex = new(@"#(?:t|z)(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MobNameRegex = new(@"#o(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuestNameRegex = new(@"#(?:q|y)(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SkillNameRegex = new(@"#s(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MapNameRegex = new(@"#m(\d+):?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentMapNameRegex = new(@"#m#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ItemIconRegex = new(@"#(?:i|v)\d+:?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RewardCategoryRegex = new(@"#W[^#\s]*#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ClientPromptTagRegex = new(@"#(?:E|I)", RegexOptions.Compiled);

        public static string Format(string text, PacketOwnedBalloonTextFormattingContext context = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string formatted = NormalizeLineBreaks(text).Replace("\r", string.Empty);
            formatted = PlayerNameRegex.Replace(formatted, _ => ResolvePlayerName(context));
            formatted = NpcRegex.Replace(formatted, static match => ResolveNpcName(match.Groups[1].Value));
            formatted = ItemNameRegex.Replace(formatted, static match => ResolveItemName(match.Groups[1].Value));
            formatted = MobNameRegex.Replace(formatted, static match => ResolveMobName(match.Groups[1].Value));
            formatted = QuestNameRegex.Replace(formatted, static match => ResolveQuestName(match.Groups[1].Value));
            formatted = SkillNameRegex.Replace(formatted, static match => ResolveSkillName(match.Groups[1].Value));
            formatted = MapNameRegex.Replace(formatted, static match => ResolveMapName(match.Groups[1].Value));
            formatted = CurrentMapNameRegex.Replace(formatted, _ => ResolveCurrentMapName(context));
            formatted = ItemIconRegex.Replace(formatted, string.Empty);
            formatted = RewardCategoryRegex.Replace(formatted, string.Empty);
            formatted = ClientPromptTagRegex.Replace(formatted, string.Empty);
            return formatted;
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
    }
}
