using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed partial class SocialListRuntime
    {
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
                IsLocalPlayer = true
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
            if (!IsPacketOwned(tab))
            {
                requestMessage = null;
                return false;
            }

            string normalizedRequest = string.IsNullOrWhiteSpace(requestKind) ? "Roster update" : requestKind.Trim();
            _lastPendingRequestByTab[tab] = normalizedRequest;
            requestMessage =
                $"{normalizedRequest} is staged locally, and {tab} currently follows packet-owned roster authority until a matching client result resolves it.";
            return true;
        }
    }
}
