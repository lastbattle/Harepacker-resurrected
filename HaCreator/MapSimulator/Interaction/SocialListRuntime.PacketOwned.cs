using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed partial class SocialListRuntime
    {
        internal Func<SocialListPacketOwnedRequest, string> PacketOwnedRequestDispatcher { get; set; }

        private bool IsPacketOwned(SocialListTab tab)
        {
            return _packetOwnedRosterByTab.TryGetValue(tab, out bool packetOwned) && packetOwned;
        }

        internal bool UsesPacketOwnedGuildSkillAuthority()
        {
            return IsPacketOwned(SocialListTab.Guild)
                   || _packetGuildUiState.HasValue
                   || _packetGuildAuthority.HasValue;
        }

        internal bool HasPendingPacketOwnedRequest(SocialListTab tab)
        {
            return tab == SocialListTab.Guild
                ? _pendingGuildDialogRequest.HasValue
                : _lastPendingRequestByTab.TryGetValue(tab, out string pendingRequest) && !string.IsNullOrWhiteSpace(pendingRequest);
        }

        internal bool UsesPacketOwnedPartyAdmissionContext()
        {
            return IsPacketOwned(SocialListTab.Party);
        }

        internal bool HasPacketOwnedPartyAdmissionContext()
        {
            return IsPacketOwned(SocialListTab.Party)
                && _clientPartyId > 0;
        }

        internal bool UsesPacketOwnedExpeditionAdmissionContext()
        {
            return _expeditionIntermediary.PacketOwned;
        }

        internal bool HasPacketOwnedExpeditionAdmissionContext()
        {
            return _expeditionIntermediary.PacketOwned
                && _expeditionIntermediary.HasActiveExpedition;
        }

        private SocialEntryState MergePacketOwnedLocalEntry(SocialEntryState existingEntry, SocialEntryState localEntry)
        {
            if (localEntry == null)
            {
                return existingEntry;
            }

            if (existingEntry == null)
            {
                return localEntry;
            }

            return new SocialEntryState(
                localEntry.Name,
                string.IsNullOrWhiteSpace(existingEntry.PrimaryText) ? localEntry.PrimaryText : existingEntry.PrimaryText,
                string.IsNullOrWhiteSpace(existingEntry.SecondaryText) ? localEntry.SecondaryText : existingEntry.SecondaryText,
                string.IsNullOrWhiteSpace(localEntry.LocationSummary) ? existingEntry.LocationSummary : localEntry.LocationSummary,
                localEntry.Channel > 0 ? localEntry.Channel : existingEntry.Channel,
                localEntry.IsOnline,
                existingEntry.IsLeader || localEntry.IsLeader,
                existingEntry.IsBlocked || localEntry.IsBlocked)
            {
                MemberId = existingEntry.MemberId ?? localEntry.MemberId,
                IsLocalPlayer = true,
                ClientGuildGrade = existingEntry.ClientGuildGrade ?? localEntry.ClientGuildGrade
            };
        }

        private string BuildOwnershipSummary(SocialListTab tab)
        {
            string summary = _lastPacketSyncSummaryByTab.TryGetValue(tab, out string lastSyncSummary) && !string.IsNullOrWhiteSpace(lastSyncSummary)
                ? lastSyncSummary
                : "No packet roster sync received.";
            string pending = _lastPendingRequestByTab.TryGetValue(tab, out string pendingRequest) && !string.IsNullOrWhiteSpace(pendingRequest)
                ? $" Pending: {pendingRequest}."
                : string.Empty;

            return IsPacketOwned(tab)
                ? $"Packet-owned roster. {summary}{pending}"
                : "Simulator-owned roster.";
        }

        private string GetOwnershipBadge(SocialListTab tab)
        {
            return IsPacketOwned(tab) ? "Packet-owned" : "Local";
        }

        private bool TryStagePacketOwnedRequest(SocialListTab tab, string requestKind, out string requestMessage)
        {
            return TryStagePacketOwnedRequest(tab, requestKind, GetSelectedEntry(tab), out requestMessage);
        }

        private bool TryStagePacketOwnedRequest(SocialListTab tab, string requestKind, SocialEntryState requestEntry, out string requestMessage)
        {
            if (!IsPacketOwned(tab))
            {
                requestMessage = null;
                return false;
            }

            string normalizedRequest = string.IsNullOrWhiteSpace(requestKind) ? "Roster update" : requestKind.Trim();
            _lastPendingRequestByTab[tab] = normalizedRequest;
            SocialListOutboundRequestDraft outboundDraft = BuildPacketOwnedOutboundDraft(
                tab,
                normalizedRequest,
                requestEntry);
            string dispatchMessage = PacketOwnedRequestDispatcher?.Invoke(new SocialListPacketOwnedRequest(
                tab,
                normalizedRequest,
                requestEntry?.Name,
                requestEntry?.MemberId,
                outboundDraft));
            requestMessage =
                $"{normalizedRequest} is staged locally, and {tab} currently follows packet-owned roster authority until a matching client result resolves it.";
            if (!string.IsNullOrWhiteSpace(dispatchMessage))
            {
                requestMessage += $" {dispatchMessage.Trim()}";
            }

            return true;
        }

        private SocialListOutboundRequestDraft BuildPacketOwnedOutboundDraft(
            SocialListTab tab,
            string requestKind,
            SocialEntryState selectedEntry)
        {
            string normalizedRequest = string.IsNullOrWhiteSpace(requestKind)
                ? string.Empty
                : requestKind.Trim();
            string selectedName = selectedEntry?.Name;
            int selectedMemberId = selectedEntry?.MemberId ?? 0;

            return normalizedRequest.ToLowerInvariant() switch
            {
                "friend add" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.FriendAdd, selectedName, GroupName: ResolveFriendAddGroupName(selectedEntry)),
                "friend delete" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.FriendDelete, selectedName, selectedMemberId),
                "party create" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.PartyCreate),
                "party invite" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.PartyInvite, selectedName, selectedMemberId),
                "party kick" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.PartyKick, selectedName, selectedMemberId),
                "party withdraw" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.PartyWithdraw, selectedName, selectedMemberId),
                "party change boss" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.PartyChangeBoss, selectedName, selectedMemberId),
                "guild invite" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.GuildInvite, selectedName, selectedMemberId),
                "guild remove" => selectedEntry?.IsLocalPlayer == true
                    ? new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.GuildWithdraw, selectedName, selectedMemberId)
                    : new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.GuildKick, selectedName, selectedMemberId),
                "guild grade up" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.GuildGradeChange, selectedName, selectedMemberId, ResolveGuildGradeChangeTarget(selectedEntry, up: true)),
                "guild grade down" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.GuildGradeChange, selectedName, selectedMemberId, ResolveGuildGradeChangeTarget(selectedEntry, up: false)),
                "alliance invite" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.AllianceInvite, selectedName, selectedMemberId),
                "alliance remove" => selectedEntry?.IsLocalPlayer == true
                    ? new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.AllianceWithdraw, selectedName, selectedMemberId)
                    : new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.AllianceKick, selectedName, selectedMemberId),
                "alliance grade up" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.AllianceGradeChange, selectedName, selectedMemberId, 1),
                "alliance grade down" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.AllianceGradeChange, selectedName, selectedMemberId, -1),
                "blacklist add" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.BlacklistAdd, selectedName, selectedMemberId),
                "blacklist delete" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.BlacklistDelete, selectedName, selectedMemberId),
                "blacklist remove" => new SocialListOutboundRequestDraft(SocialListOutboundRequestKind.BlacklistDelete, selectedName, selectedMemberId),
                _ => new SocialListOutboundRequestDraft(ResolveFallbackOutboundKind(tab), selectedName, selectedMemberId)
            };
        }

        private string ResolveFriendAddGroupName(SocialEntryState selectedEntry)
        {
            if (selectedEntry != null && TryGetFriendGroupLabel(selectedEntry.Name, out string selectedGroupLabel))
            {
                return selectedGroupLabel;
            }

            return _friendGroups.Count > 0 ? _friendGroups[0] : string.Empty;
        }

        private static int ResolveGuildGradeChangeTarget(SocialEntryState selectedEntry, bool up)
        {
            int currentGrade = Math.Clamp(selectedEntry?.ClientGuildGrade ?? 3, 1, 5);
            return Math.Clamp(up ? currentGrade - 1 : currentGrade + 1, 1, 5);
        }

        private static SocialListOutboundRequestKind ResolveFallbackOutboundKind(SocialListTab tab)
        {
            return tab switch
            {
                SocialListTab.Party => SocialListOutboundRequestKind.PartyInvite,
                SocialListTab.Guild => SocialListOutboundRequestKind.GuildInvite,
                SocialListTab.Alliance => SocialListOutboundRequestKind.AllianceInvite,
                SocialListTab.Blacklist => SocialListOutboundRequestKind.BlacklistAdd,
                _ => SocialListOutboundRequestKind.FriendAdd
            };
        }
    }

    internal readonly record struct SocialListPacketOwnedRequest(
        SocialListTab Tab,
        string RequestKind,
        string SelectedName,
        int? SelectedMemberId,
        SocialListOutboundRequestDraft OutboundRequest);
}
