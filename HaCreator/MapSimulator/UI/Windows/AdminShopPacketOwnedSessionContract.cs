using HaCreator.MapSimulator.Interaction;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    internal enum AdminShopPacketOwnedOwnerVisibilityState
    {
        Hidden,
        StagedButHidden,
        Visible,
        HiddenByCashShopFamily
    }

    internal sealed class AdminShopPacketOwnedSessionContract
    {
        public bool IsActive { get; private set; }
        public bool IsWaitingForResult { get; private set; }
        public bool IsOwnerSurfaceVisible { get; private set; }
        public bool WouldDisconnect { get; private set; }
        public bool HasObservableState => IsActive
            || OpenCount > 0
            || CloseCount > 0
            || ResultCount > 0
            || BlockedByOwnerCount > 0
            || NpcTemplateId > 0
            || DecodedItemCount > 0
            || !string.IsNullOrWhiteSpace(LastNotice)
            || !string.IsNullOrWhiteSpace(LastOwnerState);
        public int NpcTemplateId { get; private set; }
        public int DecodedItemCount { get; private set; }
        public int TrailingByteCount { get; private set; }
        public int ResultTrailingByteCount { get; private set; }
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }
        public int BlockedByOwnerCount { get; private set; }
        public int ResultCount { get; private set; }
        public int LastSubtype { get; private set; } = -1;
        public int LastResultCode { get; private set; } = -1;
        public bool LastResultHadResultCode { get; private set; }
        public int LastOutboundOpcode { get; private set; } = -1;
        public byte[] LastOutboundPayload { get; private set; } = Array.Empty<byte>();
        public string LastNotice { get; private set; } = string.Empty;
        public string LastOutboundSummary { get; private set; } = string.Empty;
        public string LastOwnerState { get; private set; } = string.Empty;
        public bool LastCloseOpenedWishlist { get; private set; }
        public bool AskItemWishlist { get; private set; }
        public int PendingWishlistRegisterItemId { get; private set; }
        public string PendingWishlistRegisterTitle { get; private set; } = string.Empty;
        public string PendingWishlistRegisterCategoryLabel { get; private set; } = string.Empty;
        public bool HasPendingWishlistRegister => PendingWishlistRegisterItemId > 0
            || !string.IsNullOrWhiteSpace(PendingWishlistRegisterTitle)
            || !string.IsNullOrWhiteSpace(PendingWishlistRegisterCategoryLabel);
        public AdminShopPacketOwnedOwnerVisibilityState OwnerVisibilityState { get; private set; }
            = AdminShopPacketOwnedOwnerVisibilityState.Hidden;

        public void BeginOpen(AdminShopPacketOwnedOpenPayloadSnapshot snapshot, string ownerState = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            AskItemWishlist = snapshot.AskItemWishlist;
            IsActive = true;
            IsWaitingForResult = false;
            IsOwnerSurfaceVisible = false;
            WouldDisconnect = false;
            OwnerVisibilityState = AdminShopPacketOwnedOwnerVisibilityState.StagedButHidden;
            NpcTemplateId = snapshot.NpcTemplateId;
            DecodedItemCount = snapshot.CommodityCount;
            TrailingByteCount = snapshot.TrailingByteCount;
            ResultTrailingByteCount = 0;
            OpenCount++;
            LastSubtype = -1;
            LastResultCode = -1;
            LastResultHadResultCode = false;
            LastOutboundOpcode = -1;
            LastOutboundPayload = Array.Empty<byte>();
            LastNotice = string.Empty;
            LastOutboundSummary = string.Empty;
            LastOwnerState = ownerState ?? string.Empty;
            LastCloseOpenedWishlist = false;
            ClearPendingWishlistRegister();
        }

        public void RejectOpen(
            string noticeText,
            string outboundSummary,
            string ownerState = null,
            AdminShopPacketOwnedOwnerVisibilityState visibilityState = AdminShopPacketOwnedOwnerVisibilityState.HiddenByCashShopFamily)
        {
            IsActive = false;
            IsWaitingForResult = false;
            IsOwnerSurfaceVisible = false;
            WouldDisconnect = false;
            OwnerVisibilityState = visibilityState;
            DecodedItemCount = 0;
            NpcTemplateId = 0;
            TrailingByteCount = 0;
            ResultTrailingByteCount = 0;
            AskItemWishlist = false;
            LastSubtype = -1;
            LastResultCode = -1;
            LastResultHadResultCode = false;
            LastNotice = noticeText ?? string.Empty;
            ClearPendingWishlistRegister();
            if (!string.IsNullOrWhiteSpace(outboundSummary))
            {
                LastOutboundSummary = outboundSummary;
            }

            LastOwnerState = ownerState ?? LastOwnerState;
            LastCloseOpenedWishlist = false;
        }

        public void RecordLocalClose(string outboundSummary, string ownerState = null, bool openedWishlistBeforeClose = false)
        {
            IsActive = false;
            IsWaitingForResult = false;
            IsOwnerSurfaceVisible = false;
            WouldDisconnect = false;
            OwnerVisibilityState = AdminShopPacketOwnedOwnerVisibilityState.Hidden;
            ResultTrailingByteCount = 0;
            LastSubtype = -1;
            LastResultCode = -1;
            LastResultHadResultCode = false;
            LastNotice = string.Empty;
            LastOutboundSummary = outboundSummary ?? string.Empty;
            LastOwnerState = ownerState ?? string.Empty;
            LastCloseOpenedWishlist = openedWishlistBeforeClose;
            CloseCount++;
            ClearPendingWishlistRegister();
        }

        public void RecordBlockedByOwner(AdminShopPacketOwnedOpenPayloadSnapshot snapshot, string blockingOwner)
        {
            snapshot ??= new AdminShopPacketOwnedOpenPayloadSnapshot();
            IsActive = false;
            IsWaitingForResult = false;
            IsOwnerSurfaceVisible = false;
            WouldDisconnect = false;
            OwnerVisibilityState = AdminShopPacketOwnedOwnerVisibilityState.StagedButHidden;
            AskItemWishlist = snapshot.AskItemWishlist;
            NpcTemplateId = Math.Max(0, snapshot.NpcTemplateId);
            DecodedItemCount = Math.Max(0, snapshot.CommodityCount);
            TrailingByteCount = Math.Max(0, snapshot.TrailingByteCount);
            ResultTrailingByteCount = 0;
            LastSubtype = -1;
            LastResultCode = -1;
            LastResultHadResultCode = false;
            LastOutboundOpcode = -1;
            LastOutboundPayload = Array.Empty<byte>();
            LastNotice = string.Empty;
            LastOutboundSummary = string.Empty;
            LastOwnerState = string.IsNullOrWhiteSpace(blockingOwner)
                ? "Packet 367 was blocked by another unique-modeless owner."
                : $"Packet 367 was blocked by the visible {blockingOwner} unique-modeless owner.";
            LastCloseOpenedWishlist = false;
            ClearPendingWishlistRegister();
            BlockedByOwnerCount++;
        }

        public void RecordResultPacket(byte subtype, byte resultCode, int trailingByteCount = 0, bool hasResultCode = true)
        {
            ResultCount++;
            LastSubtype = subtype;
            LastResultCode = resultCode;
            LastResultHadResultCode = hasResultCode;
            ResultTrailingByteCount = Math.Max(0, trailingByteCount);
            WouldDisconnect = false;
        }

        public void RecordResultIgnoredByOwner(byte subtype, byte resultCode, string ownerState, int trailingByteCount = 0, bool hasResultCode = true)
        {
            ResultCount++;
            LastSubtype = subtype;
            LastResultCode = resultCode;
            LastResultHadResultCode = hasResultCode;
            ResultTrailingByteCount = Math.Max(0, trailingByteCount);
            IsActive = false;
            IsWaitingForResult = false;
            IsOwnerSurfaceVisible = false;
            WouldDisconnect = false;
            OwnerVisibilityState = AdminShopPacketOwnedOwnerVisibilityState.StagedButHidden;
            LastOwnerState = ownerState ?? string.Empty;
            ClearPendingWishlistRegister();
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

        public void RecordOutboundRequest(int opcode, byte[] payload, string outboundSummary)
        {
            LastOutboundOpcode = opcode;
            LastOutboundPayload = payload?.ToArray() ?? Array.Empty<byte>();
            LastOutboundSummary = outboundSummary ?? string.Empty;
        }

        public void RecordPendingWishlistRegister(int itemId, string title, string categoryLabel)
        {
            PendingWishlistRegisterItemId = Math.Max(0, itemId);
            PendingWishlistRegisterTitle = title ?? string.Empty;
            PendingWishlistRegisterCategoryLabel = categoryLabel ?? string.Empty;
        }

        public void ClearPendingWishlistRegister()
        {
            PendingWishlistRegisterItemId = 0;
            PendingWishlistRegisterTitle = string.Empty;
            PendingWishlistRegisterCategoryLabel = string.Empty;
        }

        public void SetLastOutboundSummary(string outboundSummary)
        {
            LastOutboundSummary = outboundSummary ?? string.Empty;
        }

        public void RecordOwnerSurfaceShown(string ownerState = null)
        {
            IsOwnerSurfaceVisible = true;
            OwnerVisibilityState = AdminShopPacketOwnedOwnerVisibilityState.Visible;
            if (!string.IsNullOrWhiteSpace(ownerState))
            {
                LastOwnerState = ownerState;
            }
        }

        public void RecordOwnerSurfaceHidden(
            string ownerState = null,
            AdminShopPacketOwnedOwnerVisibilityState visibilityState = AdminShopPacketOwnedOwnerVisibilityState.Hidden)
        {
            IsOwnerSurfaceVisible = false;
            OwnerVisibilityState = visibilityState;
            if (!string.IsNullOrWhiteSpace(ownerState))
            {
                LastOwnerState = ownerState;
            }
        }

        public bool ShouldAcceptResultPacketAtOwnerGate(bool ownerSurfaceCurrentlyVisible)
        {
            if (ownerSurfaceCurrentlyVisible)
            {
                return true;
            }

            return OwnerVisibilityState == AdminShopPacketOwnedOwnerVisibilityState.Visible;
        }

        public void SetLastOwnerState(string ownerState)
        {
            LastOwnerState = ownerState ?? string.Empty;
        }

        public string BuildTransportSummary()
        {
            if (LastOutboundOpcode < 0)
            {
                return string.IsNullOrWhiteSpace(LastOutboundSummary)
                    ? "admin-shop outbound=idle."
                    : $"admin-shop outbound=idle ({LastOutboundSummary})";
            }

            string payloadHex = Convert.ToHexString(LastOutboundPayload ?? Array.Empty<byte>());
            return string.IsNullOrWhiteSpace(LastOutboundSummary)
                ? $"admin-shop outbound={LastOutboundOpcode}[{payloadHex}]."
                : $"admin-shop outbound={LastOutboundOpcode}[{payloadHex}] ({LastOutboundSummary})";
        }

        public string BuildWishlistSearchSessionSignature()
        {
            return string.Join("|",
                IsActive ? "1" : "0",
                IsWaitingForResult ? "1" : "0",
                WouldDisconnect ? "1" : "0",
                OpenCount,
                ResultCount,
                BlockedByOwnerCount,
                NpcTemplateId,
                DecodedItemCount,
                TrailingByteCount,
                ResultTrailingByteCount,
                LastResultHadResultCode ? "1" : "0",
                CloseCount,
                LastCloseOpenedWishlist ? "1" : "0",
                AskItemWishlist ? "1" : "0",
                LastSubtype,
                LastResultCode,
                PendingWishlistRegisterItemId,
                PendingWishlistRegisterTitle ?? string.Empty,
                PendingWishlistRegisterCategoryLabel ?? string.Empty,
                ((int)OwnerVisibilityState).ToString(),
                LastNotice ?? string.Empty,
                LastOutboundSummary ?? string.Empty,
                LastOwnerState ?? string.Empty);
        }

        public string BuildWishlistSearchStateSummary()
        {
            if (!HasObservableState)
            {
                return string.Empty;
            }

            string packetText = OpenCount > 0
                ? $"pkt open {OpenCount}"
                : "pkt idle";
            string resultText = ResultCount > 0
                ? $"result {ResultCount}"
                : "result 0";
            string closeText = CloseCount > 0
                ? $", close {CloseCount}"
                : string.Empty;
            string phaseText = DescribeWishlistSearchPhase();
            string wishlistText = AskItemWishlist
                ? "wish prompt on"
                : "wish prompt off";
            string npcText = NpcTemplateId > 0
                ? $"npc {NpcTemplateId}"
                : "npc unresolved";
            string rowText = DecodedItemCount > 0
                ? $"rows {DecodedItemCount}"
                : "rows 0";
            string resultStateText = LastSubtype >= 0
                ? $"last {DescribeLastResultState()}"
                : "no result packet";
            string pendingText = HasPendingWishlistRegister
                ? $", pending {DescribePendingWishlistRegister()}"
                : string.Empty;
            return $"{packetText}, {resultText}{closeText}, {phaseText}, {wishlistText}, {npcText}, {rowText}, {resultStateText}{pendingText}";
        }

        public IReadOnlyList<string> BuildWishlistSearchStateDetailLines()
        {
            if (!HasObservableState)
            {
                return Array.Empty<string>();
            }

            List<string> lines = new(3);
            string visibilityText = DescribeOwnerVisibility();
            string blockedText = BlockedByOwnerCount > 0
                ? $", blocked {BlockedByOwnerCount}"
                : string.Empty;
            string tailText = TrailingByteCount > 0
                ? $", tail {TrailingByteCount} byte(s)"
                : string.Empty;
            string resultTailText = ResultTrailingByteCount > 0
                ? $", result tail {ResultTrailingByteCount} byte(s)"
                : string.Empty;
            lines.Add($"{visibilityText}, {DescribeDisconnectHazard()}{blockedText}{tailText}{resultTailText}");

            string phaseText = DescribeWishlistSearchPhase();
            if (!string.IsNullOrWhiteSpace(LastNotice))
            {
                lines.Add($"{phaseText}; {LastNotice}");
            }
            else if (LastSubtype >= 0)
            {
                lines.Add($"{phaseText}; packet 366 {DescribeLastResultState()}");
            }
            else
            {
                lines.Add(phaseText);
            }

            if (!string.IsNullOrWhiteSpace(LastOwnerState))
            {
                lines.Add(LastOwnerState);
            }
            else
            {
                lines.Add(BuildTransportSummary());
            }

            if (HasPendingWishlistRegister && lines.Count < 3)
            {
                lines.Add($"pending {DescribePendingWishlistRegister()}");
            }

            return lines;
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
                ? LastResultHadResultCode
                    ? $"last result subtype {LastSubtype}, code {LastResultCode}"
                    : $"last result subtype {LastSubtype}, no result code"
                : "no result packet yet";
            string transportText = BuildTransportSummary();
            string disconnectText = WouldDisconnect
                ? "client-disconnect parity hazard recorded"
                : "no disconnect hazard recorded";
            string wishlistText = AskItemWishlist ? "wishlist prompt on" : "wishlist prompt off";
            string trailingText = TrailingByteCount > 0
                ? $", opaque tail {TrailingByteCount} byte(s)"
                : string.Empty;
            string resultTrailingText = ResultTrailingByteCount > 0
                ? $", result opaque tail {ResultTrailingByteCount} byte(s)"
                : string.Empty;
            string waitText = IsWaitingForResult
                ? ", waiting for packet 366"
                : string.Empty;
            string pendingWishlistText = HasPendingWishlistRegister
                ? $", pending {DescribePendingWishlistRegister()}"
                : string.Empty;
            string closeText = CloseCount > 0
                ? $", close={CloseCount}{(LastCloseOpenedWishlist ? " via wishlist prompt" : string.Empty)}"
                : string.Empty;
            string visibilityText = DescribeOwnerVisibility();
            string ownerText = string.IsNullOrWhiteSpace(LastOwnerState)
                ? "owner state unresolved"
                : LastOwnerState;
            string blockedText = BlockedByOwnerCount > 0
                ? $", blocked opens {BlockedByOwnerCount}"
                : string.Empty;

            return $"Packet-owned admin shop: {npcText}, {wishlistText}, open rows {DecodedItemCount} (buy {buyRowCount}, sell {sellRowCount}{trailingText}{resultTrailingText}), packets open={OpenCount}/result={ResultCount}{closeText}, {resultText}, {transportText}, {disconnectText}{waitText}{pendingWishlistText}, {visibilityText}, {ownerText}{blockedText}";
        }

        private string DescribeLastResultState()
        {
            if (LastSubtype < 0)
            {
                return "result pending";
            }

            if (!AdminShopDialogClientParityText.HandlesResultSubtype((byte)LastSubtype))
            {
                return $"subtype {LastSubtype} ignored";
            }

            string label = AdminShopDialogClientParityText.BuildResultStateLabel((byte)Math.Max(0, LastResultCode));
            return $"subtype {LastSubtype}, code {LastResultCode} ({label})";
        }

        private string DescribeDisconnectHazard()
        {
            return WouldDisconnect
                ? "disconnect hazard recorded"
                : "no disconnect hazard";
        }

        private string DescribeOwnerVisibility()
        {
            return OwnerVisibilityState switch
            {
                AdminShopPacketOwnedOwnerVisibilityState.Visible => "owner surface visible",
                AdminShopPacketOwnedOwnerVisibilityState.StagedButHidden => "owner surface staged but hidden",
                AdminShopPacketOwnedOwnerVisibilityState.HiddenByCashShopFamily => "owner surface hidden by Cash Shop family",
                _ => "owner surface hidden"
            };
        }

        private string DescribeWishlistSearchPhase()
        {
            if (IsWaitingForResult)
            {
                return HasPendingWishlistRegister
                    ? $"awaiting packet 366 subtype 4 for {DescribePendingWishlistRegister()}"
                    : "awaiting packet 366 subtype 4";
            }

            if (LastOutboundOpcode == 74 && LastOutboundPayload.Length > 0)
            {
                return LastOutboundPayload[0] switch
                {
                    0 => "awaiting packet 367 refresh",
                    1 => "trade request submitted",
                    2 => "close requested",
                    3 => "wishlist register submitted",
                    _ => "service idle"
                };
            }

            return IsActive
                ? "service idle"
                : "service closed";
        }

        private string DescribePendingWishlistRegister()
        {
            string itemText = PendingWishlistRegisterItemId > 0
                ? $"item {PendingWishlistRegisterItemId}"
                : "item unresolved";
            string titleText = string.IsNullOrWhiteSpace(PendingWishlistRegisterTitle)
                ? string.Empty
                : $" ({PendingWishlistRegisterTitle})";
            string categoryText = string.IsNullOrWhiteSpace(PendingWishlistRegisterCategoryLabel)
                ? string.Empty
                : $" in {PendingWishlistRegisterCategoryLabel}";
            return $"wishlist register {itemText}{titleText}{categoryText}";
        }
    }
}
