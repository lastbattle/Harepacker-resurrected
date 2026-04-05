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

        private const string DefaultDraftRecipient = "Cody";
        private const string DefaultDraftSubject = "Simulator parcel label";
        private const string DefaultDraftBody =
            "Parcel draft state is simulator-owned so UIWindow2.img/Delivery can be exercised without packet auth.";

        private readonly List<MemoState> _memos = new();
        private readonly MemoDraftState _draft = new();
        private ParcelDialogTab _activeTab = ParcelDialogTab.Receive;
        private ParcelComposeMode _composeMode = ParcelComposeMode.Send;
        private bool _showTaxInfo;
        private int _nextMemoId = 1;
        private string _lastActionSummary = "Parcel delivery ready.";
        internal Action<string, int> SocialChatObserved { get; set; }

        internal MemoMailboxManager()
        {
            SeedDefaultParcels();
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
                ActiveTab = _activeTab,
                UnreadCount = entries.Count(entry => !entry.IsRead),
                ClaimableCount = entries.Count(entry => entry.CanClaimAttachment),
                LastActionSummary = _lastActionSummary
            };
        }

        internal MemoMailboxDraftSnapshot GetDraftSnapshot()
        {
            bool hasAttachment = _draft.Attachment != null && _draft.Attachment.Kind != MemoAttachmentKind.None;
            bool isMesoAttachment = _draft.Attachment?.Kind == MemoAttachmentKind.Meso;
            return new MemoMailboxDraftSnapshot
            {
                Recipient = _draft.Recipient,
                Subject = _draft.Subject,
                Body = _draft.Body,
                AttachmentSummary = BuildAttachmentSummary(_draft.Attachment),
                ItemAttachmentSummary = _draft.Attachment?.Kind == MemoAttachmentKind.Item
                    ? BuildAttachmentSummary(_draft.Attachment)
                    : string.Empty,
                LastActionSummary = _lastActionSummary,
                HasAttachment = hasAttachment,
                IsMesoAttachment = isMesoAttachment,
                AttachedMeso = _draft.Attachment?.Kind == MemoAttachmentKind.Meso ? _draft.Attachment.Meso : 0,
                CanSend = CanSendDraft(),
                CanQuickSend = CanQuickSendDraft(),
                ActiveMode = _composeMode,
                ActiveTab = _activeTab,
                AttachmentKind = ResolveDraftAttachmentKind(),
                ShowTaxInfo = _showTaxInfo,
                ModeSummary = BuildModeSummary(),
                TaxSummary = BuildTaxSummary()
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
                _lastActionSummary = $"Opened parcel #{memo.MemoId} from {memo.Sender}.";
            }
        }

        internal void KeepMemo(int memoId)
        {
            MemoState memo = FindMemo(memoId);
            if (memo != null)
            {
                memo.IsRead = true;
                memo.IsKept = true;
                _lastActionSummary = $"Kept parcel #{memo.MemoId} in the receive backlog.";
            }
        }

        internal void DeleteMemo(int memoId)
        {
            if (_memos.RemoveAll(memo => memo.MemoId == memoId) > 0)
            {
                _lastActionSummary = $"Discarded parcel #{memoId}.";
            }
        }

        internal void DeliverMemo(
            string sender,
            string subject,
            string body,
            DateTimeOffset? deliveredAt = null,
            bool isRead = false,
            bool isKept = false,
            int attachmentItemId = 0,
            int attachmentQuantity = 0,
            int attachmentMeso = 0,
            bool isAttachmentClaimed = false)
        {
            DeliverMemo(
                memoId: null,
                sender,
                subject,
                body,
                deliveredAt,
                isRead,
                isKept,
                attachmentItemId,
                attachmentQuantity,
                attachmentMeso,
                isAttachmentClaimed);
        }

        internal void ReplacePacketOwnedParcelSession(
            IReadOnlyList<PacketOwnedParcelDecodedEntry> entries,
            ParcelDialogTab activeTab,
            out string message)
        {
            _memos.Clear();
            _nextMemoId = 1;

            if (entries != null)
            {
                DateTimeOffset displayTime = DateTimeOffset.Now;
                foreach (PacketOwnedParcelDecodedEntry entry in entries)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    DeliverMemo(
                        entry.ParcelSerial > 0 ? entry.ParcelSerial : null,
                        entry.Sender,
                        ResolvePacketOwnedParcelSubject(entry),
                        ResolvePacketOwnedParcelBody(entry),
                        displayTime,
                        isRead: entry.IsRead,
                        isKept: entry.IsKept,
                        attachmentItemId: entry.AttachmentItemId,
                        attachmentQuantity: entry.AttachmentQuantity,
                        attachmentMeso: ResolvePacketOwnedAttachmentMeso(entry),
                        isAttachmentClaimed: entry.IsAttachmentClaimed);
                    displayTime = displayTime.AddSeconds(-1);
                }
            }

            ApplyActiveTab(activeTab, activeTab == ParcelDialogTab.QuickSend
                ? "Packet-owned quick-delivery owner opened."
                : "Packet-owned parcel receive owner opened.");

            message = entries == null || entries.Count == 0
                ? "Applied packet-owned parcel session with no receive rows."
                : $"Applied packet-owned parcel session with {entries.Count} receive row(s).";
            _lastActionSummary = message;
        }

        internal bool TryDeliverDecodedPacketOwnedParcel(PacketOwnedParcelDecodedEntry entry, out string message)
        {
            if (entry == null)
            {
                message = "Packet-owned parcel entry is missing.";
                return false;
            }

            bool delivered = TryDeliverPacketOwnedParcel(
                entry.Sender,
                ResolvePacketOwnedParcelSubject(entry),
                ResolvePacketOwnedParcelBody(entry),
                isRead: entry.IsRead,
                isKept: entry.IsKept,
                isClaimed: entry.IsAttachmentClaimed,
                attachmentItemId: entry.AttachmentItemId,
                attachmentQuantity: entry.AttachmentQuantity,
                attachmentMeso: ResolvePacketOwnedAttachmentMeso(entry),
                memoId: entry.ParcelSerial > 0 ? entry.ParcelSerial : null,
                deliveredAt: DateTimeOffset.Now,
                out message);
            if (delivered && entry.IsQuickDelivery)
            {
                _lastActionSummary = $"{message} (quick delivery)";
                message = _lastActionSummary;
            }

            return delivered;
        }

        private void DeliverMemo(
            int? memoId,
            string sender,
            string subject,
            string body,
            DateTimeOffset? deliveredAt,
            bool isRead,
            bool isKept,
            int attachmentItemId,
            int attachmentQuantity,
            int attachmentMeso,
            bool isAttachmentClaimed)
        {
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            MemoAttachmentState attachment = BuildAttachment(attachmentItemId, attachmentQuantity, attachmentMeso);
            if (attachment != null)
            {
                attachment.IsClaimed = isAttachmentClaimed;
            }

            _memos.Add(new MemoState
            {
                MemoId = ResolveMemoId(memoId),
                Sender = string.IsNullOrWhiteSpace(sender) ? "Maple Delivery Service" : sender.Trim(),
                Subject = subject.Trim(),
                Body = body.Trim(),
                DeliveredAt = deliveredAt ?? DateTimeOffset.Now,
                IsRead = isRead,
                IsKept = isKept,
                Attachment = attachment
            });

            _lastActionSummary = attachment == null
                ? $"Queued parcel '{subject.Trim()}' from {sender?.Trim() ?? "Maple Delivery Service"}."
                : $"Queued parcel '{subject.Trim()}' with {BuildAttachmentSummary(attachment)}.";
        }

        internal void SetDraftRecipient(string recipient)
        {
            SetDraftRecipient(recipient, announce: true);
        }

        internal void SetDraftSubject(string subject)
        {
            SetDraftSubject(subject, announce: true);
        }

        internal void SetDraftBody(string body)
        {
            SetDraftBody(body, announce: true);
        }

        internal void ClearDraftAttachment()
        {
            _draft.Attachment = null;
            _lastActionSummary = "Parcel attachment cleared.";
        }

        internal void ResetDraftState()
        {
            ResetDraft();
            ApplyActiveTab(ParcelDialogTab.Send, "Parcel compose state reset to the default simulator draft.");
        }

        internal void SetActiveTab(ParcelDialogTab tab)
        {
            ApplyActiveTab(
                tab,
                tab switch
                {
                    ParcelDialogTab.Send => "Parcel send tab selected.",
                    ParcelDialogTab.QuickSend => "Quick delivery tab selected.",
                    _ => "Parcel receive tab selected."
                });
        }

        internal void SetComposeMode(ParcelComposeMode mode)
        {
            ApplyActiveTab(
                mode == ParcelComposeMode.QuickSend ? ParcelDialogTab.QuickSend : ParcelDialogTab.Send,
                mode == ParcelComposeMode.QuickSend
                    ? "Quick delivery tab selected."
                    : "Parcel send tab selected.");
        }

        internal void ToggleTaxInfo()
        {
            SetTaxInfoVisible(!_showTaxInfo);
        }

        internal void SetTaxInfoVisible(bool visible)
        {
            _showTaxInfo = visible;
            _lastActionSummary = _showTaxInfo
                ? "Opened parcel fee information."
                : "Closed parcel fee information.";
        }

        internal void ClearParcelSession()
        {
            _memos.Clear();
            _nextMemoId = 1;
            _lastActionSummary = "Cleared the parcel receive session.";
        }

        internal void ResetToSeedParcelSession()
        {
            _memos.Clear();
            _nextMemoId = 1;
            SeedDefaultParcels();
            _lastActionSummary = "Restored the seeded parcel receive session.";
        }

        internal bool TryDeliverPacketOwnedParcel(
            string sender,
            string subject,
            string body,
            bool isRead,
            bool isKept,
            bool isClaimed,
            int attachmentItemId,
            int attachmentQuantity,
            int attachmentMeso,
            out string message)
        {
            return TryDeliverPacketOwnedParcel(
                sender,
                subject,
                body,
                isRead,
                isKept,
                isClaimed,
                attachmentItemId,
                attachmentQuantity,
                attachmentMeso,
                memoId: null,
                deliveredAt: DateTimeOffset.Now,
                out message);
        }

        internal bool TryDeliverPacketOwnedParcel(
            string sender,
            string subject,
            string body,
            bool isRead,
            bool isKept,
            bool isClaimed,
            int attachmentItemId,
            int attachmentQuantity,
            int attachmentMeso,
            int? memoId,
            DateTimeOffset deliveredAt,
            out string message)
        {
            if (string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(body))
            {
                message = "Packet-owned parcel entries require sender and body text.";
                return false;
            }

            string resolvedSubject = string.IsNullOrWhiteSpace(subject)
                ? BuildFallbackParcelLabel(body)
                : subject.Trim();

            DeliverMemo(
                memoId,
                sender.Trim(),
                resolvedSubject,
                body.Trim(),
                deliveredAt,
                isRead,
                isKept,
                attachmentItemId,
                attachmentQuantity,
                attachmentMeso,
                isClaimed);
            message = $"Queued packet-owned parcel '{resolvedSubject}' from {sender.Trim()}.";
            _lastActionSummary = message;
            return true;
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
            return TrySetDraftMesoAttachment(meso, announce: true, out message);
        }

        internal void UpdateDraftRecipientFromUi(string recipient)
        {
            SetDraftRecipient(recipient, announce: false);
        }

        internal void UpdateDraftBodyFromUi(string body)
        {
            SetDraftBody(body, announce: false);
        }

        internal void UpdateDraftMesoFromUi(int meso)
        {
            TrySetDraftMesoAttachment(meso, announce: false, out _);
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
            message = $"Claimed {BuildAttachmentSummary(memo.Attachment)} from parcel #{memo.MemoId}.";
            _lastActionSummary = message;
            return true;
        }

        internal bool TrySendDraft(out string message)
        {
            if (!CanSendDraft())
            {
                message = "Parcel send requires recipient and body text before it can be sent.";
                return false;
            }

            string recipient = _draft.Recipient;
            string subject = ResolveDraftParcelLabel();
            string body = _draft.Body;
            MemoAttachmentState attachment = CloneAttachment(_draft.Attachment);
            _showTaxInfo = false;

            _lastActionSummary = attachment == null
                ? $"Sent parcel to {recipient}."
                : $"Sent parcel to {recipient} with {BuildAttachmentSummary(attachment)}.";
            message = _lastActionSummary;

            DeliverMemo(
                "Maple Mail Center",
                $"Delivery receipt: {subject}",
                attachment == null
                    ? $"Your parcel to {recipient} was queued through the simulator delivery owner."
                    : $"Your parcel to {recipient} was queued through the simulator delivery owner with {BuildAttachmentSummary(attachment)}.",
                DateTimeOffset.Now,
                isRead: false);

            NotifySocialChatObserved(body);
            ResetDraft();
            return true;
        }

        internal bool TryQuickSendDraft(out string message)
        {
            if (!CanQuickSendDraft())
            {
                message = "Quick delivery requires recipient and body, and it cannot carry an item attachment.";
                return false;
            }

            string recipient = _draft.Recipient;
            string body = _draft.Body;
            MemoAttachmentState attachment = CloneAttachment(_draft.Attachment);
            string quickSubject = ResolveDraftParcelLabel("Quick delivery");
            _showTaxInfo = false;

            _lastActionSummary = attachment == null
                ? $"Quick-sent parcel to {recipient}."
                : $"Quick-sent parcel to {recipient} with {BuildAttachmentSummary(attachment)}.";
            message = _lastActionSummary;

            DeliverMemo(
                "Maple Delivery Service",
                $"Parcel receipt: {quickSubject}",
                attachment == null
                    ? $"Your quick parcel notice to {recipient} was staged through the simulator parcel owner."
                    : $"Your quick parcel notice to {recipient} was staged through the simulator parcel owner with {BuildAttachmentSummary(attachment)}.",
                DateTimeOffset.Now,
                isRead: false,
                attachmentMeso: attachment?.Kind == MemoAttachmentKind.Meso ? attachment.Meso : 0);

            NotifySocialChatObserved(body);
            ResetDraft();
            return true;
        }

        internal bool TryDispatchActiveDraft(out string message)
        {
            return _activeTab switch
            {
                ParcelDialogTab.QuickSend => TryQuickSendDraft(out message),
                ParcelDialogTab.Send => TrySendDraft(out message),
                _ => FailActiveDispatch(out message)
            };
        }

        private MemoState FindMemo(int memoId)
        {
            return _memos.FirstOrDefault(memo => memo.MemoId == memoId);
        }

        private void SeedDefaultParcels()
        {
            if (_memos.Count > 0)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.Now;
            DeliverMemo(
                "Maple Delivery Service",
                "Welcome parcel",
                "This receive tab tracks simulator-owned parcel entries separately from whisper or messenger surfaces. Use it to validate parcel selection, receive state, and discard flow.",
                now.AddMinutes(-18));
            DeliverMemo(
                "Duey",
                "Package delivery",
                "A simulator-owned attachment was staged so UIWindow2.img/Delivery can exercise receive flow while the dedicated package popup remains available as a simulator supplement.",
                now.AddMinutes(-14),
                isRead: false,
                isKept: true,
                attachmentItemId: 2000005,
                attachmentQuantity: 5);
            DeliverMemo(
                "Maple Delivery Service",
                "Tax notice",
                "Send and quick-send tabs share the fee popup in the client. Draft edits stay in the parcel owner, with chat commands as supplemental controls.",
                now.AddMinutes(-9));
            DeliverMemo(
                "Cody",
                "Receipt archive",
                "This archived entry keeps both read and kept state alive so the receive tab starts with a small mixed parcel backlog.",
                now.AddMinutes(-4),
                isRead: true,
                isKept: true,
                attachmentMeso: 7500,
                isAttachmentClaimed: true);
        }

        private bool CanSendDraft()
        {
            return !string.IsNullOrWhiteSpace(_draft.Recipient)
                && !string.IsNullOrWhiteSpace(_draft.Body);
        }

        private bool CanQuickSendDraft()
        {
            return !string.IsNullOrWhiteSpace(_draft.Recipient)
                && !string.IsNullOrWhiteSpace(_draft.Body)
                && _draft.Attachment?.Kind != MemoAttachmentKind.Item;
        }

        private void ResetDraft()
        {
            _draft.Recipient = DefaultDraftRecipient;
            _draft.Subject = DefaultDraftSubject;
            _draft.Body = DefaultDraftBody;
            _draft.Attachment = null;
        }

        private void SetDraftRecipient(string recipient, bool announce)
        {
            _draft.Recipient = recipient?.Trim() ?? string.Empty;
            if (announce)
            {
                _lastActionSummary = string.IsNullOrWhiteSpace(_draft.Recipient)
                    ? "Parcel target cleared."
                    : $"Parcel target set to {_draft.Recipient}.";
            }
        }

        private void SetDraftSubject(string subject, bool announce)
        {
            _draft.Subject = subject?.Trim() ?? string.Empty;
            if (announce)
            {
                _lastActionSummary = string.IsNullOrWhiteSpace(_draft.Subject)
                    ? "Parcel label cleared."
                    : $"Parcel label set to '{_draft.Subject}'.";
            }
        }

        private void SetDraftBody(string body, bool announce)
        {
            string normalizedBody = body?.Replace("\r\n", "\n").Replace('\r', '\n') ?? string.Empty;
            _draft.Body = normalizedBody;
            if (announce)
            {
                _lastActionSummary = string.IsNullOrWhiteSpace(_draft.Body)
                    ? "Draft body cleared."
                    : "Draft body updated.";
            }
        }

        private bool TrySetDraftMesoAttachment(int meso, bool announce, out string message)
        {
            if (meso <= 0)
            {
                if (_draft.Attachment?.Kind == MemoAttachmentKind.Meso)
                {
                    _draft.Attachment = null;
                }

                message = announce
                    ? "Attached meso must be positive."
                    : string.Empty;
                if (announce)
                {
                    _lastActionSummary = "Parcel meso attachment cleared.";
                }

                return !announce && meso == 0;
            }

            _draft.Attachment = new MemoAttachmentState
            {
                Kind = MemoAttachmentKind.Meso,
                Meso = meso
            };

            message = $"Draft attachment set to {BuildAttachmentSummary(_draft.Attachment)}.";
            if (announce)
            {
                _lastActionSummary = message;
            }

            return true;
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

        private static string ResolvePacketOwnedParcelSubject(PacketOwnedParcelDecodedEntry entry)
        {
            if (entry == null)
            {
                return "Parcel delivery";
            }

            if (entry.IsQuickDelivery)
            {
                return "Quick delivery";
            }

            return BuildFallbackParcelLabel(entry.MemoText);
        }

        private static string ResolvePacketOwnedParcelBody(PacketOwnedParcelDecodedEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            string memoText = string.IsNullOrWhiteSpace(entry.MemoText)
                ? "Packet-owned parcel payload did not include memo text."
                : entry.MemoText.Trim();
            if (entry.HasUndecodedItemAttachment)
            {
                memoText += "\n\nAttachment: item payload present but the exact GW_ItemSlotBase body was not fully decoded here.";
            }

            if (entry.AttachmentItemId > 0 && entry.AttachmentMeso > 0)
            {
                memoText += $"\n\nAttachment summary: {ResolveItemName(entry.AttachmentItemId)} x{Math.Max(1, entry.AttachmentQuantity)}, {entry.AttachmentMeso:N0} meso.";
            }

            return memoText;
        }

        private static int ResolvePacketOwnedAttachmentMeso(PacketOwnedParcelDecodedEntry entry)
        {
            if (entry == null)
            {
                return 0;
            }

            return entry.AttachmentItemId > 0 && entry.AttachmentMeso > 0
                ? 0
                : Math.Max(0, entry.AttachmentMeso);
        }

        private int ResolveMemoId(int? requestedMemoId)
        {
            if (!requestedMemoId.HasValue || requestedMemoId.Value <= 0 || _memos.Any(memo => memo.MemoId == requestedMemoId.Value))
            {
                return _nextMemoId++;
            }

            _nextMemoId = Math.Max(_nextMemoId, requestedMemoId.Value + 1);
            return requestedMemoId.Value;
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

        private string BuildModeSummary()
        {
            return _activeTab switch
            {
                ParcelDialogTab.QuickSend =>
                    "Quick Send tab: recipient and body are required; the simulator keeps item parcels disabled here to match the client split.",
                ParcelDialogTab.Receive =>
                    "Receive tab: delivered parcel state stays separate from memo, whisper, and messenger surfaces.",
                _ =>
                    "Send tab: recipient and body are required; optional item or meso packages still flow through the simulator draft seam."
            };
        }

        private string BuildTaxSummary()
        {
            return _activeTab switch
            {
                ParcelDialogTab.QuickSend =>
                    "Quick-delivery fee info is informational only here. Use the owner meso field or /memo draft meso <amount> for supplemental staging.",
                _ =>
                    "Parcel fee info is informational only here. Use the owner package lane and meso field, or /memo draft ... for supplemental staging."
            };
        }

        private MemoDraftAttachmentKind ResolveDraftAttachmentKind()
        {
            return _draft.Attachment?.Kind switch
            {
                MemoAttachmentKind.Item => MemoDraftAttachmentKind.Item,
                MemoAttachmentKind.Meso => MemoDraftAttachmentKind.Meso,
                _ => MemoDraftAttachmentKind.None
            };
        }

        private static bool FailActiveDispatch(out string message)
        {
            message = "Receive tab parcels cannot be dispatched. Switch to Send or Quick Send.";
            return false;
        }

        private void ApplyActiveTab(ParcelDialogTab tab, string actionSummary)
        {
            _activeTab = tab;
            _composeMode = tab == ParcelDialogTab.QuickSend
                ? ParcelComposeMode.QuickSend
                : ParcelComposeMode.Send;
            _showTaxInfo = false;
            _lastActionSummary = actionSummary;
        }

        private string ResolveDraftParcelLabel(string fallback = "Parcel delivery")
        {
            return string.IsNullOrWhiteSpace(_draft.Subject)
                ? fallback
                : _draft.Subject.Trim();
        }

        private static string BuildFallbackParcelLabel(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return "Parcel delivery";
            }

            string normalized = body.Replace("\r", " ").Replace("\n", " ").Trim();
            return normalized.Length <= 24
                ? normalized
                : normalized.Substring(0, 21) + "...";
        }

        private static string ResolveItemName(int itemId)
        {
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                   && !string.IsNullOrWhiteSpace(itemInfo.Item2)
                ? itemInfo.Item2
                : $"Item {itemId}";
        }

        private void NotifySocialChatObserved(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            SocialChatObserved?.Invoke(text.Trim(), Environment.TickCount);
        }
    }
}
