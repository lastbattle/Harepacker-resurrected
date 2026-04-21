using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class MemoMailboxManager
    {
        private const int StringPoolQuickDeliveryCouponMissing = 0xF5E;
        private const int StringPoolAttachmentRequired = 0xF5F;
        private const int StringPoolRecipientRequired = 0xF60;
        private const int StringPoolQuickSendFeePrompt = 0xF61;
        private const int StringPoolSendFeePrompt = 0xF62;
        private const int StringPoolClaimSuccess = 0xF64;
        private const int StringPoolDiscardResult = 0xF65;
        private const int StringPoolSendSuccess = 0xF66;
        private const int StringPoolQuickDeliveryDefaultMemo = 0xF57;
        private const int StringPoolClaimWindowUnset = 0xF53;
        private const int StringPoolClaimWindowExpired = 0xF54;
        private const int StringPoolClaimWindowDaysRemaining = 0x1A17;
        private const int MaxPacketOwnedReceiveRows = 10;

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
            public bool HasClientTimestamp { get; init; }
            public DateTimeOffset? ExpirationTimestampUtc { get; init; }
            public bool IsRead { get; set; }
            public bool IsKept { get; set; }
            public bool IsQuickDelivery { get; init; }
            public MemoAttachmentState Attachment { get; init; }
        }

        private const string DefaultDraftRecipient = "Cody";
        private const string DefaultDraftSubject = "Simulator parcel label";
        private const string DefaultDraftBody =
            "Parcel draft state is simulator-owned so UIWindow2.img/Delivery can be exercised without packet auth.";

        private readonly List<MemoState> _memos = new();
        private readonly HashSet<string> _packetOwnedSessionEntrySignatures = new(StringComparer.Ordinal);
        private readonly MemoDraftState _draft = new();
        private ParcelDialogTab _activeTab = ParcelDialogTab.Receive;
        private ParcelDialogTabAvailability _availableTabs = ParcelDialogTabAvailability.All;
        private ParcelComposeMode _composeMode = ParcelComposeMode.Send;
        private bool _awaitingItemSelection;
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
                    DeliveredAtText = FormatTimestamp(memo.DeliveredAt),
                    ClaimDeadlineText = FormatClaimDeadline(memo.ExpirationTimestampUtc),
                    ClaimDeadlineStatusText = BuildClaimDeadlineStatusText(memo),
                    StatusText = BuildStatusText(memo),
                    IsRead = memo.IsRead,
                    IsKept = memo.IsKept,
                    IsQuickDelivery = memo.IsQuickDelivery,
                    HasAttachment = memo.Attachment != null,
                    CanClaimAttachment = CanClaimAttachment(memo),
                    IsAttachmentClaimed = memo.Attachment?.IsClaimed == true,
                    IsExpired = IsExpired(memo),
                    AttachmentSummary = BuildAttachmentSummary(memo.Attachment)
                })
                .ToArray();

            return new MemoMailboxSnapshot
            {
                Entries = entries,
                ActiveTab = _activeTab,
                AvailableTabs = _availableTabs,
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
                AwaitingItemSelection = _awaitingItemSelection,
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
                DeliveredAtText = FormatTimestamp(memo.DeliveredAt),
                ClaimDeadlineText = FormatClaimDeadline(memo.ExpirationTimestampUtc),
                ClaimDeadlineStatusText = BuildClaimDeadlineStatusText(memo),
                StatusText = BuildStatusText(memo),
                AttachmentSummary = BuildAttachmentSummary(memo.Attachment),
                AttachmentDescription = BuildAttachmentDescription(memo),
                AttachmentItemId = GetAttachmentItemId(memo.Attachment),
                AttachmentQuantity = GetAttachmentQuantity(memo.Attachment),
                AttachmentMeso = GetAttachmentMeso(memo.Attachment),
                CanClaim = CanClaimAttachment(memo),
                IsClaimed = memo.Attachment?.IsClaimed == true,
                IsExpired = IsExpired(memo)
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
            bool isAttachmentClaimed = false,
            bool notifySocialText = true)
        {
            DeliverMemo(
                memoId: null,
                sender,
                subject,
                body,
                deliveredAt,
                isRead,
                isKept,
                attachmentItemId: attachmentItemId,
                attachmentQuantity: attachmentQuantity,
                attachmentMeso: attachmentMeso,
                isQuickDelivery: false,
                isAttachmentClaimed: isAttachmentClaimed,
                notifySocialText: notifySocialText);
        }

        internal void ReplacePacketOwnedParcelSession(
            IReadOnlyList<PacketOwnedParcelDecodedEntry> entries,
            ParcelDialogTabAvailability availableTabs,
            ParcelDialogTab activeTab,
            out string message)
        {
            HashSet<string> previousSignatures = new(_packetOwnedSessionEntrySignatures, StringComparer.Ordinal);
            var refreshedSignatures = new HashSet<string>(StringComparer.Ordinal);
            var newlyHydratedBodies = new List<string>();

            _memos.Clear();
            _nextMemoId = 1;

            int skippedRows = 0;
            if (entries != null)
            {
                DateTimeOffset displayTime = DateTimeOffset.Now;
                foreach (PacketOwnedParcelDecodedEntry entry in entries)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    if (_memos.Count >= MaxPacketOwnedReceiveRows)
                    {
                        skippedRows++;
                        continue;
                    }

                    int attachmentMeso = ResolvePacketOwnedAttachmentMeso(entry);
                    string resolvedSubject = ResolvePacketOwnedParcelSubject(entry);
                    string resolvedBody = ResolvePacketOwnedParcelBody(entry);
                    string signature = BuildPacketOwnedParcelEntrySignature(
                        entry.ParcelSerial,
                        entry.Sender,
                        resolvedSubject,
                        resolvedBody,
                        entry.IsRead,
                        entry.IsKept,
                        entry.IsAttachmentClaimed,
                        entry.StateFlags,
                        entry.IsQuickDelivery,
                        entry.QuickDeliveryReservedBytes,
                        entry.AttachmentItemId,
                        entry.AttachmentQuantity,
                        attachmentMeso,
                        entry.ExpirationTimestampUtc);
                    refreshedSignatures.Add(signature);

                    DeliverMemo(
                        entry.ParcelSerial > 0 ? entry.ParcelSerial : null,
                        entry.Sender,
                        resolvedSubject,
                        resolvedBody,
                        displayTime,
                        isRead: entry.IsRead,
                        isKept: entry.IsKept,
                        isQuickDelivery: entry.IsQuickDelivery,
                        attachmentItemId: entry.AttachmentItemId,
                        attachmentQuantity: entry.AttachmentQuantity,
                        attachmentMeso: attachmentMeso,
                        isAttachmentClaimed: entry.IsAttachmentClaimed,
                        expirationTimestampUtc: entry.ExpirationTimestampUtc,
                        notifySocialText: false);

                    if (!previousSignatures.Contains(signature)
                        && !string.IsNullOrWhiteSpace(resolvedBody))
                    {
                        newlyHydratedBodies?.Add(resolvedBody.Trim());
                    }

                    displayTime = displayTime.AddSeconds(-1);
                }
            }

            _packetOwnedSessionEntrySignatures.Clear();
            foreach (string signature in refreshedSignatures)
            {
                _packetOwnedSessionEntrySignatures.Add(signature);
            }

            if (newlyHydratedBodies.Count > 0)
            {
                foreach (string body in newlyHydratedBodies)
                {
                    NotifySocialChatObserved(body);
                }
            }

            ApplyTabAvailability(availableTabs);
            ApplyActiveTab(activeTab, activeTab == ParcelDialogTab.QuickSend
                ? "Packet-owned quick-delivery owner opened."
                : "Packet-owned parcel receive owner opened.");

            message = entries == null || entries.Count == 0
                ? "Applied packet-owned parcel session with no receive rows."
                : skippedRows > 0
                    ? $"Applied packet-owned parcel session with {_memos.Count} receive row(s) and skipped {skippedRows} row(s) beyond the client-shaped 10-row backlog."
                    : $"Applied packet-owned parcel session with {_memos.Count} receive row(s).";
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
                isQuickDelivery: entry.IsQuickDelivery,
                isClaimed: entry.IsAttachmentClaimed,
                attachmentItemId: entry.AttachmentItemId,
                attachmentQuantity: entry.AttachmentQuantity,
                attachmentMeso: ResolvePacketOwnedAttachmentMeso(entry),
                memoId: entry.ParcelSerial > 0 ? entry.ParcelSerial : null,
                deliveredAt: DateTimeOffset.Now,
                expirationTimestampUtc: entry.ExpirationTimestampUtc,
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
            bool isQuickDelivery,
            bool isAttachmentClaimed,
            DateTimeOffset? expirationTimestampUtc = null,
            bool notifySocialText = false)
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
                HasClientTimestamp = deliveredAt.HasValue,
                ExpirationTimestampUtc = expirationTimestampUtc,
                IsRead = isRead,
                IsKept = isKept,
                IsQuickDelivery = isQuickDelivery,
                Attachment = attachment
            });

            _lastActionSummary = attachment == null
                ? $"Queued parcel '{subject.Trim()}' from {sender?.Trim() ?? "Maple Delivery Service"}."
                : $"Queued parcel '{subject.Trim()}' with {BuildAttachmentSummary(attachment)}.";

            if (notifySocialText)
            {
                NotifySocialChatObserved(body);
            }
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
            _awaitingItemSelection = false;
            _lastActionSummary = "Parcel attachment cleared.";
        }

        internal void BeginDraftItemSelection()
        {
            _awaitingItemSelection = true;
            _showTaxInfo = false;
            _lastActionSummary = "Parcel item picker armed. Select an inventory slot to stage it into the send tab.";
        }

        internal void CancelDraftItemSelection()
        {
            if (!_awaitingItemSelection)
            {
                return;
            }

            _awaitingItemSelection = false;
            _lastActionSummary = "Cancelled parcel item picker.";
        }

        internal void ResetDraftState()
        {
            ResetDraft();
            ApplyActiveTab(ParcelDialogTab.Send, "Parcel compose state reset to the default simulator draft.");
        }

        internal void SetActiveTab(ParcelDialogTab tab)
        {
            if (!IsTabAvailable(tab))
            {
                tab = ResolveFallbackActiveTab();
            }

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
            _packetOwnedSessionEntrySignatures.Clear();
            ResetTabAvailability();
            _lastActionSummary = "Cleared the parcel receive session.";
        }

        internal void ResetToSeedParcelSession()
        {
            _memos.Clear();
            _nextMemoId = 1;
            _packetOwnedSessionEntrySignatures.Clear();
            ResetTabAvailability();
            SeedDefaultParcels();
            _lastActionSummary = "Restored the seeded parcel receive session.";
        }

        internal void ApplyPacketOwnedDialogMode(int dialogMode)
        {
            ParcelDialogTabAvailability availability = dialogMode switch
            {
                1 => ParcelDialogTabAvailability.QuickSend,
                2 => ParcelDialogTabAvailability.Receive,
                _ => ParcelDialogTabAvailability.Receive | ParcelDialogTabAvailability.Send
            };

            ApplyTabAvailability(availability);
            SetActiveTab(dialogMode switch
            {
                1 => ParcelDialogTab.QuickSend,
                _ => ParcelDialogTab.Receive
            });
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
                isQuickDelivery: false,
                isClaimed: isClaimed,
                attachmentItemId: attachmentItemId,
                attachmentQuantity: attachmentQuantity,
                attachmentMeso: attachmentMeso,
                out message);
        }

        internal bool TryDeliverPacketOwnedParcel(
            string sender,
            string subject,
            string body,
            bool isRead,
            bool isKept,
            bool isQuickDelivery,
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
                isQuickDelivery,
                isClaimed,
                attachmentItemId,
                attachmentQuantity,
                attachmentMeso,
                memoId: null,
                deliveredAt: DateTimeOffset.Now,
                expirationTimestampUtc: null,
                out message);
        }

        internal bool TryDeliverPacketOwnedParcel(
            string sender,
            string subject,
            string body,
            bool isRead,
            bool isKept,
            bool isQuickDelivery,
            bool isClaimed,
            int attachmentItemId,
            int attachmentQuantity,
            int attachmentMeso,
            int? memoId,
            DateTimeOffset deliveredAt,
            DateTimeOffset? expirationTimestampUtc,
            out string message)
        {
            if (string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(body))
            {
                message = "Packet-owned parcel entries require sender and body text.";
                return false;
            }

            if (_memos.Count >= MaxPacketOwnedReceiveRows)
            {
                message = $"Ignored packet-owned parcel '{(string.IsNullOrWhiteSpace(subject) ? BuildFallbackParcelLabel(body) : subject.Trim())}' because the receive owner already has the client-shaped 10-row backlog filled.";
                _lastActionSummary = message;
                return true;
            }

            string resolvedSubject = string.IsNullOrWhiteSpace(subject)
                ? BuildFallbackParcelLabel(body)
                : subject.Trim();

            DeliverMemo(
                memoId: memoId,
                sender: sender.Trim(),
                subject: resolvedSubject,
                body: body.Trim(),
                deliveredAt: deliveredAt,
                isRead: isRead,
                isKept: isKept,
                attachmentItemId: attachmentItemId,
                attachmentQuantity: attachmentQuantity,
                attachmentMeso: attachmentMeso,
                isQuickDelivery: isQuickDelivery,
                isAttachmentClaimed: isClaimed,
                expirationTimestampUtc: expirationTimestampUtc,
                notifySocialText: true);
            _packetOwnedSessionEntrySignatures.Add(BuildPacketOwnedParcelEntrySignature(
                memoId ?? 0,
                sender,
                resolvedSubject,
                body,
                isRead,
                isKept,
                isClaimed,
                stateFlags: 0,
                isQuickDelivery,
                quickDeliveryReservedBytes: Array.Empty<byte>(),
                attachmentItemId,
                attachmentQuantity,
                attachmentMeso,
                expirationTimestampUtc));
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
            _awaitingItemSelection = false;

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
            return CanClaimAttachment(memo);
        }

        internal bool TryClaimAttachment(int memoId, out string message)
        {
            return TryClaimAttachment(memoId, out _, out message);
        }

        internal bool TryClaimAttachment(int memoId, out MemoMailboxClaimResult claimResult, out string message)
        {
            claimResult = null;
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

            if (IsExpired(memo))
            {
                message = memo.ExpirationTimestampUtc.HasValue
                    ? $"This parcel package expired on {memo.ExpirationTimestampUtc.Value.ToLocalTime():yyyy.MM.dd HH:mm}."
                    : "This parcel package can no longer be claimed.";
                return false;
            }

            if (!IsWithinClientClaimWindow(memo))
            {
                if (TryGetClientClaimWindowDaysRemaining(memo, out int daysRemaining))
                {
                    message = daysRemaining >= 30
                        ? MapleStoryStringPool.GetOrFallback(
                            StringPoolClaimWindowUnset,
                            "This parcel package cannot be claimed yet because it is outside the receive window.")
                        : "This parcel package cannot be claimed right now.";
                }
                else
                {
                    message = "This parcel package cannot be claimed right now.";
                }

                return false;
            }

            claimResult = new MemoMailboxClaimResult
            {
                MemoId = memo.MemoId,
                AttachmentItemId = GetAttachmentItemId(memo.Attachment),
                AttachmentQuantity = GetAttachmentQuantity(memo.Attachment),
                AttachmentMeso = GetAttachmentMeso(memo.Attachment),
                AttachmentSummary = BuildAttachmentSummary(memo.Attachment)
            };
            memo.Attachment.IsClaimed = true;
            memo.IsRead = true;
            message = MapleStoryStringPool.GetOrFallback(
                StringPoolClaimSuccess,
                $"Claimed {BuildAttachmentSummary(memo.Attachment)} from parcel #{memo.MemoId}.");
            _lastActionSummary = message;
            return true;
        }

        internal bool TrySendDraft(out string message)
        {
            if (string.IsNullOrWhiteSpace(_draft.Recipient))
            {
                message = MapleStoryStringPool.GetOrFallback(
                    StringPoolRecipientRequired,
                    "Please enter the name of the recipient.");
                return false;
            }

            if (!HasDraftAttachment())
            {
                message = MapleStoryStringPool.GetOrFallback(
                    StringPoolAttachmentRequired,
                    "You must select either Mesos or items to send.");
                return false;
            }

            string recipient = _draft.Recipient;
            string subject = ResolveDraftParcelLabel();
            MemoAttachmentState attachment = CloneAttachment(_draft.Attachment);
            _showTaxInfo = false;

            message = MapleStoryStringPool.GetOrFallback(
                StringPoolSendSuccess,
                attachment == null
                    ? $"Sent parcel to {recipient}."
                    : $"Sent parcel to {recipient} with {BuildAttachmentSummary(attachment)}.");
            _lastActionSummary = message;

            DeliverMemo(
                "Maple Mail Center",
                $"Delivery receipt: {subject}",
                attachment == null
                    ? $"Your parcel to {recipient} was sent through the simulator delivery owner."
                    : $"Your parcel to {recipient} was sent through the simulator delivery owner with {BuildAttachmentSummary(attachment)}.",
                DateTimeOffset.Now,
                isRead: false,
                notifySocialText: false);

            ResetDraft();
            return true;
        }

        internal bool TryQuickSendDraft(bool hasQuickDeliveryCoupon, Func<bool> consumeQuickDeliveryCoupon, out string message)
        {
            if (!hasQuickDeliveryCoupon)
            {
                message = MapleStoryStringPool.GetOrFallback(
                    StringPoolQuickDeliveryCouponMissing,
                    "You do not have the Quick Delivery Coupon.");
                return false;
            }

            if (!CanQuickSendDraft())
            {
                message = string.IsNullOrWhiteSpace(_draft.Recipient)
                    ? MapleStoryStringPool.GetOrFallback(
                        StringPoolRecipientRequired,
                        "Please enter the name of the recipient.")
                    : string.IsNullOrWhiteSpace(_draft.Body)
                        ? MapleStoryStringPool.GetOrFallback(
                            0x11D,
                            "Please enter the message to send")
                        : MapleStoryStringPool.GetOrFallback(
                            StringPoolAttachmentRequired,
                            "You must select either Mesos or items to send.");
                return false;
            }

            if (consumeQuickDeliveryCoupon != null && !consumeQuickDeliveryCoupon())
            {
                message = MapleStoryStringPool.GetOrFallback(
                    StringPoolQuickDeliveryCouponMissing,
                    "You do not have the Quick Delivery Coupon.");
                return false;
            }

            string recipient = _draft.Recipient;
            string body = _draft.Body;
            MemoAttachmentState attachment = CloneAttachment(_draft.Attachment);
            string quickSubject = ResolveDraftParcelLabel("Quick delivery");
            _showTaxInfo = false;

            message = MapleStoryStringPool.GetOrFallback(
                StringPoolSendSuccess,
                attachment == null
                    ? $"Quick-sent parcel to {recipient}."
                    : $"Quick-sent parcel to {recipient} with {BuildAttachmentSummary(attachment)}.");
            _lastActionSummary = message;

            DeliverMemo(
                "Maple Delivery Service",
                $"Parcel receipt: {quickSubject}",
                attachment == null
                    ? $"Your quick parcel notice to {recipient} was staged through the simulator parcel owner."
                    : $"Your quick parcel notice to {recipient} was staged through the simulator parcel owner with {BuildAttachmentSummary(attachment)}.",
                DateTimeOffset.Now,
                isRead: false,
                attachmentMeso: attachment?.Kind == MemoAttachmentKind.Meso ? attachment.Meso : 0,
                notifySocialText: false);

            NotifySocialChatObserved(body);
            ResetDraft();
            return true;
        }

        internal bool TryDispatchActiveDraft(bool hasQuickDeliveryCoupon, Func<bool> consumeQuickDeliveryCoupon, out string message)
        {
            return _activeTab switch
            {
                ParcelDialogTab.QuickSend => TryQuickSendDraft(hasQuickDeliveryCoupon, consumeQuickDeliveryCoupon, out message),
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
                now.AddMinutes(-18),
                notifySocialText: false);
            DeliverMemo(
                "Duey",
                "Package delivery",
                "A simulator-owned attachment was staged so UIWindow2.img/Delivery can exercise receive flow while the dedicated package popup remains available as a simulator supplement.",
                now.AddMinutes(-14),
                isRead: false,
                isKept: true,
                attachmentItemId: 2000005,
                attachmentQuantity: 5,
                notifySocialText: false);
            DeliverMemo(
                "Maple Delivery Service",
                "Tax notice",
                "Send and quick-send tabs share the fee popup in the client. Draft edits stay in the parcel owner, with chat commands as supplemental controls.",
                now.AddMinutes(-9),
                notifySocialText: false);
            DeliverMemo(
                "Cody",
                "Receipt archive",
                "This archived entry keeps both read and kept state alive so the receive tab starts with a small mixed parcel backlog.",
                now.AddMinutes(-4),
                isRead: true,
                isKept: true,
                attachmentMeso: 7500,
                isAttachmentClaimed: true,
                notifySocialText: false);
        }

        private bool CanSendDraft()
        {
            return !string.IsNullOrWhiteSpace(_draft.Recipient)
                && HasDraftAttachment();
        }

        private bool CanQuickSendDraft()
        {
            return !string.IsNullOrWhiteSpace(_draft.Recipient)
                && !string.IsNullOrWhiteSpace(_draft.Body)
                && _draft.Attachment?.Kind == MemoAttachmentKind.Meso
                && _draft.Attachment.Meso > 0;
        }

        private void ResetDraft()
        {
            _draft.Recipient = DefaultDraftRecipient;
            _draft.Subject = DefaultDraftSubject;
            _draft.Body = DefaultDraftBody;
            _draft.Attachment = null;
            _awaitingItemSelection = false;
        }

        private void ResetTabAvailability()
        {
            _availableTabs = ParcelDialogTabAvailability.All;
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
            _awaitingItemSelection = false;

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
            if (attachmentItemId > 0 || attachmentMeso > 0)
            {
                return new MemoAttachmentState
                {
                    Kind = attachmentItemId > 0 ? MemoAttachmentKind.Item : MemoAttachmentKind.Meso,
                    ItemId = Math.Max(0, attachmentItemId),
                    Quantity = attachmentItemId > 0 ? Math.Max(1, attachmentQuantity) : 0,
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

            bool hasItem = HasItemAttachment(attachment);
            bool hasMeso = HasMesoAttachment(attachment);
            if (hasItem && hasMeso)
            {
                return $"{ResolveItemName(attachment.ItemId)} x{Math.Max(1, attachment.Quantity)}, {attachment.Meso:N0} meso";
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
                ? entry.IsQuickDelivery
                    ? MapleStoryStringPool.GetOrFallback(
                        StringPoolQuickDeliveryDefaultMemo,
                        "The received package can be obtained through the Quick Delivery NPC.")
                    : "Packet-owned parcel payload did not include memo text."
                : entry.MemoText.Trim();
            if (entry.HasUndecodedItemAttachment)
            {
                memoText += "\n\nAttachment: item payload present but the exact GW_ItemSlotBase body was not fully decoded here.";
            }

            if (entry.AttachmentItemId > 0 && entry.AttachmentMeso > 0)
            {
                memoText += $"\n\nAttachment summary: {ResolveItemName(entry.AttachmentItemId)} x{Math.Max(1, entry.AttachmentQuantity)}, {entry.AttachmentMeso:N0} meso.";
            }
            else if (entry.AttachmentItemId > 0)
            {
                memoText += $"\n\nAttachment summary: {ResolveItemName(entry.AttachmentItemId)} x{Math.Max(1, entry.AttachmentQuantity)}.";
            }
            else if (entry.HasMesoAttachment && entry.AttachmentMeso > 0)
            {
                memoText += $"\n\nAttachment summary: {entry.AttachmentMeso:N0} meso.";
            }

            if (entry.ExpirationTimestampUtc.HasValue)
            {
                memoText += $"\n\nClaim deadline: {entry.ExpirationTimestampUtc.Value.ToLocalTime():yyyy.MM.dd HH:mm}.";
            }

            if (entry.HasQuickDeliveryReservedState)
            {
                memoText += $"\n\nQuick-delivery internal bytes: {FormatByteHex(entry.QuickDeliveryReservedBytes)}.";
            }

            return memoText;
        }

        private static int ResolvePacketOwnedAttachmentMeso(PacketOwnedParcelDecodedEntry entry)
        {
            if (entry == null)
            {
                return 0;
            }

            return Math.Max(0, entry.AttachmentMeso);
        }

        private static bool HasItemAttachment(MemoAttachmentState attachment)
        {
            return attachment?.ItemId > 0 && attachment.Quantity > 0;
        }

        private static bool HasMesoAttachment(MemoAttachmentState attachment)
        {
            return attachment?.Meso > 0;
        }

        private static int GetAttachmentItemId(MemoAttachmentState attachment)
        {
            return HasItemAttachment(attachment) ? attachment.ItemId : 0;
        }

        private static int GetAttachmentQuantity(MemoAttachmentState attachment)
        {
            return HasItemAttachment(attachment) ? attachment.Quantity : 0;
        }

        private static int GetAttachmentMeso(MemoAttachmentState attachment)
        {
            return HasMesoAttachment(attachment) ? attachment.Meso : 0;
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

        private static string BuildAttachmentDescription(MemoState memo)
        {
            MemoAttachmentState attachment = memo?.Attachment;
            if (attachment == null)
            {
                return "No package is attached to this memo.";
            }

            bool hasItem = HasItemAttachment(attachment);
            bool hasMeso = HasMesoAttachment(attachment);
            string description = hasItem && hasMeso
                ? $"Item package: {ResolveItemName(attachment.ItemId)} x{Math.Max(1, attachment.Quantity)} with {attachment.Meso:N0} meso."
                : attachment.Kind switch
                {
                    MemoAttachmentKind.Item => $"Item package: {ResolveItemName(attachment.ItemId)} x{Math.Max(1, attachment.Quantity)}.",
                    MemoAttachmentKind.Meso => $"Meso package: {attachment.Meso:N0} meso.",
                    _ => "No package is attached to this memo."
                };

            if (memo?.ExpirationTimestampUtc.HasValue == true)
            {
                description += IsExpired(memo)
                    ? $" Claim deadline passed at {memo.ExpirationTimestampUtc.Value.ToLocalTime():yyyy.MM.dd HH:mm}."
                    : $" Claim deadline: {memo.ExpirationTimestampUtc.Value.ToLocalTime():yyyy.MM.dd HH:mm}.";

                string deadlineStatus = BuildClaimDeadlineStatusText(memo);
                if (!string.IsNullOrWhiteSpace(deadlineStatus))
                {
                    description += $" ({deadlineStatus})";
                }
            }

            return description;
        }

        private static string FormatTimestamp(DateTimeOffset deliveredAt)
        {
            return deliveredAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm");
        }

        private static string FormatClaimDeadline(DateTimeOffset? expirationTimestampUtc)
        {
            return expirationTimestampUtc.HasValue
                ? expirationTimestampUtc.Value.ToLocalTime().ToString("yyyy.MM.dd HH:mm")
                : string.Empty;
        }

        private static string BuildStatusText(MemoState memo)
        {
            if (memo == null)
            {
                return string.Empty;
            }

            if (memo.Attachment?.IsClaimed == true)
            {
                return memo.IsQuickDelivery ? "Quick claimed" : "Claimed";
            }

            if (memo.ExpirationTimestampUtc.HasValue
                && IsExpired(memo))
            {
                return "Expired";
            }

            if (memo.Attachment != null)
            {
                return memo.IsQuickDelivery ? "Quick package" : "Package attached";
            }

            if (memo.IsQuickDelivery)
            {
                return "Quick delivery";
            }

            if (memo.IsKept)
            {
                return "Kept";
            }

            return memo.IsRead ? "Read" : "Unread";
        }

        private static bool CanClaimAttachment(MemoState memo)
        {
            return memo?.Attachment != null
                && !memo.Attachment.IsClaimed
                && IsWithinClientClaimWindow(memo)
                && !IsExpired(memo);
        }

        private static bool IsExpired(MemoState memo)
        {
            return memo?.ExpirationTimestampUtc.HasValue == true
                && memo.ExpirationTimestampUtc.Value.ToLocalTime() <= DateTimeOffset.Now;
        }

        private static bool IsWithinClientClaimWindow(MemoState memo)
        {
            if (!TryGetClientClaimWindowDaysRemaining(memo, out int daysRemaining))
            {
                return true;
            }

            return daysRemaining >= 1 && daysRemaining < 30;
        }

        private static bool TryGetClientClaimWindowDaysRemaining(MemoState memo, out int daysRemaining)
        {
            daysRemaining = 0;
            if (memo?.ExpirationTimestampUtc.HasValue != true)
            {
                return false;
            }

            // CTabReceive computes this as a signed FILETIME delta divided by one day (0xC92A69C000).
            long ticksRemaining = memo.ExpirationTimestampUtc.Value.ToLocalTime().Ticks - DateTimeOffset.Now.Ticks;
            daysRemaining = (int)(ticksRemaining / TimeSpan.TicksPerDay);
            return true;
        }

        private static string BuildClaimDeadlineStatusText(MemoState memo)
        {
            if (!TryGetClientClaimWindowDaysRemaining(memo, out int daysRemaining))
            {
                return string.Empty;
            }

            if (daysRemaining >= 30)
            {
                return MapleStoryStringPool.GetOrFallback(
                    StringPoolClaimWindowUnset,
                    "Claim window unavailable");
            }

            if (daysRemaining < 1)
            {
                return MapleStoryStringPool.GetOrFallback(
                    StringPoolClaimWindowExpired,
                    "Expired");
            }

            string daysTemplate = MapleStoryStringPool.GetOrFallback(
                StringPoolClaimWindowDaysRemaining,
                "%d day(s) remaining");
            return daysTemplate.Replace("%d", daysRemaining.ToString(), StringComparison.Ordinal);
        }

        private string BuildModeSummary()
        {
            return _activeTab switch
            {
                ParcelDialogTab.QuickSend =>
                    "Quick Send tab: recipient, message, a meso attachment, and a Quick Delivery Coupon are required; item parcels stay blocked on this simulator seam.",
                ParcelDialogTab.Receive =>
                    "Receive tab: delivered parcel state stays separate from memo, whisper, and messenger surfaces.",
                _ =>
                    "Send tab: recipient plus an item or meso attachment are required; the message field stays editable but the client send branch only dispatches the package."
            };
        }

        private string BuildTaxSummary()
        {
            int attachedMeso = _draft.Attachment?.Kind == MemoAttachmentKind.Meso ? Math.Max(0, _draft.Attachment.Meso) : 0;
            return _activeTab switch
            {
                ParcelDialogTab.QuickSend => FormatClientPrompt(
                    StringPoolQuickSendFeePrompt,
                    "The service charge will cost {0} mesos. This will also use up 1 [Quick Delivery Coupon]. Are you sure you want to send it?",
                    GetParcelTax(attachedMeso)),
                _ => FormatClientPrompt(
                    StringPoolSendFeePrompt,
                    "The total wiring/transportation fee is {0} mesos. Are you sure you want to send the package?",
                    GetParcelTax(attachedMeso) + 5000)
            };
        }

        private bool HasDraftAttachment()
        {
            return _draft.Attachment is { Kind: not MemoAttachmentKind.None } attachment
                && (attachment.Kind != MemoAttachmentKind.Meso || attachment.Meso > 0);
        }

        private static int GetParcelTax(int meso)
        {
            if (meso >= 10_000_000)
            {
                return (int)(meso * 0.04d);
            }

            if (meso >= 5_000_000)
            {
                return (int)(meso * 0.03d);
            }

            if (meso >= 1_000_000)
            {
                return (int)(meso * 0.02d);
            }

            if (meso >= 100_000)
            {
                return (int)(meso * 0.01d);
            }

            if (meso >= 50_000)
            {
                return (int)(meso * 0.005d);
            }

            return 0;
        }

        private static string FormatClientPrompt(int stringPoolId, string fallbackFormat, int meso)
        {
            string template = MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackFormat);
            return template.Replace("%d", meso.ToString(), StringComparison.Ordinal)
                .Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace('\r', ' ')
                .Replace('\n', ' ');
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
            _awaitingItemSelection = false;
            _showTaxInfo = false;
            _lastActionSummary = actionSummary;
        }

        private void ApplyTabAvailability(ParcelDialogTabAvailability availableTabs)
        {
            _availableTabs = availableTabs == ParcelDialogTabAvailability.None
                ? ParcelDialogTabAvailability.Receive
                : availableTabs;

            if (!IsTabAvailable(_activeTab))
            {
                _activeTab = ResolveFallbackActiveTab();
            }
        }

        private bool IsTabAvailable(ParcelDialogTab tab)
        {
            ParcelDialogTabAvailability flag = tab switch
            {
                ParcelDialogTab.Receive => ParcelDialogTabAvailability.Receive,
                ParcelDialogTab.Send => ParcelDialogTabAvailability.Send,
                ParcelDialogTab.QuickSend => ParcelDialogTabAvailability.QuickSend,
                _ => ParcelDialogTabAvailability.None
            };

            return (_availableTabs & flag) != 0;
        }

        private ParcelDialogTab ResolveFallbackActiveTab()
        {
            if ((_availableTabs & ParcelDialogTabAvailability.Receive) != 0)
            {
                return ParcelDialogTab.Receive;
            }

            if ((_availableTabs & ParcelDialogTabAvailability.Send) != 0)
            {
                return ParcelDialogTab.Send;
            }

            return ParcelDialogTab.QuickSend;
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

        private static string BuildPacketOwnedParcelEntrySignature(
            int parcelSerial,
            string sender,
            string subject,
            string body,
            bool isRead,
            bool isKept,
            bool isAttachmentClaimed,
            byte stateFlags,
            bool isQuickDelivery,
            IReadOnlyList<byte> quickDeliveryReservedBytes,
            int attachmentItemId,
            int attachmentQuantity,
            int attachmentMeso,
            DateTimeOffset? expirationTimestampUtc)
        {
            return string.Join("|",
                parcelSerial.ToString(),
                sender?.Trim() ?? string.Empty,
                subject?.Trim() ?? string.Empty,
                body?.Trim() ?? string.Empty,
                isRead ? "1" : "0",
                isKept ? "1" : "0",
                isAttachmentClaimed ? "1" : "0",
                stateFlags.ToString("X2"),
                isQuickDelivery ? "1" : "0",
                FormatByteHex(quickDeliveryReservedBytes),
                attachmentItemId.ToString(),
                attachmentQuantity.ToString(),
                attachmentMeso.ToString(),
                expirationTimestampUtc?.UtcTicks.ToString() ?? string.Empty);
        }

        private static string FormatByteHex(IReadOnlyList<byte> bytes)
        {
            if (bytes == null || bytes.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("-", bytes.Select(value => value.ToString("X2")));
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
