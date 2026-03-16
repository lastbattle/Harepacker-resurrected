using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
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
    }

    internal sealed class MemoMailboxSnapshot
    {
        public IReadOnlyList<MemoMailboxEntrySnapshot> Entries { get; init; } = Array.Empty<MemoMailboxEntrySnapshot>();
        public int UnreadCount { get; init; }
    }
}
