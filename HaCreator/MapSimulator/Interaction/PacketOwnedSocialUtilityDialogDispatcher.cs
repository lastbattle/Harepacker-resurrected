using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using BinaryReader = MapleLib.PacketLib.PacketReader;
namespace HaCreator.MapSimulator.Interaction
{
    internal sealed record ParcelAlarmPromptSnapshot(
        int PacketSubtype,
        string Sender,
        bool IsQuickDelivery,
        bool RequiresFadeYesNoOwner,
        int LifetimeMilliseconds,
        string Title,
        string Body);

    internal sealed class PacketOwnedSocialUtilityDialogDispatcher
    {
        private readonly MapleTvRuntime _mapleTvRuntime;
        private readonly PacketOwnedParcelDialogRuntime _parcelDialogRuntime;
        private readonly PacketOwnedTrunkDialogRuntime _trunkDialogRuntime;
        private readonly MessengerRuntime _messengerRuntime;
        private string _lastDispatchSummary = "Packet-owned social utility dispatcher idle.";
        private string _lastMapleTvDispatchSummary = "CMapleTVMan::OnPacket idle.";
        private string _lastParcelDispatchSummary = "CParcelDlg::OnPacket idle.";
        private string _lastTrunkDispatchSummary = "CTrunkDlg::OnPacket idle.";
        private string _lastMessengerDispatchSummary = "CUIMessenger::OnPacket idle.";

        internal PacketOwnedSocialUtilityDialogDispatcher(
            MapleTvRuntime mapleTvRuntime,
            MemoMailboxManager memoMailbox,
            Func<SimulatorStorageRuntime> trunkStorageRuntimeResolver,
            MessengerRuntime messengerRuntime)
        {
            _mapleTvRuntime = mapleTvRuntime ?? throw new ArgumentNullException(nameof(mapleTvRuntime));
            _parcelDialogRuntime = new PacketOwnedParcelDialogRuntime(memoMailbox ?? throw new ArgumentNullException(nameof(memoMailbox)));
            _trunkDialogRuntime = new PacketOwnedTrunkDialogRuntime(trunkStorageRuntimeResolver ?? throw new ArgumentNullException(nameof(trunkStorageRuntimeResolver)));
            _messengerRuntime = messengerRuntime ?? throw new ArgumentNullException(nameof(messengerRuntime));
        }

        internal bool TryApplyMapleTvPacket(
            int packetType,
            byte[] payload,
            int currentTick,
            Func<LoginAvatarLook, CharacterBuild> buildResolver,
            out string message)
        {
            bool applied = _mapleTvRuntime.TryApplyPacket(packetType, payload, currentTick, buildResolver, out message);
            _lastMapleTvDispatchSummary = applied
                ? $"CMapleTVMan::OnPacket dispatched {DescribeMapleTvPacketType(packetType)}. {message}"
                : $"CMapleTVMan::OnPacket rejected {DescribeMapleTvPacketType(packetType)}. {message}";
            _lastDispatchSummary = _lastMapleTvDispatchSummary;

            return applied;
        }

        internal bool TryApplyParcelPacket(byte[] payload, out string message)
        {
            bool applied = _parcelDialogRuntime.TryApplyPacket(payload, out message);
            if (applied)
            {
                _lastParcelDispatchSummary = $"CParcelDlg::OnPacket dispatched subtype {_parcelDialogRuntime.LastSubtype.ToString(CultureInfo.InvariantCulture)}.";
                _lastDispatchSummary = _lastParcelDispatchSummary;
            }

            return applied;
        }

        internal bool ShouldShowParcelOwnerAfterLastPacket => _parcelDialogRuntime.ShouldShowOwnerWindowAfterApply;
        internal bool ShouldHideParcelOwnerAfterLastPacket => _parcelDialogRuntime.ShouldCloseOwnerWindowAfterApply;
        internal bool IsPacketOwnedParcelDialogOpen => _parcelDialogRuntime.IsOpen;

        internal ParcelAlarmPromptSnapshot LastParcelAlarmPrompt => _parcelDialogRuntime.LastAlarmPrompt;
        internal IReadOnlyList<string> LastParcelArrivalNotices => _parcelDialogRuntime.LastArrivalNotices;

        internal bool TryDeliverParcel(
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
            bool applied = _parcelDialogRuntime.TryDeliverPacketOwnedParcel(
                sender,
                subject,
                body,
                isRead,
                isKept,
                isClaimed,
                attachmentItemId,
                attachmentQuantity,
                attachmentMeso,
                out message);
            if (applied)
            {
                _lastParcelDispatchSummary = "CParcelDlg::OnPacket applied a packet-owned parcel delivery payload.";
                _lastDispatchSummary = _lastParcelDispatchSummary;
            }

            return applied;
        }

        internal bool TryApplyTrunkPacket(byte[] payload, out string message)
        {
            bool applied = _trunkDialogRuntime.TryApplyPacket(payload, out message);
            if (applied)
            {
                _lastTrunkDispatchSummary = $"CTrunkDlg::OnPacket dispatched subtype {_trunkDialogRuntime.LastSubtype.ToString(CultureInfo.InvariantCulture)}.";
                _lastDispatchSummary = _lastTrunkDispatchSummary;
            }

            return applied;
        }

        internal bool TryApplyMessengerDispatchSubtype(byte packetSubtype, byte[] payload, out string message)
        {
            byte[] resolvedPayload = payload ?? Array.Empty<byte>();
            byte[] dispatchPayload = new byte[resolvedPayload.Length + 1];
            dispatchPayload[0] = packetSubtype;
            if (resolvedPayload.Length > 0)
            {
                Buffer.BlockCopy(resolvedPayload, 0, dispatchPayload, 1, resolvedPayload.Length);
            }

            return TryApplyMessengerDispatchPacket(dispatchPayload, out message);
        }

        internal bool TryApplyMessengerDispatchPacket(byte[] payload, out string message)
        {
            message = string.Empty;
            if (!MessengerPacketCodec.TryParseClientDispatch(payload ?? Array.Empty<byte>(), out byte packetSubtype, out _, out string dispatchError))
            {
                message = dispatchError ?? "Messenger OnPacket payload could not be decoded.";
                return false;
            }

            if (packetSubtype > 8)
            {
                message = $"Messenger OnPacket subtype '{packetSubtype}' is not modeled.";
                _lastMessengerDispatchSummary = $"CUIMessenger::OnPacket ignored unsupported subtype {packetSubtype}.";
                _lastDispatchSummary = _lastMessengerDispatchSummary;
                return false;
            }

            message = _messengerRuntime.ApplyPacketDispatchPayload(payload);
            _lastMessengerDispatchSummary = $"CUIMessenger::OnPacket dispatched subtype {packetSubtype} ({DescribeMessengerDispatchSubtype(packetSubtype)}). {_messengerRuntime.LastPacketSummary}";
            _lastDispatchSummary = _lastMessengerDispatchSummary;
            return true;
        }

        internal bool TryApplyMessengerPacket(MessengerPacketType packetType, byte[] payload, out string message)
        {
            payload ??= Array.Empty<byte>();
            if (!TryValidateMessengerPacketPayload(packetType, payload, out message))
            {
                _lastMessengerDispatchSummary = $"CUIMessenger packet-owned {DescribeMessengerPacketType(packetType)} payload rejected.";
                _lastDispatchSummary = _lastMessengerDispatchSummary;
                return false;
            }

            message = _messengerRuntime.ApplyPacketPayload(packetType, payload);
            _lastMessengerDispatchSummary = $"CUIMessenger packet-owned {DescribeMessengerPacketType(packetType)} payload applied. {_messengerRuntime.LastPacketSummary}";
            _lastDispatchSummary = _lastMessengerDispatchSummary;
            return true;
        }

        internal string DescribeMapleTvStatus(int currentTick)
        {
            return $"{_mapleTvRuntime.DescribeStatus(currentTick)} Dispatcher: {_lastMapleTvDispatchSummary}";
        }

        internal string DescribeParcelStatus()
        {
            return $"{_parcelDialogRuntime.DescribeStatus()} Dispatcher: {_lastParcelDispatchSummary}";
        }

        internal string DescribeTrunkStatus()
        {
            return $"{_trunkDialogRuntime.DescribeStatus()} Dispatcher: {_lastTrunkDispatchSummary}";
        }

        internal bool TryBuildTrunkCloseOutboundRequest(out PacketOwnedNpcUtilityOutboundRequest request, out string message)
        {
            bool built = _trunkDialogRuntime.TryBuildCloseOutboundRequest(out request, out message);
            _lastTrunkDispatchSummary = built
                ? "CTrunkDlg::SetRet mirrored the close/return request."
                : $"CTrunkDlg::SetRet close request was ignored. {message}";
            _lastDispatchSummary = _lastTrunkDispatchSummary;
            return built;
        }

        internal bool IsPacketOwnedTrunkDialogOpen => _trunkDialogRuntime.IsOpen;

        internal bool TryBuildTrunkGetItemOutboundRequest(
            InventoryType inventoryType,
            int ownerRowIndex,
            InventorySlotData slotData,
            out PacketOwnedNpcUtilityOutboundRequest request,
            out string message)
        {
            bool built = _trunkDialogRuntime.TryBuildGetItemOutboundRequest(
                inventoryType,
                ownerRowIndex,
                slotData,
                out request,
                out message);
            _lastTrunkDispatchSummary = built
                ? "CTrunkDlg::SendGetItemRequest mirrored the outbound body."
                : $"CTrunkDlg::SendGetItemRequest was ignored. {message}";
            _lastDispatchSummary = _lastTrunkDispatchSummary;
            return built;
        }

        internal bool TryBuildTrunkPutItemOutboundRequest(
            InventoryType inventoryType,
            int inventoryRowIndex,
            InventorySlotData slotData,
            int requestedQuantity,
            out PacketOwnedNpcUtilityOutboundRequest request,
            out string message)
        {
            bool built = _trunkDialogRuntime.TryBuildPutItemOutboundRequest(
                inventoryType,
                inventoryRowIndex,
                slotData,
                requestedQuantity,
                out request,
                out message);
            _lastTrunkDispatchSummary = built
                ? "CTrunkDlg::SendPutItemRequest mirrored the outbound body."
                : $"CTrunkDlg::SendPutItemRequest was ignored. {message}";
            _lastDispatchSummary = _lastTrunkDispatchSummary;
            return built;
        }

        internal string DescribeMessengerStatus()
        {
            return $"{_messengerRuntime.DescribeStatus()} Dispatcher: {_lastMessengerDispatchSummary}";
        }

        internal string DescribeStatus(int currentTick)
        {
            MapleTvSnapshot mapleTvSnapshot = _mapleTvRuntime.BuildSnapshot(currentTick);
            return $"Packet-owned social utility dispatcher. MapleTV={(_mapleTvSnapshotIsActive(mapleTvSnapshot) ? "active" : "idle")}, parcel={(_parcelDialogRuntime.IsOpen ? "open" : "idle")}, trunk={(_trunkDialogRuntime.IsOpen ? "open" : "idle")}, messenger={_messengerRuntime.BuildSnapshot(currentTick).Participants.Count} slots. Last dispatch: {_lastDispatchSummary}";

            static bool _mapleTvSnapshotIsActive(MapleTvSnapshot snapshot)
            {
                return snapshot?.IsShowingMessage == true || snapshot?.QueueExists == true;
            }
        }

        private static bool TryValidateMessengerPacketPayload(MessengerPacketType packetType, byte[] payload, out string message)
        {
            message = null;
            switch (packetType)
            {
                case MessengerPacketType.Invite:
                case MessengerPacketType.InviteAccept:
                case MessengerPacketType.InviteReject:
                case MessengerPacketType.Leave:
                    if (!MessengerPacketCodec.TryParseInvite(payload, out _, out message))
                    {
                        return false;
                    }

                    return true;
                case MessengerPacketType.RoomChat:
                case MessengerPacketType.Whisper:
                    if (!MessengerPacketCodec.TryParseChat(payload, out _, out message))
                    {
                        return false;
                    }

                    return true;
                case MessengerPacketType.MemberInfo:
                    if (!MessengerPacketCodec.TryParseMemberInfo(payload, out _, out message))
                    {
                        return false;
                    }

                    return true;
                case MessengerPacketType.Blocked:
                    if (!MessengerPacketCodec.TryParseBlocked(payload, out _, out message))
                    {
                        return false;
                    }

                    return true;
                case MessengerPacketType.Avatar:
                    if (!MessengerPacketCodec.TryParseAvatar(payload, out _, out message))
                    {
                        return false;
                    }

                    return true;
                case MessengerPacketType.Enter:
                    if (!MessengerPacketCodec.TryParseEnter(payload, out _, out message))
                    {
                        return false;
                    }

                    return true;
                case MessengerPacketType.InviteResult:
                    if (!MessengerPacketCodec.TryParseInviteResult(payload, out _, out message))
                    {
                        return false;
                    }

                    return true;
                case MessengerPacketType.Migrated:
                    if (!MessengerPacketCodec.TryParseMigrated(payload, out _, out message))
                    {
                        return false;
                    }

                    return true;
                case MessengerPacketType.SelfEnterResult:
                    if (!MessengerPacketCodec.TryParseSelfEnterResult(payload, out _, out message))
                    {
                        return false;
                    }

                    return true;
                default:
                    message = $"Messenger packet type '{packetType}' is not modeled.";
                    return false;
            }
        }

        private static string DescribeMessengerPacketType(MessengerPacketType packetType)
        {
            return packetType switch
            {
                MessengerPacketType.Invite => "invite",
                MessengerPacketType.InviteAccept => "invite-accept",
                MessengerPacketType.InviteReject => "invite-reject",
                MessengerPacketType.Leave => "leave",
                MessengerPacketType.RoomChat => "room-chat",
                MessengerPacketType.Whisper => "whisper",
                MessengerPacketType.MemberInfo => "member-info",
                MessengerPacketType.Blocked => "blocked",
                MessengerPacketType.Avatar => "avatar",
                MessengerPacketType.Enter => "enter",
                MessengerPacketType.InviteResult => "invite-result",
                MessengerPacketType.Migrated => "migrated",
                MessengerPacketType.SelfEnterResult => "self-enter-result",
                _ => packetType.ToString()
            };
        }

        private static string DescribeMessengerDispatchSubtype(byte packetSubtype)
        {
            return packetSubtype switch
            {
                0 => "OnEnter",
                1 => "OnSelfEnterResult",
                2 => "OnLeave",
                3 => "OnInvite",
                4 => "OnInviteResult",
                5 => "OnBlocked",
                6 => "OnChat",
                7 => "OnAvatar",
                8 => "OnMigrated",
                _ => packetSubtype.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static string DescribeMapleTvPacketType(int packetType)
        {
            return packetType switch
            {
                MapleTvRuntime.PacketTypeSetMessage => "packet 405 (OnSetMessage)",
                MapleTvRuntime.PacketTypeClearMessage => "packet 406 (OnClearMessage)",
                MapleTvRuntime.PacketTypeSendMessageResult => "packet 407 (OnSendMessageResult)",
                _ => $"packet {packetType.ToString(CultureInfo.InvariantCulture)}"
            };
        }
    }

    internal sealed class PacketOwnedParcelDialogRuntime
    {
        private readonly MemoMailboxManager _memoMailbox;
        private readonly List<string> _lastArrivalNotices = new();
        private int _openCount;
        private int _noticeCount;
        private int _arrivalNoticeCount;
        private int _deliveryCount;
        private int _removalCount;
        private int _currentDialogMode = -1;
        private string _lastAlarmSender = string.Empty;
        private bool _lastAlarmWasQuickDelivery;

        internal PacketOwnedParcelDialogRuntime(MemoMailboxManager memoMailbox)
        {
            _memoMailbox = memoMailbox;
        }

        internal bool IsOpen { get; private set; }
        internal int LastSubtype { get; private set; } = -1;
        internal string StatusMessage { get; private set; } = "CParcelDlg::OnPacket idle.";
        internal bool ShouldShowOwnerWindowAfterApply { get; private set; }
        internal bool ShouldCloseOwnerWindowAfterApply { get; private set; }
        internal ParcelAlarmPromptSnapshot LastAlarmPrompt { get; private set; }
        internal IReadOnlyList<string> LastArrivalNotices => _lastArrivalNotices;

        internal bool TryApplyPacket(byte[] payload, out string message)
        {
            message = null;
            ShouldShowOwnerWindowAfterApply = false;
            ShouldCloseOwnerWindowAfterApply = false;
            LastAlarmPrompt = null;
            _lastArrivalNotices.Clear();
            if (payload == null || payload.Length == 0)
            {
                message = "Parcel dialog packet payload must contain a subtype byte.";
                return false;
            }

            LastSubtype = payload[0];
            switch (LastSubtype)
            {
                case 8:
                    return TryApplyOpenPacket(payload, out message);
                case 23:
                    return TryApplyRemovePacket(payload, out message);
                case 24:
                    return TryApplyDeliverPacket(payload, out message);
                case 25:
                    return TryApplyAlarmPacket(payload, expectsSender: true, out message);
                case 26:
                    _openCount++;
                    IsOpen = true;
                    ShouldShowOwnerWindowAfterApply = true;
                    _currentDialogMode = 1;
                    _memoMailbox.ApplyPacketOwnedDialogMode(_currentDialogMode);
                    StatusMessage = "CParcelDlg packet 26 opened the packet-owned quick-delivery owner.";
                    message = StatusMessage;
                    return true;
                case 27:
                    return TryApplyAlarmPacket(payload, expectsSender: false, out message);
                default:
                    if (!IsOpen)
                    {
                        message = $"CParcelDlg result {LastSubtype.ToString(CultureInfo.InvariantCulture)} requires an open packet-owned parcel owner.";
                        return false;
                    }

                    _noticeCount++;
                    if (LastSubtype == 18)
                    {
                        switch (_currentDialogMode)
                        {
                            case 0:
                                _memoMailbox.ResetDraftState();
                                StatusMessage = "CParcelDlg result 18 reset the send-info branch on the packet-owned parcel owner.";
                                break;
                            case 1:
                                IsOpen = false;
                                ShouldCloseOwnerWindowAfterApply = true;
                                StatusMessage = "CParcelDlg result 18 closed the packet-owned quick-delivery owner.";
                                break;
                            default:
                                StatusMessage = "CParcelDlg result 18 completed without mutating the active packet-owned parcel mode.";
                                break;
                        }
                    }
                    else
                    {
                        StatusMessage = $"CParcelDlg result {LastSubtype.ToString(CultureInfo.InvariantCulture)} followed the packet-owned notice-result branch.";
                    }

                    message = StatusMessage;
                    return true;
            }
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
            bool delivered = _memoMailbox.TryDeliverPacketOwnedParcel(
                sender,
                subject,
                body,
                isRead,
                isKept,
                isClaimed,
                attachmentItemId,
                attachmentQuantity,
                attachmentMeso,
                out message);
            if (delivered)
            {
                IsOpen = true;
                LastSubtype = 24;
                _deliveryCount++;
                string resolvedSubject = string.IsNullOrWhiteSpace(subject) ? body?.Trim() ?? string.Empty : subject.Trim();
                StatusMessage = $"CParcelDlg packet 24 applied mailbox payload ownership for parcel '{resolvedSubject}'.";
                message = StatusMessage;
            }

            return delivered;
        }

        internal string DescribeStatus()
        {
            MemoMailboxSnapshot snapshot = _memoMailbox.GetSnapshot();
            return $"Parcel packet-owner {(IsOpen ? "open" : "idle")}. Rows={snapshot.Entries.Count}, unread={snapshot.UnreadCount}, claimable={snapshot.ClaimableCount}, packets open={_openCount}, delivery={_deliveryCount}, remove={_removalCount}, notice={_noticeCount}, arrival={_arrivalNoticeCount}. Last subtype={LastSubtype.ToString(CultureInfo.InvariantCulture)}. {StatusMessage}";
        }

        private bool TryApplyOpenPacket(byte[] payload, out string message)
        {
            if (payload.Length < 2)
            {
                message = "Parcel dialog packet 8 requires the follow-up mode flag byte.";
                return false;
            }

            bool modeTwoOpen = payload[1] != 0;
            if (!PacketOwnedParcelPacketCodec.TryDecodeSessionPayload(payload.AsSpan(2), out PacketOwnedParcelSessionDecodeResult session, out string error))
            {
                message = $"Parcel dialog packet 8 could not decode the CTabReceive::SetParcel payload. {error}";
                return false;
            }

            _openCount++;
            IsOpen = true;
            ShouldShowOwnerWindowAfterApply = true;
            _currentDialogMode = modeTwoOpen ? 2 : 0;
            ParcelDialogTab activeTab = !modeTwoOpen && session.ReceiveEntries.Count == 0
                ? ParcelDialogTab.Send
                : ParcelDialogTab.Receive;
            _memoMailbox.ReplacePacketOwnedParcelSession(
                session.ReceiveEntries,
                _currentDialogMode switch
                {
                    2 => ParcelDialogTabAvailability.Receive,
                    _ => ParcelDialogTabAvailability.Receive | ParcelDialogTabAvailability.Send
                },
                activeTab,
                out string sessionMessage);
            if (session.ArrivalNoticeEntries.Count > 0)
            {
                _arrivalNoticeCount += session.ArrivalNoticeEntries.Count;
                _lastAlarmSender = session.ArrivalNoticeEntries[0].Sender ?? string.Empty;
                _lastArrivalNotices.AddRange(BuildArrivalNotices(session.ArrivalNoticeEntries));
            }

            string arrivalSummary = DescribeArrivalNoticeSummary(session.ArrivalNoticeEntries);
            StatusMessage = $"CParcelDlg packet 8 opened the packet-owned receive owner through {(modeTwoOpen ? "mode 2" : "mode 0")} on the {activeTab} tab and applied {session.ReceiveEntries.Count.ToString(CultureInfo.InvariantCulture)} receive row(s){arrivalSummary}. {sessionMessage}";
            message = StatusMessage;
            return true;
        }

        private bool TryApplyRemovePacket(byte[] payload, out string message)
        {
            if (payload.Length < 1 + sizeof(int) + sizeof(byte))
            {
                message = "Parcel dialog packet 23 requires a memo id and result code.";
                return false;
            }

            if (!IsOpen)
            {
                message = "Parcel dialog packet 23 requires an open packet-owned parcel owner.";
                return false;
            }

            int memoId = BitConverter.ToInt32(payload, 1);
            byte resultCode = payload[5];
            _memoMailbox.DeleteMemo(memoId);
            _removalCount++;
            string notice = PacketOwnedSocialUtilityStringPoolText.ResolveParcelRemovalNotice(resultCode, out int stringPoolId);
            StatusMessage = resultCode == 3
                ? $"CParcelDlg packet 23 removed parcel #{memoId} through the claim-success StringPool 0x{stringPoolId:X} branch: {notice}"
                : $"CParcelDlg packet 23 removed parcel #{memoId} through the discard-result StringPool 0x{stringPoolId:X} branch ({resultCode.ToString(CultureInfo.InvariantCulture)}): {notice}";
            message = StatusMessage;
            return true;
        }

        private bool TryApplyDeliverPacket(byte[] payload, out string message)
        {
            if (!IsOpen)
            {
                StatusMessage = "CParcelDlg packet 24 arrived without an open packet-owned parcel owner, so it followed the client no-op branch.";
                message = StatusMessage;
                return true;
            }

            if (!PacketOwnedParcelPacketCodec.TryDecodeSingleEntryPayload(payload.AsSpan(1), out PacketOwnedParcelDecodedEntry entry, out string error))
            {
                message = $"Parcel dialog packet 24 could not decode the PARCEL::Decode payload. {error}";
                return false;
            }

            bool delivered = _memoMailbox.TryDeliverDecodedPacketOwnedParcel(entry, out message);
            if (!delivered)
            {
                return false;
            }

            LastSubtype = 24;
            _deliveryCount++;
            _arrivalNoticeCount++;
            _lastArrivalNotices.Add(BuildArrivalNotice(entry));
            StatusMessage = $"CParcelDlg packet 24 decoded PARCEL::Decode payload serial {entry.ParcelSerial.ToString(CultureInfo.InvariantCulture)} from {entry.Sender} and dispatched the client-shaped sender/attachment arrival notice.";
            message = StatusMessage;
            return true;
        }

        private bool TryApplyAlarmPacket(byte[] payload, bool expectsSender, out string message)
        {
            using MemoryStream stream = new(payload, 1, payload.Length - 1, writable: false);
            using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);

            string sender = string.Empty;
            if (expectsSender)
            {
                if (!TryReadPacketString(reader, out sender))
                {
                    message = "Parcel alarm packet is missing the sender string.";
                    return false;
                }
            }

            bool requiresFadeOwner = !IsOpen;
            bool shouldDecodeAttachmentFlag = expectsSender || requiresFadeOwner;
            bool hasAttachment = _lastAlarmWasQuickDelivery;
            if (shouldDecodeAttachmentFlag)
            {
                if (stream.Position >= stream.Length)
                {
                    message = expectsSender
                        ? "Parcel alarm packet is missing the attachment-state flag."
                        : "Parcel alarm packet 27 is missing the attachment-state flag required by the fade-owner branch.";
                    return false;
                }

                hasAttachment = reader.ReadByte() != 0;
            }

            _noticeCount++;
            _lastAlarmSender = sender ?? string.Empty;
            _lastAlarmWasQuickDelivery = hasAttachment;
            if (requiresFadeOwner)
            {
                LastAlarmPrompt = BuildParcelAlarmPrompt(LastSubtype, sender, hasAttachment);
            }

            StatusMessage = expectsSender
                ? $"CParcelDlg packet {LastSubtype.ToString(CultureInfo.InvariantCulture)} {(requiresFadeOwner ? "created" : "refreshed")} a CUIFadeYesNo parcel alarm for {(hasAttachment ? "quick-delivery" : "standard")} delivery from {sender}."
                : requiresFadeOwner
                    ? $"CParcelDlg packet {LastSubtype.ToString(CultureInfo.InvariantCulture)} created the CUIFadeYesNo empty-sender {(hasAttachment ? "quick-delivery" : "standard")} parcel alarm branch."
                    : $"CParcelDlg packet {LastSubtype.ToString(CultureInfo.InvariantCulture)} refreshed the existing parcel owner alarm branch without reopening CUIFadeYesNo.";
            message = StatusMessage;
            return true;
        }

        private static ParcelAlarmPromptSnapshot BuildParcelAlarmPrompt(int packetSubtype, string sender, bool isQuickDelivery)
        {
            string resolvedSender = string.IsNullOrWhiteSpace(sender)
                ? "Maple Delivery Service"
                : sender.Trim();
            string deliveryKind = isQuickDelivery ? "quick delivery" : "parcel delivery";
            string body = string.IsNullOrWhiteSpace(sender)
                ? $"A {deliveryKind} notice has arrived."
                : $"{resolvedSender} sent you a {deliveryKind} notice.";
            return new ParcelAlarmPromptSnapshot(
                packetSubtype,
                resolvedSender,
                isQuickDelivery,
                RequiresFadeYesNoOwner: true,
                LifetimeMilliseconds: isQuickDelivery ? int.MaxValue : 6000,
                Title: isQuickDelivery ? "Quick Delivery" : "Parcel Delivery",
                Body: body);
        }

        private static string DescribeArrivalNoticeSummary(IReadOnlyList<PacketOwnedParcelDecodedEntry> arrivalEntries)
        {
            if (arrivalEntries == null || arrivalEntries.Count == 0)
            {
                return string.Empty;
            }

            return $", and dispatched {arrivalEntries.Count.ToString(CultureInfo.InvariantCulture)} arrival notice(s)";
        }

        private static IReadOnlyList<string> BuildArrivalNotices(IReadOnlyList<PacketOwnedParcelDecodedEntry> arrivalEntries)
        {
            if (arrivalEntries == null || arrivalEntries.Count == 0)
            {
                return Array.Empty<string>();
            }

            var notices = new List<string>(arrivalEntries.Count);
            for (int i = 0; i < arrivalEntries.Count; i++)
            {
                notices.Add(BuildArrivalNotice(arrivalEntries[i]));
            }

            return notices;
        }

        private static string BuildArrivalNotice(PacketOwnedParcelDecodedEntry entry)
        {
            StringBuilder noticeBuilder = new(PacketOwnedSocialUtilityStringPoolText.ResolveParcelArrivalSenderNotice(entry?.Sender));
            if (entry?.AttachmentMeso > 0)
            {
                noticeBuilder.Append(PacketOwnedSocialUtilityStringPoolText.ResolveParcelArrivalMesoNotice(entry.AttachmentMeso));
            }

            if (entry?.HasItemAttachment == true)
            {
                noticeBuilder.Append(PacketOwnedSocialUtilityStringPoolText.ResolveParcelArrivalItemNotice(
                    ResolveArrivalItemName(entry.AttachmentItemId)));
            }

            return noticeBuilder.ToString();
        }

        private static string ResolveArrivalItemName(int itemId)
        {
            if (itemId <= 0)
            {
                return "item";
            }

            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                              && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                              && !string.IsNullOrWhiteSpace(itemInfo.Item2)
                ? itemInfo.Item2
                : $"item {itemId.ToString(CultureInfo.InvariantCulture)}";
        }

        private static bool TryReadPacketString(BinaryReader reader, out string value)
        {
            value = string.Empty;
            Stream stream = reader?.BaseStream;
            if (stream == null || stream.Length - stream.Position < sizeof(short))
            {
                return false;
            }

            short length = reader.ReadInt16();
            if (length < 0 || stream.Length - stream.Position < length)
            {
                return false;
            }

            value = Encoding.ASCII.GetString(reader.ReadBytes(length)).Trim();
            return true;
        }
    }

    internal sealed class PacketOwnedTrunkDialogRuntime
    {
        private const int TrunkOutboundOpcode = 67;
        private const byte TrunkGetItemMode = 4;
        private const byte TrunkPutItemMode = 5;
        private const byte TrunkCloseMode = 8;
        private const int TrunkSendPutSharableOnceBlockedStringPoolId = 0x169E;
        private const int TrunkSendPutSharableOnceConfirmStringPoolId = 0x169D;
        private const int TrunkSendPutConfirmStringPoolId = 0x1245;
        private const int TrunkSendGetConfirmStringPoolId = 0x1246;
        private const int TrunkDefaultNoticeStringPoolId = 0x0369;
        private const int TrunkResult10NoticeStringPoolId = 0x0366;
        private const int TrunkResult11And16NoticeStringPoolId = 0x1A8B;
        private const int TrunkResult12NoticeStringPoolId = 0x0374;
        private const int TrunkResult17NoticeStringPoolId = 0x0373;
        private const int TrunkResult23NoticeStringPoolId = 0x16ED;

        private static readonly InventoryType[] SnapshotInventoryOrder =
        {
            InventoryType.EQUIP,
            InventoryType.USE,
            InventoryType.SETUP,
            InventoryType.ETC,
            InventoryType.CASH
        };

        private readonly Func<SimulatorStorageRuntime> _storageRuntimeResolver;
        private int _openCount;
        private int _refreshCount;
        private int _noticeCount;
        private bool _requestInFlight;

        internal PacketOwnedTrunkDialogRuntime(Func<SimulatorStorageRuntime> storageRuntimeResolver)
        {
            _storageRuntimeResolver = storageRuntimeResolver;
        }

        internal bool IsOpen { get; private set; }
        internal int LastSubtype { get; private set; } = -1;
        internal int LastGetRequestSnapshotNativeItemType { get; private set; } = -1;
        internal int LastGetRequestSnapshotNativeRow { get; private set; } = -1;
        internal string StatusMessage { get; private set; } = "CTrunkDlg::OnPacket idle.";

        internal bool TryApplyPacket(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length == 0)
            {
                message = "Trunk dialog packet payload must contain a subtype byte.";
                return false;
            }

            _requestInFlight = false;
            LastGetRequestSnapshotNativeItemType = -1;
            LastGetRequestSnapshotNativeRow = -1;
            LastSubtype = payload[0];
            switch (LastSubtype)
            {
                case 22:
                    return TryApplySnapshotPacket(payload, openDialog: true, out message);
                case 9:
                case 13:
                case 15:
                case 19:
                    return TryApplySnapshotPacket(payload, openDialog: false, out message);
                case 10:
                    return ApplyStringPoolNotice(
                        LastSubtype,
                        TrunkResult10NoticeStringPoolId,
                        "Trunk notice 10.",
                        out message);
                case 11:
                case 16:
                    return ApplyStringPoolNotice(
                        LastSubtype,
                        TrunkResult11And16NoticeStringPoolId,
                        "Trunk notice 11/16.",
                        out message);
                case 12:
                    return ApplyStringPoolNotice(
                        LastSubtype,
                        TrunkResult12NoticeStringPoolId,
                        "Trunk notice 12.",
                        out message);
                case 17:
                    return ApplyStringPoolNotice(
                        LastSubtype,
                        TrunkResult17NoticeStringPoolId,
                        "Trunk notice 17.",
                        out message);
                case 23:
                    return ApplyStringPoolNotice(
                        LastSubtype,
                        TrunkResult23NoticeStringPoolId,
                        "Trunk notice 23.",
                        out message);
                case 24:
                    return TryApplyCustomNoticePacket(payload, out message);
                default:
                    return ApplyStringPoolNotice(
                        LastSubtype,
                        TrunkDefaultNoticeStringPoolId,
                        $"Trunk notice {LastSubtype.ToString(CultureInfo.InvariantCulture)}.",
                        out message,
                        "fell back to the default branch");
            }
        }

        internal string DescribeStatus()
        {
            SimulatorStorageRuntime runtime = _storageRuntimeResolver();
            int usedSlots = runtime?.GetUsedSlotCount() ?? 0;
            long meso = runtime?.GetMesoCount() ?? 0;
            return $"Trunk packet-owner {(IsOpen ? "open" : "idle")}. Used slots={usedSlots}, meso={meso.ToString(CultureInfo.InvariantCulture)}, packets open={_openCount}, refresh={_refreshCount}, notice={_noticeCount}. Last subtype={LastSubtype.ToString(CultureInfo.InvariantCulture)}. {StatusMessage}";
        }

        internal bool TryBuildGetItemOutboundRequest(
            InventoryType inventoryType,
            int ownerRowIndex,
            InventorySlotData slotData,
            out PacketOwnedNpcUtilityOutboundRequest request,
            out string message)
        {
            request = default;
            if (!IsOpen)
            {
                StatusMessage = "CTrunkDlg ignored SendGetItemRequest because the owner is closed.";
                message = StatusMessage;
                return false;
            }

            if (_requestInFlight)
            {
                StatusMessage = "CTrunkDlg ignored SendGetItemRequest because a prior trunk request is still in flight.";
                message = StatusMessage;
                return false;
            }

            if (slotData == null || slotData.ItemId <= 0)
            {
                StatusMessage = "CTrunkDlg ignored SendGetItemRequest because the selected storage row is unavailable.";
                message = StatusMessage;
                return false;
            }

            if (!TryResolveTrunkItemType(slotData.ItemId, out int itemType))
            {
                StatusMessage = $"CTrunkDlg ignored SendGetItemRequest because item {slotData.ItemId.ToString(CultureInfo.InvariantCulture)} did not resolve a valid native item type index.";
                message = StatusMessage;
                return false;
            }

            if (!TryResolveStorageRowPosition(ownerRowIndex, slotData, out byte trunkRow))
            {
                StatusMessage = "CTrunkDlg ignored SendGetItemRequest because the selected storage row does not carry the packet-authored native nIdx token.";
                message = StatusMessage;
                return false;
            }

            request = new PacketOwnedNpcUtilityOutboundRequest(
                TrunkOutboundOpcode,
                BuildGetItemRequestPayload(itemType, trunkRow),
                $"Mirrored CTrunkDlg::SendGetItemRequest (opcode 67, mode 4, itemType {itemType.ToString(CultureInfo.InvariantCulture)}, row {trunkRow.ToString(CultureInfo.InvariantCulture)}).");
            _requestInFlight = true;
            LastGetRequestSnapshotNativeItemType = itemType;
            LastGetRequestSnapshotNativeRow = trunkRow;
            bool preConfirmShown = ShouldShowTrunkPreConfirmPrompt(slotData);
            string preConfirm = TrunkDialogClientParityText.ToInlineText(TrunkDialogClientParityText.ResolveSendGetPreConfirm());
            string costConfirm = TrunkDialogClientParityText.ToInlineText(TrunkDialogClientParityText.ResolveSendGetCostConfirm(0));
            string preConfirmSummary = preConfirmShown
                ? $"Accepted owner confirm {FormatStringPoolId(TrunkSendGetConfirmStringPoolId)}: {preConfirm}"
                : $"Skipped owner confirm {FormatStringPoolId(TrunkSendGetConfirmStringPoolId)} because the native pre-confirm predicate evaluated false for this row.";
            StatusMessage =
                $"CTrunkDlg staged SendGetItemRequest for item {slotData.ItemId.ToString(CultureInfo.InvariantCulture)} (itemType {itemType.ToString(CultureInfo.InvariantCulture)}, row {trunkRow.ToString(CultureInfo.InvariantCulture)}). " +
                $"{preConfirmSummary} " +
                $"Then accepted cost confirm {FormatStringPoolId(TrunkDialogClientParityText.SendGetNoCostConfirmStringPoolId)} / {FormatStringPoolId(TrunkDialogClientParityText.SendGetCostConfirmStringPoolId)}: {costConfirm}. " +
                $"Native post-send branch set m_nSnapshotTI={itemType.ToString(CultureInfo.InvariantCulture)} and called SetPutItems(m_nSnapshotTI, m_aSnapShot) before waiting for the packet refresh.";
            message = StatusMessage;
            return true;
        }

        internal bool TryBuildPutItemOutboundRequest(
            InventoryType inventoryType,
            int inventoryRowIndex,
            InventorySlotData slotData,
            int requestedQuantity,
            out PacketOwnedNpcUtilityOutboundRequest request,
            out string message)
        {
            request = default;
            if (!IsOpen)
            {
                StatusMessage = "CTrunkDlg ignored SendPutItemRequest because the owner is closed.";
                message = StatusMessage;
                return false;
            }

            if (_requestInFlight)
            {
                StatusMessage = "CTrunkDlg ignored SendPutItemRequest because a prior trunk request is still in flight.";
                message = StatusMessage;
                return false;
            }

            if (slotData == null || slotData.ItemId <= 0)
            {
                StatusMessage = "CTrunkDlg ignored SendPutItemRequest because the selected inventory row is unavailable.";
                message = StatusMessage;
                return false;
            }

            bool treatSingly = inventoryType == InventoryType.EQUIP
                || InventoryItemMetadataResolver.ResolveMaxStack(inventoryType, slotData.MaxStackSize) <= 1;
            int availableQuantity = Math.Max(1, slotData.Quantity);
            int normalizedQuantity;
            if (treatSingly)
            {
                normalizedQuantity = 1;
            }
            else
            {
                if (requestedQuantity <= 0 || requestedQuantity > availableQuantity)
                {
                    StatusMessage = $"CTrunkDlg ignored SendPutItemRequest because the requested count {requestedQuantity.ToString(CultureInfo.InvariantCulture)} is outside the accepted AskItemCount range 1..{availableQuantity.ToString(CultureInfo.InvariantCulture)}.";
                    message = StatusMessage;
                    return false;
                }

                normalizedQuantity = requestedQuantity;
            }

            if (IsSharableOnceCashOwnershipBlocked(slotData))
            {
                string blockedMessage = TrunkDialogClientParityText.ResolveSharableOnceBlockedNotice();
                StatusMessage = $"CTrunkDlg blocked SendPutItemRequest on the sharable-once ownership branch via {FormatStringPoolId(TrunkSendPutSharableOnceBlockedStringPoolId)}: {blockedMessage}";
                message = StatusMessage;
                return false;
            }

            if (!TryResolveInventoryPosition(inventoryRowIndex, slotData, out short inventoryPosition))
            {
                StatusMessage = "CTrunkDlg ignored SendPutItemRequest because the selected inventory row does not carry the packet-authored native inventory position token.";
                message = StatusMessage;
                return false;
            }

            request = new PacketOwnedNpcUtilityOutboundRequest(
                TrunkOutboundOpcode,
                BuildPutItemRequestPayload(inventoryPosition, slotData.ItemId, normalizedQuantity),
                $"Mirrored CTrunkDlg::SendPutItemRequest (opcode 67, mode 5, pos {inventoryPosition.ToString(CultureInfo.InvariantCulture)}, item {slotData.ItemId.ToString(CultureInfo.InvariantCulture)}, count {normalizedQuantity.ToString(CultureInfo.InvariantCulture)}).");
            _requestInFlight = true;
            bool sharableOnce = slotData.CashItemSerialNumber.GetValueOrDefault() > 0;
            bool preConfirmShown = sharableOnce || ShouldShowTrunkPreConfirmPrompt(slotData);
            string preConfirm = TrunkDialogClientParityText.ToInlineText(TrunkDialogClientParityText.ResolveSendPutPreConfirm(sharableOnce));
            string askCount = !treatSingly && availableQuantity > 1
                ? TrunkDialogClientParityText.ToInlineText(TrunkDialogClientParityText.ResolveSendPutAskItemCountPrompt())
                : "No AskItemCount branch (client treat-singly path).";
            string costConfirm = TrunkDialogClientParityText.ToInlineText(TrunkDialogClientParityText.ResolveSendPutCostConfirm(0));
            int preConfirmStringPoolId = sharableOnce
                ? TrunkSendPutSharableOnceConfirmStringPoolId
                : TrunkSendPutConfirmStringPoolId;
            string preConfirmSummary = preConfirmShown
                ? $"Accepted pre-send confirm {FormatStringPoolId(preConfirmStringPoolId)}: {preConfirm}"
                : $"Skipped pre-send confirm {FormatStringPoolId(preConfirmStringPoolId)} because the native pre-confirm predicate evaluated false for this row.";
            string quantitySummary = normalizedQuantity == availableQuantity
                ? $"full count {normalizedQuantity.ToString(CultureInfo.InvariantCulture)}"
                : $"partial count {normalizedQuantity.ToString(CultureInfo.InvariantCulture)}";
            StatusMessage =
                $"CTrunkDlg staged SendPutItemRequest for slot {inventoryPosition.ToString(CultureInfo.InvariantCulture)} with {quantitySummary}. " +
                $"{preConfirmSummary} " +
                $"AskItemCount path {FormatStringPoolId(TrunkDialogClientParityText.SendPutAskItemCountStringPoolId)}: {askCount} " +
                $"Then accepted cost confirm {FormatStringPoolId(TrunkDialogClientParityText.SendPutNoCostConfirmStringPoolId)} / {FormatStringPoolId(TrunkDialogClientParityText.SendPutCostConfirmStringPoolId)}: {costConfirm}.";
            message = StatusMessage;
            return true;
        }

        internal bool TryBuildCloseOutboundRequest(out PacketOwnedNpcUtilityOutboundRequest request, out string message)
        {
            request = default;
            if (!IsOpen)
            {
                StatusMessage = "CTrunkDlg ignored close because the owner is already closed.";
                message = StatusMessage;
                return false;
            }

            IsOpen = false;
            _requestInFlight = false;
            StatusMessage = "CTrunkDlg::SetRet closed the owner and mirrored packet 67 [08].";
            request = new PacketOwnedNpcUtilityOutboundRequest(
                TrunkOutboundOpcode,
                new byte[] { TrunkCloseMode },
                "Mirrored CTrunkDlg::SetRet close/return request (opcode 67, mode 8).");
            message = StatusMessage;
            return true;
        }

        private bool TryApplySnapshotPacket(byte[] payload, bool openDialog, out string message)
        {
            SimulatorStorageRuntime runtime = _storageRuntimeResolver();
            if (runtime == null)
            {
                message = "Trunk packet could not be applied because the trunk storage runtime is unavailable.";
                return false;
            }

            if (!TryParseSnapshotPayload(payload, out int slotLimit, out long meso, out Dictionary<InventoryType, IReadOnlyList<InventorySlotData>> snapshot, out message))
            {
                return false;
            }

            runtime.ReplaceContents(slotLimit, meso, snapshot);
            IsOpen = true;
            if (openDialog)
            {
                _openCount++;
                StatusMessage = $"CTrunkDlg packet 22 opened the packet-owned trunk dialog with slotLimit={slotLimit.ToString(CultureInfo.InvariantCulture)} and meso={meso.ToString(CultureInfo.InvariantCulture)}.";
            }
            else
            {
                _refreshCount++;
                StatusMessage = $"CTrunkDlg packet {LastSubtype.ToString(CultureInfo.InvariantCulture)} refreshed the packet-owned trunk storage snapshot.";
            }

            message = StatusMessage;
            return true;
        }

        private bool TryApplyCustomNoticePacket(byte[] payload, out string message)
        {
            if (payload.Length < 2)
            {
                message = "Trunk dialog packet 24 requires the custom-notice flag byte.";
                return false;
            }

            if (payload[1] == 0)
            {
                return ApplyStringPoolNotice(
                    LastSubtype,
                    TrunkDefaultNoticeStringPoolId,
                    "Trunk notice 24.",
                    out message,
                    "followed the default (non-custom) notice branch");
            }

            using MemoryStream stream = new(payload, 2, payload.Length - 2, writable: false);
            using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);
            if (!TryReadPacketString(reader, out string notice))
            {
                message = "Trunk dialog packet 24 reported a custom notice without text.";
                return false;
            }

            _noticeCount++;
            IsOpen = true;
            StatusMessage = $"CTrunkDlg result 24 delivered a custom packet-owned notice: {notice}";
            message = StatusMessage;
            return true;
        }

        private bool ApplyStringPoolNotice(int subtype, int stringPoolId, string fallback, out string message, string branchSuffix = null)
        {
            string text = ResolveStringPoolNoticeText(stringPoolId, fallback);
            string branchText = string.IsNullOrWhiteSpace(branchSuffix)
                ? $"followed {FormatStringPoolId(stringPoolId)}"
                : $"{branchSuffix} via {FormatStringPoolId(stringPoolId)}";
            return ApplyNotice(
                $"CTrunkDlg result {subtype.ToString(CultureInfo.InvariantCulture)} {branchText}: {text}",
                out message);
        }

        private static string ResolveStringPoolNoticeText(int stringPoolId, string fallback)
        {
            string text = MapleStoryStringPool.GetOrFallback(stringPoolId, fallback)?.Trim();
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static string FormatStringPoolId(int stringPoolId)
        {
            return $"StringPool 0x{stringPoolId.ToString("X", CultureInfo.InvariantCulture)}";
        }

        private bool ApplyNotice(string statusMessage, out string message)
        {
            _noticeCount++;
            IsOpen = true;
            StatusMessage = statusMessage;
            message = StatusMessage;
            return true;
        }

        private static bool TryParseSnapshotPayload(
            byte[] payload,
            out int slotLimit,
            out long meso,
            out Dictionary<InventoryType, IReadOnlyList<InventorySlotData>> snapshot,
            out string message)
        {
            slotLimit = 24;
            meso = 0;
            snapshot = null;
            message = null;

            using MemoryStream stream = new(payload, 1, payload.Length - 1, writable: false);
            using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);
            if (stream.Length - stream.Position < sizeof(int) + sizeof(long))
            {
                message = "Trunk snapshot packet is missing the slot-limit and meso header.";
                return false;
            }

            slotLimit = reader.ReadInt32();
            meso = reader.ReadInt64();
            snapshot = new Dictionary<InventoryType, IReadOnlyList<InventorySlotData>>(SnapshotInventoryOrder.Length);

            foreach (InventoryType type in SnapshotInventoryOrder)
            {
                if (stream.Length - stream.Position < sizeof(int))
                {
                    message = $"Trunk snapshot packet is missing the entry count for {type}.";
                    return false;
                }

                int count = reader.ReadInt32();
                if (count < 0)
                {
                    message = $"Trunk snapshot packet reported a negative entry count for {type}.";
                    return false;
                }

                List<InventorySlotData> rows = new(count);
                for (int i = 0; i < count; i++)
                {
                    if (stream.Length - stream.Position < sizeof(int) * 2)
                    {
                        message = $"Trunk snapshot packet ended while decoding {type} row {i.ToString(CultureInfo.InvariantCulture)}.";
                        return false;
                    }

                    rows.Add(new InventorySlotData
                    {
                        ItemId = reader.ReadInt32(),
                        Quantity = Math.Max(1, reader.ReadInt32()),
                        PreferredInventoryType = type,
                        ClientItemToken = i + 1
                    });
                }

                snapshot[type] = rows;
            }

            return true;
        }

        private static bool TryReadPacketString(BinaryReader reader, out string value)
        {
            value = string.Empty;
            Stream stream = reader?.BaseStream;
            if (stream == null || stream.Length - stream.Position < sizeof(short))
            {
                return false;
            }

            short length = reader.ReadInt16();
            if (length < 0 || stream.Length - stream.Position < length)
            {
                return false;
            }

            value = Encoding.ASCII.GetString(reader.ReadBytes(length)).Trim();
            return true;
        }

        private static byte[] BuildGetItemRequestPayload(int itemType, byte trunkRow)
        {
            return new[]
            {
                TrunkGetItemMode,
                unchecked((byte)Math.Clamp(itemType, 0, byte.MaxValue)),
                trunkRow
            };
        }

        private static byte[] BuildPutItemRequestPayload(short inventoryPosition, int itemId, int quantity)
        {
            short normalizedQuantity = (short)Math.Clamp(quantity, 1, short.MaxValue);
            ushort encodedPosition = unchecked((ushort)inventoryPosition);
            ushort encodedQuantity = unchecked((ushort)normalizedQuantity);
            byte[] payload = new byte[1 + sizeof(ushort) + sizeof(int) + sizeof(ushort)];
            payload[0] = TrunkPutItemMode;
            payload[1] = (byte)(encodedPosition & 0xFF);
            payload[2] = (byte)((encodedPosition >> 8) & 0xFF);
            payload[3] = (byte)(itemId & 0xFF);
            payload[4] = (byte)((itemId >> 8) & 0xFF);
            payload[5] = (byte)((itemId >> 16) & 0xFF);
            payload[6] = (byte)((itemId >> 24) & 0xFF);
            payload[7] = (byte)(encodedQuantity & 0xFF);
            payload[8] = (byte)((encodedQuantity >> 8) & 0xFF);
            return payload;
        }

        private static bool TryResolveTrunkItemType(int itemId, out int itemType)
        {
            itemType = itemId > 0 ? itemId / 1000000 : 0;
            return itemType is >= 1 and <= 5;
        }

        private static bool TryResolveStorageRowPosition(int ownerRowIndex, InventorySlotData slotData, out byte trunkRow)
        {
            if (slotData?.ClientItemToken is int clientRowToken
                && clientRowToken > 0
                && clientRowToken <= byte.MaxValue)
            {
                trunkRow = unchecked((byte)clientRowToken);
                return true;
            }

            trunkRow = 0;
            return false;
        }

        private static bool TryResolveInventoryPosition(int inventoryRowIndex, InventorySlotData slotData, out short inventoryPosition)
        {
            if (slotData?.ClientItemToken is int clientPositionToken
                && clientPositionToken > 0
                && clientPositionToken <= short.MaxValue)
            {
                inventoryPosition = unchecked((short)clientPositionToken);
                return true;
            }

            inventoryPosition = 0;
            return false;
        }

        private static bool IsSharableOnceCashOwnershipBlocked(InventorySlotData slotData)
        {
            if (slotData == null)
            {
                return false;
            }

            if (slotData.IsCashOwnershipLocked)
            {
                return true;
            }

            return slotData.CashItemSerialNumber.GetValueOrDefault() > 0
                && slotData.OwnerAccountId.GetValueOrDefault() > 0;
        }

        private static bool ShouldShowTrunkPreConfirmPrompt(InventorySlotData slotData)
        {
            if (slotData == null)
            {
                return false;
            }

            return slotData.CashItemSerialNumber.GetValueOrDefault() > 0
                || slotData.OwnerAccountId.GetValueOrDefault() > 0
                || slotData.OwnerCharacterId.GetValueOrDefault() > 0
                || slotData.IsCashOwnershipLocked;
        }
    }
}
