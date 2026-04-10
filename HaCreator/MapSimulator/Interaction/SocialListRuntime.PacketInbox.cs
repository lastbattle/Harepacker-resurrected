using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed partial class SocialListRuntime
    {
        internal string ApplyPacketOwnedRosterPayload(SocialListTab tab, byte[] payload)
        {
            if (payload == null)
            {
                return $"Packet-owned {GetHeaderTitle(tab)} payload is missing.";
            }

            return SocialListPacketCodec.TryParseRoster(payload, out SocialListRosterPacket packet, out string error)
                ? ApplyPacketOwnedRosterDelta(tab, packet)
                : error ?? $"Packet-owned {GetHeaderTitle(tab)} payload could not be decoded.";
        }

        internal string ApplyPacketOwnedGuildAuthorityPayload(byte[] payload)
        {
            if (payload == null)
            {
                return "Packet-owned guild authority payload is missing.";
            }

            return SocialListPacketCodec.TryParseGuildAuthority(payload, out SocialListGuildAuthorityPacket packet, out string error)
                ? ApplyPacketOwnedGuildAuthorityDelta(packet)
                : error ?? "Packet-owned guild authority payload could not be decoded.";
        }

        internal string ApplyPacketOwnedAllianceAuthorityPayload(byte[] payload)
        {
            if (payload == null)
            {
                return "Packet-owned alliance authority payload is missing.";
            }

            return SocialListPacketCodec.TryParseAllianceAuthority(payload, out SocialListAllianceAuthorityPacket packet, out string error)
                ? ApplyPacketOwnedAllianceAuthorityDelta(packet)
                : error ?? "Packet-owned alliance authority payload could not be decoded.";
        }

        internal string ApplyPacketOwnedGuildUiPayload(byte[] payload)
        {
            if (payload == null)
            {
                return "Packet-owned guild UI payload is missing.";
            }

            return SocialListPacketCodec.TryParseGuildUi(payload, out SocialListGuildUiPacket packet, out string error)
                ? ApplyPacketOwnedGuildUiDelta(packet)
                : error ?? "Packet-owned guild UI payload could not be decoded.";
        }

        internal string ApplyClientGuildResultPayload(byte[] payload)
        {
            if (payload == null)
            {
                return "Client guild-result payload is missing.";
            }

            return SocialListPacketCodec.TryParseClientGuildResult(payload, out SocialListClientGuildResultPacket packet, out string error)
                ? ApplyClientGuildResultDelta(packet)
                : error ?? "Client guild-result payload could not be decoded.";
        }

        internal string ApplyClientAllianceResultPayload(byte[] payload)
        {
            if (payload == null)
            {
                return "Client alliance-result payload is missing.";
            }

            return SocialListPacketCodec.TryParseClientAllianceResult(payload, out SocialListClientAllianceResultPacket packet, out string error)
                ? ApplyClientAllianceResultDelta(packet)
                : error ?? "Client alliance-result payload could not be decoded.";
        }

        internal string ApplyPacketOwnedRosterDelta(SocialListTab tab, SocialListRosterPacket packet)
        {
            return packet.Kind switch
            {
                SocialListRosterPacketKind.Replace => ApplyPacketOwnedRosterReplace(tab, packet),
                SocialListRosterPacketKind.ReplaceWithIds => ApplyPacketOwnedRosterReplace(tab, packet),
                SocialListRosterPacketKind.Upsert => ApplyPacketOwnedRosterUpsert(tab, packet),
                SocialListRosterPacketKind.UpsertWithId => ApplyPacketOwnedRosterUpsert(tab, packet),
                SocialListRosterPacketKind.Remove => RemovePacketEntry(tab, packet.Name),
                SocialListRosterPacketKind.RemoveById => RemovePacketEntryByMemberId(tab, packet.MemberId),
                SocialListRosterPacketKind.Clear => ApplyPacketOwnedRosterClear(tab, packet.Summary),
                SocialListRosterPacketKind.Select => SelectEntryByName(tab, packet.Name),
                SocialListRosterPacketKind.SelectById => SelectEntryByMemberId(tab, packet.MemberId),
                SocialListRosterPacketKind.Summary => SetPacketSyncSummary(tab, packet.Summary),
                SocialListRosterPacketKind.Resolve => ApplyPacketOwnedRosterResolve(tab, packet),
                SocialListRosterPacketKind.Request => ApplyPacketOwnedRosterRequest(tab, packet),
                _ => $"Unsupported packet-owned social-list roster delta kind {(byte)packet.Kind}."
            };
        }

        internal string ApplyPacketOwnedGuildAuthorityDelta(SocialListGuildAuthorityPacket packet)
        {
            return SetPacketGuildAuthority(
                packet.RoleLabel,
                packet.CanManageRanks,
                packet.CanToggleAdmission,
                packet.CanEditNotice);
        }

        internal string ApplyPacketOwnedAllianceAuthorityDelta(SocialListAllianceAuthorityPacket packet)
        {
            return SetPacketAllianceAuthority(
                packet.RoleLabel,
                packet.CanEditRanks,
                packet.CanEditNotice);
        }

        internal string ApplyPacketOwnedGuildUiDelta(SocialListGuildUiPacket packet)
        {
            return SetPacketGuildUiContext(packet.HasGuildMembership, packet.GuildName, packet.GuildLevel);
        }

        internal string ApplyClientGuildResultDelta(SocialListClientGuildResultPacket packet)
        {
            return packet.Kind switch
            {
                SocialListClientGuildResultKind.GradeChange => ApplyPacketOwnedGradeChange(SocialListTab.Guild, packet.GradeChange, _guildRankTitles, packet.GuildId, "guild"),
                SocialListClientGuildResultKind.Ranking => SetPacketGuildRankingEntries(packet.RankingEntries, packet.GuildId),
                SocialListClientGuildResultKind.RankTitles => SetPacketGuildRankTitles(packet.RankTitles, packet.GuildId),
                SocialListClientGuildResultKind.Notice => SetPacketGuildNoticeText(packet.Notice, packet.GuildId),
                SocialListClientGuildResultKind.Mark when packet.MarkSelection.HasValue => SetPacketGuildMarkSelection(packet.MarkSelection.Value, packet.GuildId),
                SocialListClientGuildResultKind.PointsAndLevel => SetPacketGuildPointsAndLevel(packet.GuildPoints, packet.GuildLevel, packet.GuildId),
                SocialListClientGuildResultKind.SkillRecord when packet.GuildSkillRecord.HasValue =>
                    $"Client OnGuildResult({(byte)SocialListClientGuildResultKind.SkillRecord}) decoded guild-skill record {packet.GuildSkillRecord.Value.SkillId} for guild {packet.GuildId}.",
                _ => $"Unsupported client guild-result subtype {(byte)packet.Kind}."
            };
        }

        internal string ApplyClientAllianceResultDelta(SocialListClientAllianceResultPacket packet)
        {
            return packet.Kind switch
            {
                SocialListClientAllianceResultKind.GradeChange => ApplyPacketOwnedGradeChange(SocialListTab.Alliance, packet.GradeChange, _allianceRankTitles, packet.AllianceId, "alliance"),
                SocialListClientAllianceResultKind.RankTitles => SetPacketAllianceRankTitles(packet.RankTitles, packet.AllianceId),
                SocialListClientAllianceResultKind.Notice => SetPacketAllianceNoticeText(packet.Notice, packet.AllianceId),
                _ => $"Unsupported client alliance-result subtype {(byte)packet.Kind}."
            };
        }

        private string ApplyPacketOwnedRosterReplace(SocialListTab tab, SocialListRosterPacket packet)
        {
            List<SocialEntryState> entries = _entriesByTab[tab];
            SocialEntryState localState = ResolveLocalEntryState(tab);
            entries.Clear();

            for (int i = 0; i < packet.Entries.Count; i++)
            {
                SocialListPacketEntry packetEntry = packet.Entries[i];
                SocialEntryState nextEntry = new(
                    packetEntry.Name,
                    packetEntry.PrimaryText,
                    packetEntry.SecondaryText,
                    packetEntry.LocationSummary,
                    packetEntry.Channel,
                    packetEntry.IsOnline,
                    packetEntry.IsLeader,
                    packetEntry.IsBlocked)
                {
                    MemberId = packetEntry.MemberId,
                    IsLocalPlayer = packetEntry.IsLocalPlayer
                };

                if (nextEntry.IsLocalPlayer)
                {
                    nextEntry = MergePacketOwnedLocalEntry(nextEntry, localState);
                }

                entries.Add(nextEntry);
            }

            _packetOwnedRosterByTab[tab] = true;
            _lastPendingRequestByTab[tab] = null;
            _lastPacketSyncSummaryByTab[tab] = string.IsNullOrWhiteSpace(packet.Summary)
                ? $"Packet replace synchronized {entries.Count} roster entr{(entries.Count == 1 ? "y" : "ies")}."
                : packet.Summary.Trim();

            SyncPacketGuildUiStateFromRoster(tab);
            ResetSelectionAfterMutation(tab);
            return $"{GetHeaderTitle(tab)} roster replaced from packet-owned sync ({entries.Count} entr{(entries.Count == 1 ? "y" : "ies")}).";
        }

        private string ApplyPacketOwnedRosterUpsert(SocialListTab tab, SocialListRosterPacket packet)
        {
            if (packet.Entries.Count <= 0)
            {
                return $"Packet upsert on {GetHeaderTitle(tab)} did not include any roster entries.";
            }

            SocialListPacketEntry entry = packet.Entries[0];
            return UpsertPacketEntry(
                tab,
                entry.Name,
                entry.PrimaryText,
                entry.SecondaryText,
                entry.LocationSummary,
                entry.Channel,
                entry.IsOnline,
                entry.IsLeader,
                entry.IsBlocked,
                entry.IsLocalPlayer,
                entry.MemberId);
        }

        private string RemovePacketEntryByMemberId(SocialListTab tab, int? memberId)
        {
            if (!memberId.HasValue || memberId.Value <= 0)
            {
                return $"Provide a positive {GetHeaderTitle(tab)} member id to remove.";
            }

            int removed = _entriesByTab[tab].RemoveAll(entry => entry.MemberId == memberId.Value);
            _packetOwnedRosterByTab[tab] = true;
            _lastPendingRequestByTab[tab] = null;
            _lastPacketSyncSummaryByTab[tab] = removed > 0
                ? $"Packet member-id remove deleted {removed} entr{(removed == 1 ? "y" : "ies")} for member #{memberId.Value}."
                : $"Packet member-id remove for #{memberId.Value} did not match any current roster row.";
            SyncPacketGuildUiStateFromRoster(tab);
            ResetSelectionAfterMutation(tab);
            return removed > 0
                ? $"Member #{memberId.Value} was removed from the packet-owned {GetHeaderTitle(tab)} roster."
                : $"Member #{memberId.Value} was not present in the current {GetHeaderTitle(tab)} roster.";
        }

        private string SelectEntryByMemberId(SocialListTab tab, int? memberId)
        {
            if (!memberId.HasValue || memberId.Value <= 0)
            {
                return $"Provide a positive {GetHeaderTitle(tab)} member id to select.";
            }

            IReadOnlyList<SocialEntryState> entries = GetFilteredEntries(tab);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].MemberId != memberId.Value)
                {
                    continue;
                }

                _selectedIndexByTab[tab] = i;
                EnsureSelectionVisible(tab, entries.Count);
                return $"Member #{memberId.Value} ({entries[i].Name}) is now selected on the {GetHeaderTitle(tab)} roster.";
            }

            return $"Member #{memberId.Value} is not present in the current {GetHeaderTitle(tab)} roster.";
        }

        private string ApplyPacketOwnedGradeChange(
            SocialListTab tab,
            SocialListGradeChangePacket gradeChange,
            IReadOnlyList<string> rankTitles,
            int ownerId,
            string ownerLabel)
        {
            if (gradeChange.MemberId <= 0)
            {
                return $"Client {ownerLabel}-result grade-change member id must be positive.";
            }

            if (!gradeChange.AbsoluteGrade.HasValue)
            {
                return $"Client {ownerLabel}-result grade-change payload did not include an absolute grade.";
            }

            int absoluteGrade = gradeChange.AbsoluteGrade.Value;
            int rankIndex = absoluteGrade - 1;
            if (rankTitles == null || rankTitles.Count == 0 || rankIndex < 0 || rankIndex >= rankTitles.Count)
            {
                return $"Client {ownerLabel}-result grade {absoluteGrade} is outside the current rank-title range.";
            }

            List<SocialEntryState> entries = _entriesByTab[tab];
            EnsureRosterMemberIds(tab);
            int entryIndex = entries.FindIndex(entry => entry.MemberId == gradeChange.MemberId);
            if (entryIndex < 0)
            {
                _packetOwnedRosterByTab[tab] = true;
                _lastPacketSyncSummaryByTab[tab] =
                    $"Client {ownerLabel}-result grade change for member #{gradeChange.MemberId} could not be mapped onto the packet-owned roster.";
                return $"Client {ownerLabel}-result grade change for member #{gradeChange.MemberId} did not match any {GetHeaderTitle(tab)} roster row.";
            }

            SocialEntryState entry = entries[entryIndex];
            string nextTitle = rankTitles[rankIndex];
            entries[entryIndex] = new SocialEntryState(
                entry.Name,
                nextTitle,
                entry.SecondaryText,
                entry.LocationSummary,
                entry.Channel,
                entry.IsOnline,
                rankIndex == 0,
                entry.IsBlocked)
            {
                MemberId = entry.MemberId,
                IsLocalPlayer = entry.IsLocalPlayer
            };

            _packetOwnedRosterByTab[tab] = true;
            _lastPendingRequestByTab[tab] = null;
            _lastPacketSyncSummaryByTab[tab] =
                $"Client {ownerLabel}-result grade change set {entry.Name} (#{gradeChange.MemberId}) to grade {absoluteGrade} ({nextTitle})"
                + (ownerId > 0 ? $" for {ownerLabel} {ownerId}." : ".");
            ResetSelectionAfterMutation(tab);
            NotifySocialChatObserved($"{entry.Name}'s {ownerLabel} grade changed to {nextTitle}.");
            return _lastPacketSyncSummaryByTab[tab];
        }

        private string ApplyPacketOwnedRosterClear(SocialListTab tab, string summary)
        {
            string message = ClearPacketRoster(tab);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                _lastPacketSyncSummaryByTab[tab] = summary.Trim();
                message = $"{message} Summary: {_lastPacketSyncSummaryByTab[tab]}";
            }

            return message;
        }

        private string ApplyPacketOwnedRosterResolve(SocialListTab tab, SocialListRosterPacket packet)
        {
            if (string.IsNullOrWhiteSpace(_lastPendingRequestByTab.TryGetValue(tab, out string pending) ? pending : null)
                && !string.IsNullOrWhiteSpace(packet.RequestKind))
            {
                _lastPendingRequestByTab[tab] = packet.RequestKind.Trim();
            }

            return ResolvePacketOwnedRequest(tab, packet.Approved, packet.Summary);
        }

        private string ApplyPacketOwnedRosterRequest(SocialListTab tab, SocialListRosterPacket packet)
        {
            _packetOwnedRosterByTab[tab] = true;
            string requestKind = string.IsNullOrWhiteSpace(packet.RequestKind) ? "Roster update" : packet.RequestKind.Trim();
            _lastPendingRequestByTab[tab] = requestKind;
            if (!string.IsNullOrWhiteSpace(packet.Summary))
            {
                _lastPacketSyncSummaryByTab[tab] = packet.Summary.Trim();
            }

            return string.IsNullOrWhiteSpace(packet.Summary)
                ? $"{GetHeaderTitle(tab)} packet-owned request staged: {requestKind}."
                : $"{GetHeaderTitle(tab)} packet-owned request staged: {requestKind}. {packet.Summary.Trim()}";
        }

        private SocialEntryState ResolveLocalEntryState(SocialListTab tab)
        {
            if (!_entriesByTab.TryGetValue(tab, out List<SocialEntryState> entries) || entries == null)
            {
                return null;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                SocialEntryState entry = entries[i];
                if (entry != null && entry.IsLocalPlayer)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
