using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class MemoMailboxManager
    {
        private sealed class MemoState
        {
            public int MemoId { get; init; }
            public string Sender { get; init; } = string.Empty;
            public string Subject { get; init; } = string.Empty;
            public string Body { get; init; } = string.Empty;
            public DateTimeOffset DeliveredAt { get; init; }
            public bool IsRead { get; set; }
            public bool IsKept { get; set; }
        }

        private readonly List<MemoState> _memos = new();
        private int _nextMemoId = 1;

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
                    IsKept = memo.IsKept
                })
                .ToArray();

            return new MemoMailboxSnapshot
            {
                Entries = entries,
                UnreadCount = entries.Count(entry => !entry.IsRead)
            };
        }

        internal void OpenMemo(int memoId)
        {
            MemoState memo = FindMemo(memoId);
            if (memo != null)
            {
                memo.IsRead = true;
            }
        }

        internal void KeepMemo(int memoId)
        {
            MemoState memo = FindMemo(memoId);
            if (memo != null)
            {
                memo.IsRead = true;
                memo.IsKept = true;
            }
        }

        internal void DeleteMemo(int memoId)
        {
            _memos.RemoveAll(memo => memo.MemoId == memoId);
        }

        internal void DeliverMemo(string sender, string subject, string body, DateTimeOffset? deliveredAt = null, bool isRead = false)
        {
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            _memos.Add(new MemoState
            {
                MemoId = _nextMemoId++,
                Sender = string.IsNullOrWhiteSpace(sender) ? "Maple Admin" : sender.Trim(),
                Subject = subject.Trim(),
                Body = body.Trim(),
                DeliveredAt = deliveredAt ?? DateTimeOffset.Now,
                IsRead = isRead
            });
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
    }
}
