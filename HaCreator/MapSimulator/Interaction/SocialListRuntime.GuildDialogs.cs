using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed partial class SocialListRuntime
    {
        internal GuildDialogContext BuildGuildDialogContext(CharacterBuild build)
        {
            bool hasGuildMembership = ResolveEffectiveGuildMembership(build);
            string guildName = ResolveEffectiveGuildName(build, hasGuildMembership);
            string guildRoleLabel = GetEffectiveGuildRoleLabel();
            IReadOnlyList<string> rankTitles = _guildRankTitles.ToArray();
            IReadOnlyList<GuildRankingSeedEntry> rankingEntries = BuildGuildRankingSeedEntries(guildName);

            return new GuildDialogContext(
                guildName,
                guildRoleLabel,
                rankTitles,
                _guildNoticeText,
                _guildManageRequiresApproval,
                rankingEntries);
        }

        private IReadOnlyList<GuildRankingSeedEntry> BuildGuildRankingSeedEntries(string guildName)
        {
            IEnumerable<SocialEntryState> guildRoster = _entriesByTab.TryGetValue(SocialListTab.Guild, out List<SocialEntryState> entries)
                ? entries
                : Enumerable.Empty<SocialEntryState>();

            string masterName = guildRoster.FirstOrDefault(entry => entry.IsLeader)?.Name
                ?? guildRoster.FirstOrDefault(entry => entry.IsLocalPlayer)?.Name
                ?? _playerName;
            int onlineCount = guildRoster.Count(entry => entry.IsOnline);
            int totalCount = guildRoster.Count();

            return
            [
                new GuildRankingSeedEntry(
                    string.IsNullOrWhiteSpace(guildName) ? "No Guild" : guildName,
                    string.IsNullOrWhiteSpace(masterName) ? "Guild Master" : masterName,
                    $"Lv. {Math.Max(1, ResolveEffectiveGuildLevel())}",
                    $"{onlineCount}/{Math.Max(onlineCount, totalCount)} online",
                    _guildNoticeText)
            ];
        }
    }
}
