using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum ParcelDialogTab
    {
        Receive = 0,
        Send = 1,
        QuickSend = 2
    }

    internal enum ParcelComposeMode
    {
        Send,
        QuickSend
    }

    [Flags]
    internal enum ParcelDialogTabAvailability
    {
        None = 0,
        Receive = 1 << 0,
        Send = 1 << 1,
        QuickSend = 1 << 2,
        All = Receive | Send | QuickSend
    }

    internal enum MemoDraftAttachmentKind
    {
        None,
        Item,
        Meso,
        ItemAndMeso
    }

    internal sealed class MemoMailboxDraftSnapshot
    {
        public string Recipient { get; init; } = string.Empty;
        public string Subject { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string AttachmentSummary { get; init; } = string.Empty;
        public string ItemAttachmentSummary { get; init; } = string.Empty;
        public string LastActionSummary { get; init; } = string.Empty;
        public bool HasAttachment { get; init; }
        public bool HasItemAttachment { get; init; }
        public bool HasMesoAttachment { get; init; }
        public bool IsMesoAttachment { get; init; }
        public int AttachedMeso { get; init; }
        public bool CanSend { get; init; }
        public bool CanQuickSend { get; init; }
        public ParcelComposeMode ActiveMode { get; init; }
        public ParcelDialogTab ActiveTab { get; init; }
        public MemoDraftAttachmentKind AttachmentKind { get; init; }
        public bool AwaitingItemSelection { get; init; }
        public bool ShowTaxInfo { get; init; }
        public string ModeSummary { get; init; } = string.Empty;
        public string TaxSummary { get; init; } = string.Empty;
        public ParcelDialogOutboundRequestSnapshot LastOutboundRequest { get; init; }
    }

    internal sealed class ParcelDialogOutboundRequestSnapshot
    {
        public int Opcode { get; init; }
        public byte Subtype { get; init; }
        public ParcelDialogTab SourceTab { get; init; }
        public byte InventoryType { get; init; }
        public short InventoryPosition { get; init; }
        public int AttachmentItemId { get; init; }
        public short ItemQuantity { get; init; }
        public int Meso { get; init; }
        public int FeeMeso { get; init; }
        public string Recipient { get; init; } = string.Empty;
        public bool IsQuickDelivery { get; init; }
        public string QuickDeliveryMemo { get; init; } = string.Empty;
        public int QuickDeliveryCouponPosition { get; init; }
        public int ParcelSerial { get; init; }
        public byte[] Payload { get; init; } = Array.Empty<byte>();
        public string PayloadHex => Payload.Length == 0 ? string.Empty : Convert.ToHexString(Payload);
    }

    internal sealed class MemoMailboxAttachmentSnapshot
    {
        public int MemoId { get; init; }
        public string Sender { get; init; } = string.Empty;
        public string Subject { get; init; } = string.Empty;
        public string DeliveredAtText { get; init; } = string.Empty;
        public string ClaimDeadlineText { get; init; } = string.Empty;
        public string ClaimDeadlineStatusText { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public string AttachmentSummary { get; init; } = string.Empty;
        public string AttachmentDescription { get; init; } = string.Empty;
        public int AttachmentItemId { get; init; }
        public int AttachmentQuantity { get; init; }
        public int AttachmentMeso { get; init; }
        public bool CanClaim { get; init; }
        public bool IsClaimed { get; init; }
        public bool IsExpired { get; init; }
    }

    internal sealed class MemoMailboxClaimResult
    {
        public int MemoId { get; init; }
        public int AttachmentItemId { get; init; }
        public int AttachmentQuantity { get; init; }
        public int AttachmentMeso { get; init; }
        public string AttachmentSummary { get; init; } = string.Empty;
    }

    internal sealed class MemoMailboxEntrySnapshot
    {
        public int MemoId { get; init; }
        public string Sender { get; init; } = string.Empty;
        public string Subject { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string Preview { get; init; } = string.Empty;
        public string DeliveredAtText { get; init; } = string.Empty;
        public string ClaimDeadlineText { get; init; } = string.Empty;
        public string ClaimDeadlineStatusText { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public bool IsRead { get; init; }
        public bool IsKept { get; init; }
        public bool IsQuickDelivery { get; init; }
        public bool HasAttachment { get; init; }
        public bool CanClaimAttachment { get; init; }
        public bool IsAttachmentClaimed { get; init; }
        public bool IsExpired { get; init; }
        public string AttachmentSummary { get; init; } = string.Empty;
    }

    internal sealed class MemoMailboxSnapshot
    {
        public IReadOnlyList<MemoMailboxEntrySnapshot> Entries { get; init; } = Array.Empty<MemoMailboxEntrySnapshot>();
        public ParcelDialogTab ActiveTab { get; init; }
        public ParcelDialogTabAvailability AvailableTabs { get; init; } = ParcelDialogTabAvailability.All;
        public int UnreadCount { get; init; }
        public int ClaimableCount { get; init; }
        public string LastActionSummary { get; init; } = string.Empty;
    }
}
