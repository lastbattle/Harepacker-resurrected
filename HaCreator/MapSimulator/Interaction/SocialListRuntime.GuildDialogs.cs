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

            List<GuildRankingSeedEntry> rankingEntries =
            [
                new GuildRankingSeedEntry(
                    string.IsNullOrWhiteSpace(guildName) ? "No Guild" : guildName,
                    string.IsNullOrWhiteSpace(masterName) ? "Guild Master" : masterName,
                    null,
                    $"Lv. {Math.Max(1, ResolveEffectiveGuildLevel())}",
                    $"{onlineCount}/{Math.Max(onlineCount, totalCount)} online",
                    _guildNoticeText)
            ];

            IReadOnlyList<GuildRankingSeedEntry> packetOwnedRivals = GetPacketGuildRankingEntries(guildName);
            if (packetOwnedRivals.Count > 0)
            {
                rankingEntries.AddRange(packetOwnedRivals);
            }

            return rankingEntries;
        }

        internal string SubmitGuildCreateAgreementAcceptance(GuildCreateAgreementAcceptance acceptance)
        {
            if (string.IsNullOrWhiteSpace(acceptance.GuildName))
            {
                return null;
            }

            return SubmitPendingGuildDialogRequest(new GuildDialogPendingRequest(
                GuildDialogPendingRequestKind.CreateGuild,
                "Create guild",
                string.IsNullOrWhiteSpace(acceptance.MasterName) ? _playerName : acceptance.MasterName.Trim(),
                acceptance.GuildName.Trim(),
                null,
                DefaultGuildCreateCostMesos,
                acceptance.AcceptedAtUtc));
        }

        private string ApplyGuildCreateAgreementAcceptanceCore(string masterName, string guildName)
        {
            if (string.IsNullOrWhiteSpace(guildName))
            {
                return null;
            }

            string acceptedGuildName = guildName.Trim();
            string acceptedMasterName = string.IsNullOrWhiteSpace(masterName)
                ? _playerName
                : masterName.Trim();

            _hasGuildMembership = true;
            _guildName = acceptedGuildName;
            _packetGuildPoints = 0;
            _packetGuildUiState = new PacketGuildUiState(true, acceptedGuildName, Math.Max(1, _packetGuildUiState?.GuildLevel ?? 1));

            UpdateOrInsertLocalEntry(
                SocialListTab.Guild,
                new SocialEntryState(
                    _playerName,
                    "Master",
                    acceptedGuildName,
                    _locationSummary,
                    _channel,
                    true,
                    true,
                    false)
                {
                    IsLocalPlayer = true
                });

            _selectedIndexByTab[SocialListTab.Guild] = Math.Max(0, _entriesByTab[SocialListTab.Guild].FindIndex(entry => entry.IsLocalPlayer));
            _firstVisibleIndexByTab[SocialListTab.Guild] = 0;

            if (_packetOwnedRosterByTab[SocialListTab.Guild])
            {
                _lastPacketSyncSummaryByTab[SocialListTab.Guild] =
                    $"Guild creation agreement accepted for {acceptedGuildName}; local guild row now mirrors the new master-owned guild and seeds guild points={_packetGuildPoints}.";
            }

            return $"{acceptedMasterName} guild agreement now updates the shared guild seam: guild={acceptedGuildName}, role=Master, Guild Lv. {_packetGuildUiState.Value.GuildLevel}.";
        }
    }
}
