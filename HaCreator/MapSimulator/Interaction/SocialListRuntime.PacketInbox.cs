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

        internal string ApplyClientFriendResultPayload(byte[] payload)
        {
            if (payload == null)
            {
                return "Client friend-result payload is missing.";
            }

            return SocialListPacketCodec.TryParseClientFriendResult(payload, out SocialListClientFriendResultPacket packet, out string error)
                ? ApplyClientFriendResultDelta(packet)
                : error ?? "Client friend-result payload could not be decoded.";
        }

        internal string ApplyClientPartyResultPayload(byte[] payload)
        {
            if (payload == null)
            {
                return "Client party-result payload is missing.";
            }

            return SocialListPacketCodec.TryParseClientPartyResult(payload, out SocialListClientPartyResultPacket packet, out string error)
                ? ApplyClientPartyResultDelta(packet)
                : error ?? "Client party-result payload could not be decoded.";
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
                SocialListClientGuildResultKind.GuildQuestNotEnoughMembers => ApplyClientGuildQuestDirectNoticeResult(packet),
                SocialListClientGuildResultKind.GuildQuestRegistrantDisconnected => ApplyClientGuildQuestDirectNoticeResult(packet),
                SocialListClientGuildResultKind.GuildQuestQueueNotice => ApplyClientGuildQuestQueueNoticeResult(packet),
                SocialListClientGuildResultKind.GuildBoardAuthKey => SetPacketGuildBoardAuthKey(packet.GuildBoardAuthKey),
                SocialListClientGuildResultKind.GuildNameInput
                    or SocialListClientGuildResultKind.CreateGuildAgreement
                    or SocialListClientGuildResultKind.GuildInvite
                    or SocialListClientGuildResultKind.GuildMarkInput => SetPacketSyncSummary(
                    SocialListTab.Guild,
                    BuildClientGuildExplicitBranchSummary(packet)),
                SocialListClientGuildResultKind.GuildDataSnapshot => ApplyClientGuildDataSnapshot(packet),
                SocialListClientGuildResultKind.SkillRecord when packet.GuildSkillRecord.HasValue =>
                    BuildClientGuildSkillRecordSummary(packet),
                SocialListClientGuildResultKind.ResultNotice => SetPacketSyncSummary(
                    SocialListTab.Guild,
                    BuildClientGuildResultNoticeSummary(packet)),
                SocialListClientGuildResultKind.Notice35
                    or SocialListClientGuildResultKind.Notice37
                    or SocialListClientGuildResultKind.Notice42
                    or SocialListClientGuildResultKind.Notice43
                    or SocialListClientGuildResultKind.Notice44
                    or SocialListClientGuildResultKind.Notice47
                    or SocialListClientGuildResultKind.Notice50
                    or SocialListClientGuildResultKind.Notice55
                    or SocialListClientGuildResultKind.Notice56
                    or SocialListClientGuildResultKind.Notice57
                    or SocialListClientGuildResultKind.Notice58 => SetPacketSyncSummary(
                    SocialListTab.Guild,
                    BuildClientGuildDirectNoticeSummary(packet)),
                _ => SetPacketSyncSummary(
                    SocialListTab.Guild,
                    BuildClientGuildResultFallbackNoticeSummary(packet))
            };
        }

        private string ApplyClientGuildDataSnapshot(SocialListClientGuildResultPacket packet)
        {
            if (ShouldIgnoreGuildScopedResult(packet.GuildId, out int activeGuildId))
            {
                return $"Ignored client OnGuildResult({packet.RawSubtype}) for guild {packet.GuildId} because the active packet-owned guild context is {activeGuildId}.";
            }

            RememberPacketGuildId(packet.GuildId);
            string resolvedGuildName = string.IsNullOrWhiteSpace(packet.GuildName)
                ? ResolveEffectiveGuildName(null, hasGuildMembership: true)
                : packet.GuildName.Trim();
            SetPacketGuildUiContext(
                hasGuildMembership: true,
                resolvedGuildName,
                packet.GuildLevel);
            SetPacketGuildPointsAndLevel(packet.GuildPoints, packet.GuildLevel, packet.GuildId);
            SetPacketGuildRankTitles(packet.RankTitles, packet.GuildId);
            ApplyClientGuildDataSnapshotMembers(packet.GuildMembers, packet.RankTitles, resolvedGuildName, packet.GuildId);

            int skillRecordCount = packet.GuildSkillRecords?.Count ?? 0;
            int memberCount = packet.GuildMembers?.Count ?? 0;
            string summary = $"Client OnGuildResult({packet.RawSubtype}) decoded guild snapshot for {resolvedGuildName} (id={packet.GuildId}, level={Math.Max(0, packet.GuildLevel)}, points={Math.Max(0, packet.GuildPoints)}, members={memberCount}, skillRecords={skillRecordCount}).";
            return SetPacketSyncSummary(SocialListTab.Guild, summary);
        }

        private void ApplyClientGuildDataSnapshotMembers(
            IReadOnlyList<SocialListClientGuildMemberEntry> members,
            IReadOnlyList<string> rankTitles,
            string guildName,
            int guildId)
        {
            List<SocialEntryState> entries = _entriesByTab[SocialListTab.Guild];
            entries.Clear();
            if (members != null)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    SocialListClientGuildMemberEntry member = members[i];
                    if (string.IsNullOrWhiteSpace(member.Name))
                    {
                        continue;
                    }

                    entries.Add(CreateClientGuildSnapshotEntry(member, rankTitles, guildName));
                }
            }

            _packetOwnedRosterByTab[SocialListTab.Guild] = true;
            _lastPendingRequestByTab[SocialListTab.Guild] = null;
            _packetGuildRosterRevision = AdvanceGuildDialogRevision(_packetGuildRosterRevision);
            RememberPacketGuildId(guildId);
            ResetSelectionAfterMutation(SocialListTab.Guild);
            TryFinalizePendingGuildDialogRequestFromPacket();
        }

        private SocialEntryState CreateClientGuildSnapshotEntry(
            SocialListClientGuildMemberEntry member,
            IReadOnlyList<string> rankTitles,
            string guildName)
        {
            int rankIndex = Math.Clamp(member.Grade - 1, 0, Math.Max(0, (rankTitles?.Count ?? 1) - 1));
            string roleLabel = rankTitles != null && rankIndex < rankTitles.Count && !string.IsNullOrWhiteSpace(rankTitles[rankIndex])
                ? rankTitles[rankIndex].Trim()
                : $"Rank {Math.Max(1, member.Grade)}";
            bool isLocal = (_playerCharacterId > 0 && member.MemberId == _playerCharacterId)
                           || string.Equals(member.Name, _playerName, StringComparison.OrdinalIgnoreCase);
            string secondary = string.IsNullOrWhiteSpace(guildName) ? ResolveEffectiveGuildName(null, hasGuildMembership: true) : guildName.Trim();
            return new SocialEntryState(
                member.Name.Trim(),
                roleLabel,
                secondary,
                member.IsOnline ? $"Job {member.JobId}, Lv. {member.Level}" : "Offline",
                member.IsOnline ? _channel : 0,
                member.IsOnline,
                rankIndex == 0,
                isBlocked: false)
            {
                MemberId = member.MemberId > 0 ? member.MemberId : null,
                IsLocalPlayer = isLocal
            };
        }

        private string BuildClientGuildSkillRecordSummary(SocialListClientGuildResultPacket packet)
        {
            if (ShouldIgnoreGuildScopedResult(packet.GuildId, out int activeGuildId))
            {
                return $"Ignored client OnGuildResult({(byte)SocialListClientGuildResultKind.SkillRecord}) for guild {packet.GuildId} because the active packet-owned guild context is {activeGuildId}.";
            }

            RememberPacketGuildId(packet.GuildId);
            return $"Client OnGuildResult({(byte)SocialListClientGuildResultKind.SkillRecord}) decoded guild-skill record {packet.GuildSkillRecord!.Value.SkillId} for guild {packet.GuildId}.";
        }

        private static string BuildClientGuildResultNoticeSummary(SocialListClientGuildResultPacket packet)
        {
            string notice = packet.HasExplicitNotice
                ? packet.ResultNotice?.Trim() ?? string.Empty
                : SocialListGuildResultClientText.GetSharedResultNoticeFallback();
            string noticeSource = packet.HasExplicitNotice
                ? "explicit notice"
                : $"shared StringPool 0x{SocialListGuildResultClientText.SharedResultNoticeStringPoolId:X} notice";

            return $"Client OnGuildResult({(byte)SocialListClientGuildResultKind.ResultNotice}) reported {noticeSource}: {notice}.";
        }

        private static string BuildClientGuildQuestDirectNoticeSummary(SocialListClientGuildResultPacket packet)
        {
            string notice = packet.Kind == SocialListClientGuildResultKind.GuildQuestRegistrantDisconnected
                ? SocialListGuildResultClientText.GetGuildQuestRegistrantDisconnectedNotice()
                : SocialListGuildResultClientText.GetGuildQuestNotEnoughMembersNotice();
            return $"Client OnGuildResult({(byte)packet.Kind}) reported guild-quest notice: {notice}";
        }

        private static string BuildClientGuildQuestQueueNoticeSummary(SocialListClientGuildResultPacket packet)
        {
            string notice = SocialListGuildResultClientText.FormatGuildQuestQueueNotice(packet.GuildQuestChannel, packet.GuildQuestWaitStatus);
            return packet.GuildQuestWaitStatus <= 0
                ? $"Client OnGuildResult({(byte)SocialListClientGuildResultKind.GuildQuestQueueNotice}) cleared the guild-quest queue temporary notice."
                : $"Client OnGuildResult({(byte)SocialListClientGuildResultKind.GuildQuestQueueNotice}) reported guild-quest queue notice: {notice}";
        }

        private string ApplyClientGuildQuestDirectNoticeResult(SocialListClientGuildResultPacket packet)
        {
            if (TryBuildNoGuildContextOwnedResultIgnore(
                    packet.RawSubtype,
                    "guild-quest notice",
                    out string ignoredMessage))
            {
                return ignoredMessage;
            }

            return SetPacketSyncSummary(
                SocialListTab.Guild,
                BuildClientGuildQuestDirectNoticeSummary(packet));
        }

        private string ApplyClientGuildQuestQueueNoticeResult(SocialListClientGuildResultPacket packet)
        {
            if (TryBuildNoGuildContextOwnedResultIgnore(
                    packet.RawSubtype,
                    "guild-quest queue notice",
                    out string ignoredMessage))
            {
                return ignoredMessage;
            }

            return SetPacketSyncSummary(
                SocialListTab.Guild,
                BuildClientGuildQuestQueueNoticeSummary(packet));
        }

        private static string BuildClientGuildResultFallbackNoticeSummary(SocialListClientGuildResultPacket packet)
        {
            if (!packet.UsesSharedResultNoticeFallback)
            {
                return $"Client OnGuildResult({packet.RawSubtype}) follows an explicit client branch that is not yet modeled by the packet summary seam.";
            }

            string notice = SocialListGuildResultClientText.GetSharedResultNoticeFallback();
            return $"Client OnGuildResult({(byte)packet.Kind}) fell back to shared StringPool 0x{SocialListGuildResultClientText.SharedResultNoticeStringPoolId:X} notice: {notice}.";
        }

        private static string BuildClientGuildDirectNoticeSummary(SocialListClientGuildResultPacket packet)
        {
            string notice = string.IsNullOrWhiteSpace(packet.DirectNotice)
                ? $"Client OnGuildResult({packet.RawSubtype}) notice."
                : packet.DirectNotice.Trim();
            return $"Client OnGuildResult({packet.RawSubtype}) reported explicit notice: {notice}";
        }

        private static string BuildClientGuildExplicitBranchSummary(SocialListClientGuildResultPacket packet)
        {
            return string.IsNullOrWhiteSpace(packet.ExplicitBranchSummary)
                ? $"Client OnGuildResult({packet.RawSubtype}) followed an explicit client branch."
                : packet.ExplicitBranchSummary.Trim();
        }

        internal string ApplyClientFriendResultDelta(SocialListClientFriendResultPacket packet)
        {
            switch (packet.Kind)
            {
                case SocialListClientFriendResultKind.Reset:
                case SocialListClientFriendResultKind.Refresh:
                    return AppendAutoResolvedPacketOwnedRequest(
                        ApplyClientFriendListReplace(SocialListTab.Friend, packet.Entries, packet.Kind),
                        TryAutoResolvePacketOwnedRequest(
                            SocialListTab.Friend,
                            approved: true,
                            packet.Kind,
                            packet.Summary,
                            "Friend"));

                case SocialListClientFriendResultKind.ResetBlocked:
                    return AppendAutoResolvedPacketOwnedRequest(
                        ApplyClientFriendListReplace(SocialListTab.Blacklist, packet.Entries, packet.Kind),
                        TryAutoResolvePacketOwnedRequest(
                            SocialListTab.Blacklist,
                            approved: true,
                            packet.Kind,
                            packet.Summary,
                            "Blacklist"));

                case SocialListClientFriendResultKind.Update:
                case SocialListClientFriendResultKind.Insert:
                    return packet.Entry.HasValue
                        ? AppendAutoResolvedPacketOwnedRequest(
                            UpsertClientFriendEntry(SocialListTab.Friend, packet.Entry.Value, packet.Kind, packet.Summary),
                            TryAutoResolvePacketOwnedRequest(
                                SocialListTab.Friend,
                                approved: true,
                                packet.Kind,
                                packet.Summary,
                                "Friend"))
                        : $"Client OnFriendResult({(byte)packet.Kind}) did not include a friend entry.";

                case SocialListClientFriendResultKind.Channel:
                    return AppendAutoResolvedPacketOwnedRequest(
                        ApplyClientFriendChannelUpdate(packet),
                        TryAutoResolvePacketOwnedRequest(
                            SocialListTab.Friend,
                            approved: true,
                            packet.Kind,
                            packet.Summary,
                            "Friend"));

                case SocialListClientFriendResultKind.Capacity:
                    return SetPacketSyncSummary(SocialListTab.Friend, $"Client OnFriendResult({(byte)packet.Kind}) updated friend capacity to {packet.FriendMax}.");

                case SocialListClientFriendResultKind.NoticeInviteBlocked:
                case SocialListClientFriendResultKind.NoticeTargetFull:
                case SocialListClientFriendResultKind.NoticeTargetUnknown:
                case SocialListClientFriendResultKind.NoticeSelf:
                case SocialListClientFriendResultKind.NoticeDuplicate:
                case SocialListClientFriendResultKind.NoticeFailure:
                case SocialListClientFriendResultKind.NoticeFailureWithMessage:
                case SocialListClientFriendResultKind.NoticeBlocked:
                case SocialListClientFriendResultKind.NoticeRequestDenied:
                case SocialListClientFriendResultKind.NoticeCapacityExpanded:
                    return AppendAutoResolvedPacketOwnedRequest(
                        SetPacketSyncSummary(
                            SocialListTab.Friend,
                            string.IsNullOrWhiteSpace(packet.Summary)
                                ? $"Client OnFriendResult({(byte)packet.Kind}) reported {packet.Kind}."
                                : $"Client OnFriendResult({(byte)packet.Kind}) reported {packet.Summary.Trim()}."),
                        TryAutoResolvePacketOwnedRequest(
                            SocialListTab.Friend,
                            approved: false,
                            packet.Kind,
                            packet.Summary,
                            "Friend"));

                default:
                    return $"Unsupported client friend-result subtype {(byte)packet.Kind}.";
            }
        }

        internal string ApplyClientPartyResultDelta(SocialListClientPartyResultPacket packet)
        {
            switch (packet.Kind)
            {
                case SocialListClientPartyResultKind.Load:
                case SocialListClientPartyResultKind.Refresh:
                    return AppendAutoResolvedPacketOwnedRequest(
                        ApplyClientPartyListReplace(packet),
                        TryAutoResolvePacketOwnedRequest(
                            SocialListTab.Party,
                            approved: true,
                            packet.Kind,
                            packet.Summary,
                            "Party"));

                case SocialListClientPartyResultKind.Join:
                    if (packet.Entries.Count > 0)
                    {
                        return AppendAutoResolvedPacketOwnedRequest(
                            ApplyClientPartyListReplace(packet),
                            TryAutoResolvePacketOwnedRequest(
                                SocialListTab.Party,
                                approved: true,
                                packet.Kind,
                                packet.Summary,
                                "Party"));
                    }

                    return SetPacketSyncSummary(
                        SocialListTab.Party,
                        string.IsNullOrWhiteSpace(packet.ActorName)
                            ? $"Client OnPartyResult({(byte)packet.Kind}) reported a party join result for party {packet.PartyId}."
                            : $"Client OnPartyResult({(byte)packet.Kind}) reported {packet.ActorName} joining party {packet.PartyId}.");

                case SocialListClientPartyResultKind.Create:
                    return AppendAutoResolvedPacketOwnedRequest(
                        ApplyClientPartyCreate(packet),
                        TryAutoResolvePacketOwnedRequest(
                            SocialListTab.Party,
                            approved: true,
                            packet.Kind,
                            packet.Summary,
                            "Party"));

                case SocialListClientPartyResultKind.LeaderChange:
                    return AppendAutoResolvedPacketOwnedRequest(
                        ApplyClientPartyLeaderChange(packet),
                        TryAutoResolvePacketOwnedRequest(
                            SocialListTab.Party,
                            approved: true,
                            packet.Kind,
                            packet.Summary,
                            "Party"));

                case SocialListClientPartyResultKind.MemberJobLevel:
                    return AppendAutoResolvedPacketOwnedRequest(
                        ApplyClientPartyJobLevelChange(packet),
                        TryAutoResolvePacketOwnedRequest(
                            SocialListTab.Party,
                            approved: true,
                            packet.Kind,
                            packet.Summary,
                            "Party"));

                case SocialListClientPartyResultKind.Notice9:
                case SocialListClientPartyResultKind.Notice10:
                case SocialListClientPartyResultKind.Notice16:
                case SocialListClientPartyResultKind.Notice17:
                case SocialListClientPartyResultKind.Notice18:
                case SocialListClientPartyResultKind.NoOp19:
                case SocialListClientPartyResultKind.NoticeNamed:
                case SocialListClientPartyResultKind.Notice29:
                case SocialListClientPartyResultKind.Notice32:
                case SocialListClientPartyResultKind.Notice33:
                case SocialListClientPartyResultKind.Notice34:
                case SocialListClientPartyResultKind.Notice36:
                case SocialListClientPartyResultKind.Notice37:
                case SocialListClientPartyResultKind.PqRewardSelectSuccess:
                case SocialListClientPartyResultKind.PqRewardSelectFail:
                case SocialListClientPartyResultKind.PqRewardReceive:
                case SocialListClientPartyResultKind.PqRewardRequestFail:
                case SocialListClientPartyResultKind.Notice44:
                case SocialListClientPartyResultKind.NoticeOptionalMessage:
                case SocialListClientPartyResultKind.TownPortal:
                    return SetPacketSyncSummary(
                        SocialListTab.Party,
                        string.IsNullOrWhiteSpace(packet.Summary)
                            ? $"Client OnPartyResult({(byte)packet.Kind}) reported {packet.Kind}."
                            : packet.Summary.Trim());

                case SocialListClientPartyResultKind.Invite:
                case SocialListClientPartyResultKind.SearchPacket:
                case SocialListClientPartyResultKind.SearchPacket2:
                case SocialListClientPartyResultKind.SearchPacket3:
                case SocialListClientPartyResultKind.SearchApply:
                case SocialListClientPartyResultKind.SearchPacket4:
                case SocialListClientPartyResultKind.SearchPacket5:
                    return SetPacketSyncSummary(
                        SocialListTab.Party,
                        string.IsNullOrWhiteSpace(packet.Summary)
                            ? $"Client OnPartyResult({(byte)packet.Kind}) reported {packet.Kind}."
                            : packet.Summary.Trim());

                default:
                    return $"Unsupported client party-result subtype {(byte)packet.Kind}.";
            }
        }

        private string ApplyClientPartyCreate(SocialListClientPartyResultPacket packet)
        {
            _clientPartyId = Math.Max(0, packet.PartyId);

            List<SocialEntryState> entries = _entriesByTab[SocialListTab.Party];
            SocialEntryState localState = ResolveLocalEntryState(SocialListTab.Party);
            int localLevel = 1;
            if (localState != null
                && !string.IsNullOrWhiteSpace(localState.SecondaryText)
                && localState.SecondaryText.StartsWith("Lv.", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(localState.SecondaryText.Replace("Lv.", string.Empty, StringComparison.OrdinalIgnoreCase).Trim(), out int parsedLevel))
            {
                localLevel = Math.Max(1, parsedLevel);
            }

            SocialEntryState seededLeader = new(
                localState?.Name ?? _playerName,
                "Leader",
                $"Lv. {localLevel}",
                localState?.LocationSummary ?? _locationSummary,
                localState?.Channel ?? _channel,
                isOnline: true,
                isLeader: true,
                isBlocked: false)
            {
                MemberId = localState?.MemberId,
                IsLocalPlayer = true
            };

            entries.Clear();
            entries.Add(EnsureEntryHasMemberId(SocialListTab.Party, seededLeader));

            _packetOwnedRosterByTab[SocialListTab.Party] = true;
            _lastPendingRequestByTab[SocialListTab.Party] = null;
            _lastPacketSyncSummaryByTab[SocialListTab.Party] =
                $"Client OnPartyResult({(byte)packet.Kind}) created party {packet.PartyId} and seeded the local leader row.";
            ResetSelectionAfterMutation(SocialListTab.Party);
            return _lastPacketSyncSummaryByTab[SocialListTab.Party];
        }

        private string TryAutoResolvePacketOwnedRequest(
            SocialListTab tab,
            bool approved,
            Enum resultKind,
            string resultSummary,
            params string[] pendingRequestPrefixes)
        {
            if (!_lastPendingRequestByTab.TryGetValue(tab, out string pendingRequest)
                || string.IsNullOrWhiteSpace(pendingRequest))
            {
                return null;
            }

            if (pendingRequestPrefixes is { Length: > 0 }
                && Array.TrueForAll(
                    pendingRequestPrefixes,
                    prefix => !pendingRequest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            string trimmedSummary = string.IsNullOrWhiteSpace(resultSummary)
                ? $"Client {GetHeaderTitle(tab)} result {resultKind} {(approved ? "approved" : "rejected")} staged {pendingRequest.ToLowerInvariant()}."
                : $"Client {GetHeaderTitle(tab)} result {resultKind}: {resultSummary.Trim()}";
            return ResolvePacketOwnedRequest(tab, approved, trimmedSummary);
        }

        private static string AppendAutoResolvedPacketOwnedRequest(string primary, string resolution)
        {
            if (string.IsNullOrWhiteSpace(primary))
            {
                return resolution;
            }

            if (string.IsNullOrWhiteSpace(resolution))
            {
                return primary;
            }

            return $"{primary} {resolution}";
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

        private string ApplyClientFriendListReplace(
            SocialListTab tab,
            IReadOnlyList<SocialListClientFriendEntry> clientEntries,
            SocialListClientFriendResultKind kind)
        {
            List<SocialEntryState> entries = _entriesByTab[tab];
            SocialEntryState localState = tab == SocialListTab.Friend ? ResolveLocalEntryState(tab) : null;
            entries.Clear();

            if (localState != null)
            {
                entries.Add(localState);
            }

            for (int i = 0; i < clientEntries.Count; i++)
            {
                SocialListClientFriendEntry clientEntry = clientEntries[i];
                SocialEntryState entry = CreateFriendEntry(tab, clientEntry);
                if (tab == SocialListTab.Friend && !string.IsNullOrWhiteSpace(clientEntry.GroupName))
                {
                    _friendGroupByName[entry.Name] = clientEntry.GroupName.Trim();
                }

                entries.Add(entry);
            }

            _packetOwnedRosterByTab[tab] = true;
            _lastPendingRequestByTab[tab] = null;
            _lastPacketSyncSummaryByTab[tab] =
                $"Client OnFriendResult({(byte)kind}) synchronized {clientEntries.Count} {GetHeaderTitle(tab).ToLowerInvariant()} entr{(clientEntries.Count == 1 ? "y" : "ies")}.";
            ResetSelectionAfterMutation(tab);
            return _lastPacketSyncSummaryByTab[tab];
        }

        private string UpsertClientFriendEntry(
            SocialListTab tab,
            SocialListClientFriendEntry clientEntry,
            SocialListClientFriendResultKind kind,
            string summary)
        {
            if (!string.IsNullOrWhiteSpace(clientEntry.GroupName))
            {
                _friendGroupByName[clientEntry.Name] = clientEntry.GroupName.Trim();
            }

            string detail = UpsertPacketEntry(
                tab,
                clientEntry.Name,
                ResolveFriendPrimaryText(tab, clientEntry),
                ResolveFriendSecondaryText(clientEntry),
                ResolveFriendLocationText(clientEntry),
                ResolveClientChannel(clientEntry.ChannelId),
                IsClientFriendOnline(clientEntry),
                isLeader: false,
                tab == SocialListTab.Blacklist,
                isLocalPlayer: false,
                clientEntry.FriendId);
            _lastPacketSyncSummaryByTab[tab] = string.IsNullOrWhiteSpace(summary)
                ? $"Client OnFriendResult({(byte)kind}) upserted {clientEntry.Name}."
                : $"Client OnFriendResult({(byte)kind}) upserted {clientEntry.Name}; {summary.Trim()}.";
            return $"{_lastPacketSyncSummaryByTab[tab]} {detail}";
        }

        private string ApplyClientFriendChannelUpdate(SocialListClientFriendResultPacket packet)
        {
            if (packet.FriendId <= 0)
            {
                return "Client OnFriendResult channel update did not include a positive friend id.";
            }

            List<SocialEntryState> entries = _entriesByTab[SocialListTab.Friend];
            int index = entries.FindIndex(entry => entry.MemberId == packet.FriendId);
            if (index < 0)
            {
                _packetOwnedRosterByTab[SocialListTab.Friend] = true;
                _lastPacketSyncSummaryByTab[SocialListTab.Friend] =
                    $"Client OnFriendResult({(byte)packet.Kind}) channel update for friend #{packet.FriendId} did not match the packet-owned roster.";
                return _lastPacketSyncSummaryByTab[SocialListTab.Friend];
            }

            SocialEntryState entry = entries[index];
            bool isOnline = packet.ChannelId > 0 || string.Equals(packet.Summary, "Cash Shop", StringComparison.OrdinalIgnoreCase);
            string location = string.Equals(packet.Summary, "Cash Shop", StringComparison.OrdinalIgnoreCase)
                ? "Cash Shop"
                : packet.ChannelId > 0
                    ? $"Channel {packet.ChannelId}"
                    : "Offline";
            entries[index] = new SocialEntryState(
                entry.Name,
                entry.PrimaryText,
                isOnline ? $"Ch. {Math.Max(1, packet.ChannelId)}" : "Offline",
                location,
                ResolveClientChannel(packet.ChannelId),
                isOnline,
                entry.IsLeader,
                entry.IsBlocked)
            {
                MemberId = entry.MemberId,
                IsLocalPlayer = entry.IsLocalPlayer
            };

            _packetOwnedRosterByTab[SocialListTab.Friend] = true;
            _lastPendingRequestByTab[SocialListTab.Friend] = null;
            _lastPacketSyncSummaryByTab[SocialListTab.Friend] =
                $"Client OnFriendResult({(byte)packet.Kind}) updated {entry.Name}'s channel to {location}.";
            ResetSelectionAfterMutation(SocialListTab.Friend);
            return _lastPacketSyncSummaryByTab[SocialListTab.Friend];
        }

        private string ApplyClientPartyListReplace(SocialListClientPartyResultPacket packet)
        {
            _clientPartyId = Math.Max(0, packet.PartyId);
            List<SocialEntryState> entries = _entriesByTab[SocialListTab.Party];
            SocialEntryState localState = ResolveLocalEntryState(SocialListTab.Party);
            entries.Clear();

            for (int i = 0; i < packet.Entries.Count; i++)
            {
                SocialListClientPartyEntry clientEntry = packet.Entries[i];
                entries.Add(CreatePartyEntry(clientEntry, localState));
            }

            _packetOwnedRosterByTab[SocialListTab.Party] = true;
            _lastPendingRequestByTab[SocialListTab.Party] = null;
            _lastPacketSyncSummaryByTab[SocialListTab.Party] =
                $"Client OnPartyResult({(byte)packet.Kind}) synchronized party {packet.PartyId} with {packet.Entries.Count} member entr{(packet.Entries.Count == 1 ? "y" : "ies")}.";
            ResetSelectionAfterMutation(SocialListTab.Party);
            return _lastPacketSyncSummaryByTab[SocialListTab.Party];
        }

        private string ApplyClientPartyLeaderChange(SocialListClientPartyResultPacket packet)
        {
            if (packet.MemberId <= 0)
            {
                return "Client OnPartyResult leader-change member id must be positive.";
            }

            List<SocialEntryState> entries = _entriesByTab[SocialListTab.Party];
            bool found = false;
            for (int i = 0; i < entries.Count; i++)
            {
                SocialEntryState entry = entries[i];
                bool isLeader = entry.MemberId == packet.MemberId && packet.Level > 0;
                found |= entry.MemberId == packet.MemberId;
                entries[i] = new SocialEntryState(
                    entry.Name,
                    isLeader ? "Leader" : NormalizePartyPrimaryText(entry.PrimaryText, isLeader),
                    entry.SecondaryText,
                    entry.LocationSummary,
                    entry.Channel,
                    entry.IsOnline,
                    isLeader,
                    entry.IsBlocked)
                {
                    MemberId = entry.MemberId,
                    IsLocalPlayer = entry.IsLocalPlayer
                };
            }

            _packetOwnedRosterByTab[SocialListTab.Party] = true;
            _lastPendingRequestByTab[SocialListTab.Party] = null;
            _lastPacketSyncSummaryByTab[SocialListTab.Party] = found
                ? $"Client OnPartyResult({(byte)packet.Kind}) moved party leadership to member #{packet.MemberId}."
                : $"Client OnPartyResult({(byte)packet.Kind}) could not map leader member #{packet.MemberId} onto the party roster.";
            ResetSelectionAfterMutation(SocialListTab.Party);
            return _lastPacketSyncSummaryByTab[SocialListTab.Party];
        }

        private string ApplyClientPartyJobLevelChange(SocialListClientPartyResultPacket packet)
        {
            if (packet.MemberId <= 0)
            {
                return "Client OnPartyResult member job or level update must include a positive member id.";
            }

            List<SocialEntryState> entries = _entriesByTab[SocialListTab.Party];
            int index = entries.FindIndex(entry => entry.MemberId == packet.MemberId);
            if (index < 0)
            {
                _packetOwnedRosterByTab[SocialListTab.Party] = true;
                _lastPacketSyncSummaryByTab[SocialListTab.Party] =
                    $"Client OnPartyResult({(byte)packet.Kind}) job/level update for member #{packet.MemberId} did not match the packet-owned roster.";
                return _lastPacketSyncSummaryByTab[SocialListTab.Party];
            }

            SocialEntryState entry = entries[index];
            entries[index] = new SocialEntryState(
                entry.Name,
                entry.IsLeader ? "Leader" : $"Job {packet.JobId}",
                $"Lv. {Math.Max(1, packet.Level)}",
                entry.LocationSummary,
                entry.Channel,
                entry.IsOnline,
                entry.IsLeader,
                entry.IsBlocked)
            {
                MemberId = entry.MemberId,
                IsLocalPlayer = entry.IsLocalPlayer
            };
            _packetOwnedRosterByTab[SocialListTab.Party] = true;
            _lastPendingRequestByTab[SocialListTab.Party] = null;
            _lastPacketSyncSummaryByTab[SocialListTab.Party] =
                $"Client OnPartyResult({(byte)packet.Kind}) updated {entry.Name} to job {packet.JobId}, level {Math.Max(1, packet.Level)}.";
            ResetSelectionAfterMutation(SocialListTab.Party);
            return _lastPacketSyncSummaryByTab[SocialListTab.Party];
        }

        private SocialEntryState CreateFriendEntry(SocialListTab tab, SocialListClientFriendEntry clientEntry)
        {
            return new SocialEntryState(
                clientEntry.Name,
                ResolveFriendPrimaryText(tab, clientEntry),
                ResolveFriendSecondaryText(clientEntry),
                ResolveFriendLocationText(clientEntry),
                ResolveClientChannel(clientEntry.ChannelId),
                IsClientFriendOnline(clientEntry),
                isLeader: false,
                tab == SocialListTab.Blacklist)
            {
                MemberId = clientEntry.FriendId > 0 ? clientEntry.FriendId : null,
                IsLocalPlayer = false
            };
        }

        private SocialEntryState CreatePartyEntry(SocialListClientPartyEntry clientEntry, SocialEntryState localState)
        {
            bool isLocal = localState != null && string.Equals(localState.Name, clientEntry.Name, StringComparison.OrdinalIgnoreCase);
            return new SocialEntryState(
                isLocal ? localState.Name : clientEntry.Name,
                clientEntry.IsLeader ? "Leader" : $"Job {clientEntry.JobId}",
                $"Lv. {Math.Max(1, clientEntry.Level)}",
                clientEntry.FieldId > 0 ? $"Field {clientEntry.FieldId}" : "Unknown field",
                ResolveClientChannel(clientEntry.ChannelId),
                clientEntry.ChannelId > 0,
                clientEntry.IsLeader,
                isBlocked: false)
            {
                MemberId = clientEntry.MemberId > 0 ? clientEntry.MemberId : null,
                IsLocalPlayer = isLocal
            };
        }

        private static string ResolveFriendPrimaryText(SocialListTab tab, SocialListClientFriendEntry clientEntry)
        {
            if (tab == SocialListTab.Blacklist)
            {
                return "Blacklisted";
            }

            return string.IsNullOrWhiteSpace(clientEntry.GroupName)
                ? "Friend"
                : clientEntry.GroupName.Trim();
        }

        private static string ResolveFriendSecondaryText(SocialListClientFriendEntry clientEntry)
        {
            if (clientEntry.InShop)
            {
                return "Cash Shop";
            }

            return clientEntry.ChannelId > 0
                ? $"Ch. {clientEntry.ChannelId}"
                : "Offline";
        }

        private static string ResolveFriendLocationText(SocialListClientFriendEntry clientEntry)
        {
            if (clientEntry.InShop)
            {
                return "Cash Shop";
            }

            return clientEntry.ChannelId > 0
                ? $"Channel {clientEntry.ChannelId}"
                : "Offline";
        }

        private static int ResolveClientChannel(int channelId)
        {
            return Math.Max(1, channelId);
        }

        private static bool IsClientFriendOnline(SocialListClientFriendEntry clientEntry)
        {
            return clientEntry.InShop || clientEntry.ChannelId > 0;
        }

        private static string NormalizePartyPrimaryText(string primaryText, bool isLeader)
        {
            if (isLeader)
            {
                return "Leader";
            }

            return string.Equals(primaryText, "Leader", StringComparison.OrdinalIgnoreCase)
                ? "Member"
                : primaryText;
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
            if (tab == SocialListTab.Guild && ShouldIgnoreGuildScopedResult(ownerId, out int activeGuildId))
            {
                return $"Ignored client OnGuildResult({(byte)SocialListClientGuildResultKind.GradeChange}) for guild {ownerId} because the active packet-owned guild context is {activeGuildId}.";
            }

            if (tab == SocialListTab.Guild)
            {
                RememberPacketGuildId(ownerId);
            }

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
