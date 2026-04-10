using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class AdminShopPacketOwnedSessionContract
    {
        public bool IsActive { get; private set; }
        public bool IsWaitingForResult { get; private set; }
        public bool WouldDisconnect { get; private set; }
        public bool HasObservableState => IsActive
            || OpenCount > 0
            || ResultCount > 0
            || BlockedByOwnerCount > 0
            || NpcTemplateId > 0
            || DecodedItemCount > 0
            || !string.IsNullOrWhiteSpace(LastNotice)
            || !string.IsNullOrWhiteSpace(LastOwnerState);
        public int NpcTemplateId { get; private set; }
        public int DecodedItemCount { get; private set; }
        public int TrailingByteCount { get; private set; }
        public int OpenCount { get; private set; }
        public int BlockedByOwnerCount { get; private set; }
        public int ResultCount { get; private set; }
        public int LastSubtype { get; private set; } = -1;
        public int LastResultCode { get; private set; } = -1;
        public string LastNotice { get; private set; } = string.Empty;
        public string LastOutboundSummary { get; private set; } = string.Empty;
        public string LastOwnerState { get; private set; } = string.Empty;
        public bool AskItemWishlist { get; private set; }

        public void BeginOpen(AdminShopPacketOwnedOpenPayloadSnapshot snapshot, string ownerState = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            AskItemWishlist = snapshot.AskItemWishlist;
            IsActive = true;
            IsWaitingForResult = false;
            WouldDisconnect = false;
            NpcTemplateId = snapshot.NpcTemplateId;
            DecodedItemCount = snapshot.CommodityCount;
            TrailingByteCount = snapshot.TrailingByteCount;
            OpenCount++;
            LastSubtype = -1;
            LastResultCode = -1;
            LastNotice = string.Empty;
            LastOutboundSummary = string.Empty;
            LastOwnerState = ownerState ?? string.Empty;
        }

        public void RejectOpen(string noticeText, string outboundSummary, string ownerState = null)
        {
            IsActive = false;
            IsWaitingForResult = false;
            WouldDisconnect = false;
            DecodedItemCount = 0;
            NpcTemplateId = 0;
            TrailingByteCount = 0;
            AskItemWishlist = false;
            LastSubtype = -1;
            LastResultCode = -1;
            LastNotice = noticeText ?? string.Empty;
            LastOutboundSummary = outboundSummary ?? string.Empty;
            LastOwnerState = ownerState ?? LastOwnerState;
        }

        public void RecordBlockedByOwner(int npcTemplateId, int decodedItemCount, string blockingOwner)
        {
            IsActive = false;
            IsWaitingForResult = false;
            WouldDisconnect = false;
            AskItemWishlist = false;
            NpcTemplateId = Math.Max(0, npcTemplateId);
            DecodedItemCount = Math.Max(0, decodedItemCount);
            TrailingByteCount = 0;
            LastSubtype = -1;
            LastResultCode = -1;
            LastNotice = string.Empty;
            LastOutboundSummary = string.Empty;
            LastOwnerState = string.IsNullOrWhiteSpace(blockingOwner)
                ? "Packet 367 was blocked by another unique-modeless owner."
                : $"Packet 367 was blocked by the visible {blockingOwner} unique-modeless owner.";
            BlockedByOwnerCount++;
        }

        public void RecordResultPacket(byte subtype, byte resultCode)
        {
            ResultCount++;
            LastSubtype = subtype;
            LastResultCode = resultCode;
            LastOutboundSummary = string.Empty;
            WouldDisconnect = false;
        }

        public void SetWaitingForResult(bool waitingForResult)
        {
            IsWaitingForResult = waitingForResult;
        }

        public void ClearWaitingForResult()
        {
            IsWaitingForResult = false;
        }

        public void MarkDisconnectHazard()
        {
            WouldDisconnect = true;
        }

        public void SetLastNotice(string noticeText)
        {
            LastNotice = noticeText ?? string.Empty;
        }

        public void ClearLastNotice()
        {
            LastNotice = string.Empty;
        }

        public void SetLastOutboundSummary(string outboundSummary)
        {
            LastOutboundSummary = outboundSummary ?? string.Empty;
        }

        public void SetLastOwnerState(string ownerState)
        {
            LastOwnerState = ownerState ?? string.Empty;
        }

        public string BuildStateSummary(IReadOnlyList<AdminShopDialogUI.PacketOwnedAdminShopCommoditySnapshot> rows)
        {
            rows ??= Array.Empty<AdminShopDialogUI.PacketOwnedAdminShopCommoditySnapshot>();
            string npcText = NpcTemplateId > 0
                ? $"NPC {NpcTemplateId}"
                : "NPC unresolved";
            int buyRowCount = rows.Count(row => row != null && row.Price > 0);
            int sellRowCount = rows.Count(row => row != null && row.Price <= 0);
            string resultText = LastSubtype >= 0
                ? $"last result subtype {LastSubtype}, code {LastResultCode}"
                : "no result packet yet";
            string outboundText = string.IsNullOrWhiteSpace(LastOutboundSummary)
                ? "no mirrored reopen/close packet yet"
                : LastOutboundSummary;
            string disconnectText = WouldDisconnect
                ? "client-disconnect parity hazard recorded"
                : "no disconnect hazard recorded";
            string wishlistText = AskItemWishlist ? "wishlist prompt on" : "wishlist prompt off";
            string trailingText = TrailingByteCount > 0
                ? $", opaque tail {TrailingByteCount} byte(s)"
                : string.Empty;
            string waitText = IsWaitingForResult
                ? ", waiting for packet 366"
                : string.Empty;
            string ownerText = string.IsNullOrWhiteSpace(LastOwnerState)
                ? "owner state unresolved"
                : LastOwnerState;
            string blockedText = BlockedByOwnerCount > 0
                ? $", blocked opens {BlockedByOwnerCount}"
                : string.Empty;

            return $"Packet-owned admin shop: {npcText}, {wishlistText}, open rows {DecodedItemCount} (buy {buyRowCount}, sell {sellRowCount}{trailingText}), packets open={OpenCount}/result={ResultCount}, {resultText}, {outboundText}, {disconnectText}{waitText}, {ownerText}{blockedText}";
        }
    }
}
