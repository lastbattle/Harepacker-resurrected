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

            int partyId = ResolvePendingGuildCreateAgreementPartyId(acceptance.MasterName, acceptance.GuildName);
            if (!acceptance.Accepted)
            {
                SocialListGuildDialogRequestPacket packet = new(
                    SocialListGuildDialogRequestKind.CreateGuildAgreement,
                    acceptance.GuildName.Trim(),
                    null,
                    partyId,
                    false);
                string dispatchMessage = GuildDialogRequestDispatcher?.Invoke(packet);
                string declineMessage = $"Create guild agreement for {acceptance.GuildName.Trim()} was declined; no guild state or meso balance changed.";
                ClearPendingGuildCreateAgreementContext(acceptance.MasterName, acceptance.GuildName);
                return NotifyGuildDialogSocialChatObserved(
                    string.IsNullOrWhiteSpace(dispatchMessage)
                        ? declineMessage
                        : $"{declineMessage} {dispatchMessage.Trim()}");
            }

            return SubmitPendingGuildDialogRequest(new GuildDialogPendingRequest(
                GuildDialogPendingRequestKind.CreateGuildAgreement,
                "Create guild agreement",
                string.IsNullOrWhiteSpace(acceptance.MasterName) ? _playerName : acceptance.MasterName.Trim(),
                acceptance.GuildName.Trim(),
                null,
                0,
                acceptance.AcceptedAtUtc,
                true,
                partyId,
                _packetGuildUiRevision,
                _packetGuildMarkRevision,
                _packetGuildPointsAndLevelRevision,
                _packetGuildRosterRevision));
        }

        internal string SubmitCreateGuildNameRequest(string guildName)
        {
            if (string.IsNullOrWhiteSpace(guildName))
            {
                return NotifyGuildDialogSocialChatObserved("Create guild request needs a guild name.");
            }

            return SubmitPendingGuildDialogRequest(new GuildDialogPendingRequest(
                GuildDialogPendingRequestKind.CreateGuild,
                "Create guild",
                _playerName,
                guildName.Trim(),
                null,
                DefaultGuildCreateCostMesos,
                DateTimeOffset.UtcNow,
                true,
                0,
                _packetGuildUiRevision,
                _packetGuildMarkRevision,
                _packetGuildPointsAndLevelRevision,
                _packetGuildRosterRevision));
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

        internal string CaptureClientGuildCreateAgreement(int partyId, string masterName, string guildName)
        {
            _pendingGuildCreateAgreementPartyId = Math.Max(0, partyId);
            _pendingGuildCreateAgreementMasterName = string.IsNullOrWhiteSpace(masterName) ? _playerName : masterName.Trim();
            _pendingGuildCreateAgreementGuildName = string.IsNullOrWhiteSpace(guildName) ? "New Guild" : guildName.Trim();
            string summary = $"Client OnGuildResult(3) captured create-guild agreement party={_pendingGuildCreateAgreementPartyId}, master={_pendingGuildCreateAgreementMasterName}, guild={_pendingGuildCreateAgreementGuildName}.";
            _lastPacketSyncSummaryByTab[SocialListTab.Guild] = summary;
            NotifyGuildDialogSocialChatObserved(summary);
            return summary;
        }

        private int ResolvePendingGuildCreateAgreementPartyId(string masterName, string guildName)
        {
            string normalizedMasterName = string.IsNullOrWhiteSpace(masterName) ? _playerName : masterName.Trim();
            string normalizedGuildName = string.IsNullOrWhiteSpace(guildName) ? "New Guild" : guildName.Trim();
            bool matchesCapturedAgreement =
                !string.IsNullOrWhiteSpace(_pendingGuildCreateAgreementGuildName)
                && string.Equals(_pendingGuildCreateAgreementGuildName, normalizedGuildName, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(_pendingGuildCreateAgreementMasterName)
                    || string.Equals(_pendingGuildCreateAgreementMasterName, normalizedMasterName, StringComparison.OrdinalIgnoreCase));
            return matchesCapturedAgreement && _pendingGuildCreateAgreementPartyId > 0
                ? _pendingGuildCreateAgreementPartyId
                : Math.Max(0, _clientPartyId);
        }

        private void ClearPendingGuildCreateAgreementContext(string masterName, string guildName)
        {
            string normalizedMasterName = string.IsNullOrWhiteSpace(masterName) ? _playerName : masterName.Trim();
            string normalizedGuildName = string.IsNullOrWhiteSpace(guildName) ? "New Guild" : guildName.Trim();
            if (!string.Equals(_pendingGuildCreateAgreementGuildName, normalizedGuildName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_pendingGuildCreateAgreementMasterName)
                && !string.Equals(_pendingGuildCreateAgreementMasterName, normalizedMasterName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _pendingGuildCreateAgreementPartyId = 0;
            _pendingGuildCreateAgreementMasterName = null;
            _pendingGuildCreateAgreementGuildName = null;
        }
    }
}
