using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class MemoMailboxManager
    {
        private enum MemoAttachmentKind
        {
            None,
            Item,
            Meso
        }

        private sealed class MemoAttachmentState
        {
            public MemoAttachmentKind Kind { get; init; }
            public int ItemId { get; init; }
            public int Quantity { get; init; }
            public int Meso { get; init; }
            public bool IsClaimed { get; set; }
        }

        private sealed class MemoDraftState
        {
            public string Recipient { get; set; } = "Cody";
            public string Subject { get; set; } = "Simulator note";
            public string Body { get; set; } =
                "Mailbox draft state is simulator-owned so UIWindow2.img/Memo can be exercised without packet auth.";
            public MemoAttachmentState Attachment { get; set; }
        }

        private sealed class MemoState
        {
            public int MemoId { get; init; }
            public string Sender { get; init; } = string.Empty;
            public string Subject { get; init; } = string.Empty;
            public string Body { get; init; } = string.Empty;
            public DateTimeOffset DeliveredAt { get; init; }
            public bool IsRead { get; set; }
            public bool IsKept { get; set; }
            public MemoAttachmentState Attachment { get; init; }
        }

        private readonly List<MemoState> _memos = new();
        private readonly MemoDraftState _draft = new();
        private int _nextMemoId = 1;
        private string _lastActionSummary = "Memo mailbox ready.";

        internal MemoMailboxManager()
        {
            SeedDefaultMemos();
        }

        internal MemoMailboxSnapshot GetSnapshot()
        {
            MemoMailboxEntrySnapshot[] entries = _memos
                .OrderByDescending(memo => memo.DeliveredAt)
                .ThenByDescending(memo => memo.MemoId)
                .Select(memo => new MemoMailboxEntrySnapshot
                {
                    MemoId = memo.MemoId,
                    Sender = memo.Sender,
                    Subject = memo.Subject,
                    Body = memo.Body,
                    Preview = BuildPreview(memo.Body),
                    DeliveredAtText = memo.DeliveredAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm"),
                    IsRead = memo.IsRead,
                    IsKept = memo.IsKept,
                    HasAttachment = memo.Attachment != null,
                    CanClaimAttachment = memo.Attachment != null && !memo.Attachment.IsClaimed,
                    IsAttachmentClaimed = memo.Attachment?.IsClaimed == true,
                    AttachmentSummary = BuildAttachmentSummary(memo.Attachment)
                })
                .ToArray();

            return new MemoMailboxSnapshot
            {
                Entries = entries,
                UnreadCount = entries.Count(entry => !entry.IsRead),
                ClaimableCount = entries.Count(entry => entry.CanClaimAttachment),
                LastActionSummary = _lastActionSummary
            };
        }

        internal MemoMailboxDraftSnapshot GetDraftSnapshot()
        {
            return new MemoMailboxDraftSnapshot
            {
                Recipient = _draft.Recipient,
                Subject = _draft.Subject,
                Body = _draft.Body,
                AttachmentSummary = BuildAttachmentSummary(_draft.Attachment),
                LastActionSummary = _lastActionSummary,
                CanSend = CanSendDraft()
            };
        }

        internal MemoMailboxAttachmentSnapshot GetAttachmentSnapshot(int memoId)
        {
            MemoState memo = FindMemo(memoId);
            if (memo == null)
            {
                return new MemoMailboxAttachmentSnapshot();
            }

            return new MemoMailboxAttachmentSnapshot
            {
                MemoId = memo.MemoId,
                Sender = memo.Sender,
                Subject = memo.Subject,
                DeliveredAtText = memo.DeliveredAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm"),
                AttachmentSummary = BuildAttachmentSummary(memo.Attachment),
                AttachmentDescription = BuildAttachmentDescription(memo.Attachment),
                CanClaim = memo.Attachment != null && !memo.Attachment.IsClaimed,
                IsClaimed = memo.Attachment?.IsClaimed == true
            };
        }

        internal void OpenMemo(int memoId)
        {
            MemoState memo = FindMemo(memoId);
            if (memo != null)
            {
                memo.IsRead = true;
                _lastActionSummary = $"Opened memo #{memo.MemoId} from {memo.Sender}.";
            }
        }

        internal void KeepMemo(int memoId)
        {
            MemoState memo = FindMemo(memoId);
            if (memo != null)
            {
                memo.IsRead = true;
                memo.IsKept = true;
                _lastActionSummary = $"Kept memo #{memo.MemoId} in the inbox backlog.";
            }
        }

        internal void DeleteMemo(int memoId)
        {
            if (_memos.RemoveAll(memo => memo.MemoId == memoId) > 0)
            {
                _lastActionSummary = $"Deleted memo #{memoId}.";
            }
        }

        internal void DeliverMemo(
            string sender,
            string subject,
            string body,
            DateTimeOffset? deliveredAt = null,
            bool isRead = false,
            int attachmentItemId = 0,
            int attachmentQuantity = 0,
            int attachmentMeso = 0)
        {
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            MemoAttachmentState attachment = BuildAttachment(attachmentItemId, attachmentQuantity, attachmentMeso);
            _memos.Add(new MemoState
            {
                MemoId = _nextMemoId++,
                Sender = string.IsNullOrWhiteSpace(sender) ? "Maple Admin" : sender.Trim(),
                Subject = subject.Trim(),
                Body = body.Trim(),
                DeliveredAt = deliveredAt ?? DateTimeOffset.Now,
                IsRead = isRead,
                Attachment = attachment
            });

            _lastActionSummary = attachment == null
                ? $"Delivered memo '{subject.Trim()}' from {sender?.Trim() ?? "Maple Admin"}."
                : $"Delivered memo '{subject.Trim()}' with {BuildAttachmentSummary(attachment)}.";
        }

        internal void SetDraftRecipient(string recipient)
        {
            _draft.Recipient = string.IsNullOrWhiteSpace(recipient) ? _draft.Recipient : recipient.Trim();
            _lastActionSummary = $"Draft recipient set to {_draft.Recipient}.";
        }

        internal void SetDraftSubject(string subject)
        {
            if (string.IsNullOrWhiteSpace(subject))
            {
                return;
            }

            _draft.Subject = subject.Trim();
            _lastActionSummary = $"Draft subject set to '{_draft.Subject}'.";
        }

        internal void SetDraftBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            _draft.Body = body.Trim();
            _lastActionSummary = "Draft body updated.";
        }

        internal void ClearDraftAttachment()
        {
            _draft.Attachment = null;
            _lastActionSummary = "Draft attachment cleared.";
        }

        internal void ResetDraftState()
        {
            ResetDraft();
            _lastActionSummary = "Draft reset to the default simulator memo.";
        }

        internal bool SetDraftItemAttachment(int itemId, int quantity, out string message)
        {
            if (itemId <= 0)
            {
                message = "Attachment item ID must be positive.";
                return false;
            }

            if (quantity <= 0)
            {
                quantity = 1;
            }

            _draft.Attachment = new MemoAttachmentState
            {
                Kind = MemoAttachmentKind.Item,
                ItemId = itemId,
                Quantity = quantity
            };

            message = $"Draft attachment set to {BuildAttachmentSummary(_draft.Attachment)}.";
            _lastActionSummary = message;
            return true;
        }

        internal bool SetDraftMesoAttachment(int meso, out string message)
        {
            if (meso <= 0)
            {
                message = "Attached meso must be positive.";
                return false;
            }

            _draft.Attachment = new MemoAttachmentState
            {
                Kind = MemoAttachmentKind.Meso,
                Meso = meso
            };

            message = $"Draft attachment set to {BuildAttachmentSummary(_draft.Attachment)}.";
            _lastActionSummary = message;
            return true;
        }

        internal bool CanClaimAttachment(int memoId)
        {
            MemoState memo = FindMemo(memoId);
            return memo?.Attachment != null && !memo.Attachment.IsClaimed;
        }

        internal bool TryClaimAttachment(int memoId, out string message)
        {
            MemoState memo = FindMemo(memoId);
            if (memo?.Attachment == null)
            {
                message = "This memo does not contain a claimable attachment.";
                return false;
            }

            if (memo.Attachment.IsClaimed)
            {
                message = "This memo attachment has already been claimed.";
                return false;
            }

            memo.Attachment.IsClaimed = true;
            memo.IsRead = true;
            message = $"Claimed {BuildAttachmentSummary(memo.Attachment)} from memo #{memo.MemoId}.";
            _lastActionSummary = message;
            return true;
        }

        internal bool TrySendDraft(out string message)
        {
            if (!CanSendDraft())
            {
                message = "Draft requires recipient, subject, and body before it can be sent.";
                return false;
            }

            string recipient = _draft.Recipient;
            string subject = _draft.Subject;
            string body = _draft.Body;
            MemoAttachmentState attachment = CloneAttachment(_draft.Attachment);

            _lastActionSummary = attachment == null
                ? $"Sent memo to {recipient}."
                : $"Sent memo to {recipient} with {BuildAttachmentSummary(attachment)}.";
            message = _lastActionSummary;

            DeliverMemo(
                "Maple Mail Center",
                $"Delivery receipt: {subject}",
                attachment == null
                    ? $"Your memo to {recipient} was queued through the simulator mailbox."
                    : $"Your memo to {recipient} was queued through the simulator mailbox with {BuildAttachmentSummary(attachment)}.",
                DateTimeOffset.Now,
                isRead: false);

            ResetDraft();
            return true;
        }

        private MemoState FindMemo(int memoId)
        {
            return _memos.FirstOrDefault(memo => memo.MemoId == memoId);
        }

        private void SeedDefaultMemos()
        {
            if (_memos.Count > 0)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.Now;
            DeliverMemo(
                "Maple Admin",
                "Welcome to MapSimulator",
                "This mailbox tracks simulator-owned memos separately from whisper or messenger surfaces. Use it to validate inbox delivery, read state, and note retention flow.",
                now.AddMinutes(-18));
            DeliverMemo(
                "Duey",
                "Package delivery",
                "A simulator-owned attachment was staged so UIWindow2.img/Memo/Get can exercise claim flow instead of stopping at the list window.",
                now.AddMinutes(-14),
                isRead: false,
                attachmentItemId: 2000005,
                attachmentQuantity: 5);
            DeliverMemo(
                "Maple Tip",
                "Travel reminder",
                "Map transfer shortcuts and field transitions can strand parity checks if you do not keep a baseline map handy. Register a safe map before testing social windows across scenes.",
                now.AddMinutes(-9));
            DeliverMemo(
                "Cody",
                "Companion backlog",
                "Pet runtime parity landed first, but memo and mailbox flow still needed its own owner. This note is here so the inbox starts with both read and unread state to exercise the UI.",
                now.AddMinutes(-4),
                isRead: true);
        }

        private bool CanSendDraft()
        {
            return !string.IsNullOrWhiteSpace(_draft.Recipient)
                && !string.IsNullOrWhiteSpace(_draft.Subject)
                && !string.IsNullOrWhiteSpace(_draft.Body);
        }

        private void ResetDraft()
        {
            _draft.Recipient = "Cody";
            _draft.Subject = "Simulator note";
            _draft.Body = "Mailbox draft state is simulator-owned so UIWindow2.img/Memo can be exercised without packet auth.";
            _draft.Attachment = null;
        }

        private static string BuildPreview(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }

            string normalized = body.Replace("\r", " ").Replace("\n", " ").Trim();
            return normalized.Length <= 52
                ? normalized
                : normalized.Substring(0, 49) + "...";
        }

        private static MemoAttachmentState BuildAttachment(int attachmentItemId, int attachmentQuantity, int attachmentMeso)
        {
            if (attachmentItemId > 0)
            {
                return new MemoAttachmentState
                {
                    Kind = MemoAttachmentKind.Item,
                    ItemId = attachmentItemId,
                    Quantity = Math.Max(1, attachmentQuantity)
                };
            }

            if (attachmentMeso > 0)
            {
                return new MemoAttachmentState
                {
                    Kind = MemoAttachmentKind.Meso,
                    Meso = attachmentMeso
                };
            }

            return null;
        }

        private static MemoAttachmentState CloneAttachment(MemoAttachmentState attachment)
        {
            return attachment == null
                ? null
                : new MemoAttachmentState
                {
                    Kind = attachment.Kind,
                    ItemId = attachment.ItemId,
                    Quantity = attachment.Quantity,
                    Meso = attachment.Meso,
                    IsClaimed = attachment.IsClaimed
                };
        }

        private static string BuildAttachmentSummary(MemoAttachmentState attachment)
        {
            if (attachment == null || attachment.Kind == MemoAttachmentKind.None)
            {
                return "No attachment";
            }

            return attachment.Kind switch
            {
                MemoAttachmentKind.Item => $"{ResolveItemName(attachment.ItemId)} x{Math.Max(1, attachment.Quantity)}",
                MemoAttachmentKind.Meso => $"{attachment.Meso:N0} meso",
                _ => "No attachment"
            };
        }

        private static string BuildAttachmentDescription(MemoAttachmentState attachment)
        {
            if (attachment == null)
            {
                return "No package is attached to this memo.";
            }

            return attachment.Kind switch
            {
                MemoAttachmentKind.Item => $"Item package: {ResolveItemName(attachment.ItemId)} x{Math.Max(1, attachment.Quantity)}.",
                MemoAttachmentKind.Meso => $"Meso package: {attachment.Meso:N0} meso.",
                _ => "No package is attached to this memo."
            };
        }

        private static string ResolveItemName(int itemId)
        {
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                   && !string.IsNullOrWhiteSpace(itemInfo.Item2)
                ? itemInfo.Item2
                : $"Item {itemId}";
        }
    }
}
