using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class MemoMailboxDraftSnapshot
    {
        public string Recipient { get; init; } = string.Empty;
        public string Subject { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string AttachmentSummary { get; init; } = string.Empty;
        public string LastActionSummary { get; init; } = string.Empty;
        public bool HasAttachment { get; init; }
        public bool IsMesoAttachment { get; init; }
        public bool CanSend { get; init; }
        public bool CanQuickSend { get; init; }
    }

    internal sealed class MemoMailboxAttachmentSnapshot
    {
        public int MemoId { get; init; }
        public string Sender { get; init; } = string.Empty;
        public string Subject { get; init; } = string.Empty;
        public string DeliveredAtText { get; init; } = string.Empty;
        public string AttachmentSummary { get; init; } = string.Empty;
        public string AttachmentDescription { get; init; } = string.Empty;
        public bool CanClaim { get; init; }
        public bool IsClaimed { get; init; }
    }

    internal sealed class MemoMailboxEntrySnapshot
    {
        public int MemoId { get; init; }
        public string Sender { get; init; } = string.Empty;
        public string Subject { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string Preview { get; init; } = string.Empty;
        public string DeliveredAtText { get; init; } = string.Empty;
        public bool IsRead { get; init; }
        public bool IsKept { get; init; }
        public bool HasAttachment { get; init; }
        public bool CanClaimAttachment { get; init; }
        public bool IsAttachmentClaimed { get; init; }
        public string AttachmentSummary { get; init; } = string.Empty;
    }

    internal sealed class MemoMailboxSnapshot
    {
        public IReadOnlyList<MemoMailboxEntrySnapshot> Entries { get; init; } = Array.Empty<MemoMailboxEntrySnapshot>();
        public int UnreadCount { get; init; }
        public int ClaimableCount { get; init; }
        public string LastActionSummary { get; init; } = string.Empty;
    }
}
