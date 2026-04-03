using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketOwnedSocialUtilityDialogDispatcher
    {
        private readonly MapleTvRuntime _mapleTvRuntime;
        private readonly PacketOwnedParcelDialogRuntime _parcelDialogRuntime;
        private readonly PacketOwnedTrunkDialogRuntime _trunkDialogRuntime;
        private readonly MessengerRuntime _messengerRuntime;
        private string _lastDispatchSummary = "Packet-owned social utility dispatcher idle.";

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
            if (applied)
            {
                _lastDispatchSummary = $"CMapleTVMan::OnPacket dispatched packet {packetType.ToString(CultureInfo.InvariantCulture)}.";
            }

            return applied;
        }

        internal bool TryApplyParcelPacket(byte[] payload, out string message)
        {
            bool applied = _parcelDialogRuntime.TryApplyPacket(payload, out message);
            if (applied)
            {
                _lastDispatchSummary = $"CParcelDlg::OnPacket dispatched subtype {_parcelDialogRuntime.LastSubtype.ToString(CultureInfo.InvariantCulture)}.";
            }

            return applied;
        }

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
                _lastDispatchSummary = "CParcelDlg::OnPacket applied a packet-owned parcel delivery payload.";
            }

            return applied;
        }

        internal bool TryApplyTrunkPacket(byte[] payload, out string message)
        {
            bool applied = _trunkDialogRuntime.TryApplyPacket(payload, out message);
            if (applied)
            {
                _lastDispatchSummary = $"CTrunkDlg::OnPacket dispatched subtype {_trunkDialogRuntime.LastSubtype.ToString(CultureInfo.InvariantCulture)}.";
            }

            return applied;
        }

        internal string ApplyMessengerPacket(MessengerPacketType packetType, byte[] payload)
        {
            string result = _messengerRuntime.ApplyPacketPayload(packetType, payload);
            _lastDispatchSummary = $"CUIMessenger::OnPacket dispatched logical subtype {packetType}.";
            return result;
        }

        internal string DescribeParcelStatus()
        {
            return _parcelDialogRuntime.DescribeStatus();
        }

        internal string DescribeTrunkStatus()
        {
            return _trunkDialogRuntime.DescribeStatus();
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
    }

    internal sealed class PacketOwnedParcelDialogRuntime
    {
        private const byte ParcelFlagRead = 1 << 0;
        private const byte ParcelFlagKeep = 1 << 1;
        private const byte ParcelFlagClaimed = 1 << 2;
        private const byte ParcelFlagHasItem = 1 << 3;
        private const byte ParcelFlagHasMeso = 1 << 4;

        private readonly MemoMailboxManager _memoMailbox;
        private int _openCount;
        private int _noticeCount;
        private int _arrivalNoticeCount;
        private int _deliveryCount;
        private int _removalCount;
        private string _lastAlarmSender = string.Empty;

        internal PacketOwnedParcelDialogRuntime(MemoMailboxManager memoMailbox)
        {
            _memoMailbox = memoMailbox;
        }

        internal bool IsOpen { get; private set; }
        internal int LastSubtype { get; private set; } = -1;
        internal string StatusMessage { get; private set; } = "CParcelDlg::OnPacket idle.";

        internal bool TryApplyPacket(byte[] payload, out string message)
        {
            message = null;
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
                    _memoMailbox.SetActiveTab(ParcelDialogTab.QuickSend);
                    StatusMessage = "CParcelDlg packet 26 opened the packet-owned quick-delivery owner.";
                    message = StatusMessage;
                    return true;
                case 27:
                    return TryApplyAlarmPacket(payload, expectsSender: false, out message);
                default:
                    IsOpen = true;
                    _noticeCount++;
                    if (LastSubtype == 18)
                    {
                        _memoMailbox.ResetDraftState();
                        StatusMessage = "CParcelDlg result 18 reset the send-info branch on the packet-owned parcel owner.";
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
                StatusMessage = $"CParcelDlg packet 24 applied mailbox payload ownership for parcel '{subject.Trim()}'.";
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
            _memoMailbox.ReplacePacketOwnedParcelSession(session.ReceiveEntries, ParcelDialogTab.Receive, out string sessionMessage);
            if (session.ArrivalNoticeEntries.Count > 0)
            {
                _arrivalNoticeCount += session.ArrivalNoticeEntries.Count;
                _lastAlarmSender = session.ArrivalNoticeEntries[0].Sender ?? string.Empty;
            }

            string arrivalSummary = DescribeArrivalNoticeSummary(session.ArrivalNoticeEntries);
            StatusMessage = $"CParcelDlg packet 8 opened the packet-owned receive owner through {(modeTwoOpen ? "mode 2" : "mode 0")} and applied {session.ReceiveEntries.Count.ToString(CultureInfo.InvariantCulture)} receive row(s){arrivalSummary}. {sessionMessage}";
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

            int memoId = BitConverter.ToInt32(payload, 1);
            byte resultCode = payload[5];
            _memoMailbox.DeleteMemo(memoId);
            _removalCount++;
            IsOpen = true;
            StatusMessage = resultCode == 3
                ? $"CParcelDlg packet 23 removed parcel #{memoId} through the claim-success branch."
                : $"CParcelDlg packet 23 removed parcel #{memoId} through the discard-result branch ({resultCode.ToString(CultureInfo.InvariantCulture)}).";
            message = StatusMessage;
            return true;
        }

        private bool TryApplyDeliverPacket(byte[] payload, out string message)
        {
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

            IsOpen = true;
            LastSubtype = 24;
            _deliveryCount++;
            StatusMessage = $"CParcelDlg packet 24 decoded PARCEL::Decode payload serial {entry.ParcelSerial.ToString(CultureInfo.InvariantCulture)} from {entry.Sender}.";
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

            bool hasAttachment = stream.Position < stream.Length && reader.ReadByte() != 0;
            _noticeCount++;
            IsOpen = true;
            _lastAlarmSender = sender ?? string.Empty;
            StatusMessage = expectsSender
                ? $"CParcelDlg packet {LastSubtype.ToString(CultureInfo.InvariantCulture)} raised a packet-owned parcel alarm from {sender} (attachment={hasAttachment})."
                : $"CParcelDlg packet {LastSubtype.ToString(CultureInfo.InvariantCulture)} raised the packet-owned empty-sender parcel alarm branch (attachment={hasAttachment}).";
            message = StatusMessage;
            return true;
        }

        private static string DescribeArrivalNoticeSummary(IReadOnlyList<PacketOwnedParcelDecodedEntry> arrivalEntries)
        {
            if (arrivalEntries == null || arrivalEntries.Count == 0)
            {
                return string.Empty;
            }

            string sender = arrivalEntries[0]?.Sender;
            if (string.IsNullOrWhiteSpace(sender))
            {
                return $", plus {arrivalEntries.Count.ToString(CultureInfo.InvariantCulture)} arrival notice(s)";
            }

            return $", plus {arrivalEntries.Count.ToString(CultureInfo.InvariantCulture)} arrival notice(s) led by {sender}";
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

        internal PacketOwnedTrunkDialogRuntime(Func<SimulatorStorageRuntime> storageRuntimeResolver)
        {
            _storageRuntimeResolver = storageRuntimeResolver;
        }

        internal bool IsOpen { get; private set; }
        internal int LastSubtype { get; private set; } = -1;
        internal string StatusMessage { get; private set; } = "CTrunkDlg::OnPacket idle.";

        internal bool TryApplyPacket(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length == 0)
            {
                message = "Trunk dialog packet payload must contain a subtype byte.";
                return false;
            }

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
                    return ApplyNotice("CTrunkDlg result 10 followed the StringPool 0x366 trunk notice branch.", out message);
                case 11:
                case 16:
                    return ApplyNotice($"CTrunkDlg result {LastSubtype.ToString(CultureInfo.InvariantCulture)} followed the StringPool 0x1A8B trunk notice branch.", out message);
                case 12:
                    return ApplyNotice("CTrunkDlg result 12 followed the StringPool 0x374 trunk notice branch.", out message);
                case 17:
                    return ApplyNotice("CTrunkDlg result 17 followed the StringPool 0x373 trunk notice branch.", out message);
                case 23:
                    return ApplyNotice("CTrunkDlg result 23 followed the StringPool 0x16ED trunk notice branch.", out message);
                case 24:
                    return TryApplyCustomNoticePacket(payload, out message);
                default:
                    return ApplyNotice($"CTrunkDlg result {LastSubtype.ToString(CultureInfo.InvariantCulture)} fell back to the default StringPool 0x369 trunk notice branch.", out message);
            }
        }

        internal string DescribeStatus()
        {
            SimulatorStorageRuntime runtime = _storageRuntimeResolver();
            int usedSlots = runtime?.GetUsedSlotCount() ?? 0;
            long meso = runtime?.GetMesoCount() ?? 0;
            return $"Trunk packet-owner {(IsOpen ? "open" : "idle")}. Used slots={usedSlots}, meso={meso.ToString(CultureInfo.InvariantCulture)}, packets open={_openCount}, refresh={_refreshCount}, notice={_noticeCount}. Last subtype={LastSubtype.ToString(CultureInfo.InvariantCulture)}. {StatusMessage}";
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
                return ApplyNotice("CTrunkDlg result 24 followed the default StringPool 0x369 notice branch.", out message);
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
                        Quantity = Math.Max(1, reader.ReadInt32())
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
    }
}
