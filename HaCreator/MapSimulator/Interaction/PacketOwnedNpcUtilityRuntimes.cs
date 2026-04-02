using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketOwnedShopDialogRuntime
    {
        private sealed class ShopItemEntry
        {
            public int ItemId { get; init; }
            public string ItemName { get; init; } = string.Empty;
            public InventoryType InventoryType { get; init; }
            public int Price { get; init; }
            public int TokenItemId { get; init; }
            public int TokenPrice { get; init; }
            public int Quantity { get; init; }
            public int MaxPerSlot { get; init; }
            public int ItemPeriod { get; init; }
            public int LevelLimited { get; init; }
            public int DiscountRate { get; init; }
            public double? UnitPrice { get; init; }
            public bool IsRecharge { get; init; }
        }

        private readonly List<string> _recentNotes = new();
        private readonly List<ShopItemEntry> _buyItems = new();
        private readonly List<ShopItemEntry> _rechargeItems = new();
        private readonly Dictionary<InventoryType, int> _buyCountsByInventoryType = new();
        private string _activePane = "Buy";
        private string _lastTemplateNote = "No shop result packet has been applied yet.";
        private int _openCount;
        private int _resultCount;
        private int _lastPacketType = -1;
        private int _lastSubtype = -1;
        private int _lastTemplateStringPoolId = -1;
        private int _lastTemplateArgument;
        private int _npcTemplateId;
        private int _recommendedEquipCandidateCount;
        private int _lastDecodedOpenItemCount;

        internal bool IsOpen { get; private set; }
        internal string StatusMessage { get; private set; } = "CShopDlg::OnPacket idle.";

        internal void Close()
        {
            IsOpen = false;
            StatusMessage = "CShopDlg owner closed locally.";
        }

        internal bool TryApplyPacket(int packetType, byte[] payload, out string message)
        {
            payload ??= Array.Empty<byte>();
            _lastPacketType = packetType;

            switch (packetType)
            {
                case 364:
                    _openCount++;
                    return TryApplyOpenPacket(payload, out message);

                case 365:
                    return TryApplyResultPacket(payload, out message);

                default:
                    message = $"Unsupported NPC shop packet type {packetType}.";
                    return false;
            }
        }

        internal IReadOnlyList<string> BuildLines()
        {
            List<string> lines = new()
            {
                "Packet-owned owner: CShopDlg::OnPacket (364/365).",
                $"Dialog: {(IsOpen ? "open" : "closed")} | Active pane: {_activePane}",
                $"Packets: open={_openCount.ToString(CultureInfo.InvariantCulture)}, result={_resultCount.ToString(CultureInfo.InvariantCulture)}",
                $"Last packet: {DescribePacket()}",
                _npcTemplateId > 0
                    ? $"NPC template: {_npcTemplateId.ToString(CultureInfo.InvariantCulture)} | Buy items: {_buyItems.Count.ToString(CultureInfo.InvariantCulture)} | Recharge items: {_rechargeItems.Count.ToString(CultureInfo.InvariantCulture)}"
                    : "No decoded SetShopDlg payload is staged.",
                BuildBuyTabSummary(),
                _recommendedEquipCandidateCount > 0
                    ? $"Recommended-equip candidates: {_recommendedEquipCandidateCount.ToString(CultureInfo.InvariantCulture)} (the client still filters these by level, job, and gender before surfacing TabBuy)."
                    : "Recommended-equip candidates: none decoded.",
                BuildActivePanePreview(),
                _lastTemplateNote
            };

            lines.AddRange(_recentNotes);
            return lines;
        }

        internal string BuildFooter()
        {
            return StatusMessage;
        }

        private bool TryApplyOpenPacket(byte[] payload, out string message)
        {
            IsOpen = true;
            _activePane = "Buy";
            _buyItems.Clear();
            _rechargeItems.Clear();
            _buyCountsByInventoryType.Clear();
            _recommendedEquipCandidateCount = 0;
            _lastDecodedOpenItemCount = 0;

            if (!TryParseOpenPayload(payload, out string parsedStatus))
            {
                StatusMessage = $"CShopDlg::SetShopDlg opened the packet-owned NPC shop owner from packet 364 with {payload.Length.ToString(CultureInfo.InvariantCulture)} byte(s), but the simulator could not decode the item payload.";
                _lastTemplateNote = "SetShopDlg owns NPC id, item rows, recharge-unit-price entries, and TabBuy recommendation candidates.";
                AppendNote(StatusMessage);
                message = StatusMessage;
                return true;
            }

            StatusMessage = parsedStatus;
            _lastTemplateNote = "SetShopDlg decoded the shop inventory snapshot, recharge entries, and the client-side recommended-equip candidate pool.";
            AppendNote(StatusMessage);
            message = StatusMessage;
            return true;
        }

        private bool TryParseOpenPayload(byte[] payload, out string status)
        {
            status = string.Empty;
            if (payload.Length < sizeof(int) + sizeof(ushort))
            {
                return false;
            }

            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);

            _npcTemplateId = reader.ReadInt32();
            int count = reader.ReadUInt16();
            _lastDecodedOpenItemCount = count;

            for (int i = 0; i < count; i++)
            {
                if (!TryReadShopItem(reader, out ShopItemEntry entry))
                {
                    return false;
                }

                if (entry.IsRecharge)
                {
                    _rechargeItems.Add(entry);
                }

                if (entry.Price > 0 || entry.TokenPrice > 0)
                {
                    _buyItems.Add(entry);
                    if (!_buyCountsByInventoryType.TryAdd(entry.InventoryType, 1))
                    {
                        _buyCountsByInventoryType[entry.InventoryType]++;
                    }
                }

                if (entry.InventoryType == InventoryType.EQUIP && entry.TokenPrice == 0)
                {
                    _recommendedEquipCandidateCount++;
                }
            }

            status = $"CShopDlg::SetShopDlg decoded NPC {_npcTemplateId.ToString(CultureInfo.InvariantCulture)} with {_buyItems.Count.ToString(CultureInfo.InvariantCulture)} buy entry(s) and {_rechargeItems.Count.ToString(CultureInfo.InvariantCulture)} recharge entry(s).";
            return true;
        }

        private bool TryReadShopItem(BinaryReader reader, out ShopItemEntry entry)
        {
            entry = null;
            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < (sizeof(int) * 6) + sizeof(byte) + (sizeof(short) * 2))
            {
                return false;
            }

            int itemId = reader.ReadInt32();
            int price = reader.ReadInt32();
            int discountRate = reader.ReadByte();
            int tokenItemId = reader.ReadInt32();
            int tokenPrice = reader.ReadInt32();
            int itemPeriod = reader.ReadInt32();
            int levelLimited = reader.ReadInt32();

            bool isRecharge = itemId / 10000 is 207 or 233;
            double? unitPrice = null;
            int quantity;
            if (isRecharge)
            {
                if (stream.Length - stream.Position < sizeof(double))
                {
                    return false;
                }

                unitPrice = reader.ReadDouble();
                quantity = 1;
            }
            else
            {
                if (stream.Length - stream.Position < sizeof(short))
                {
                    return false;
                }

                quantity = Math.Max(1, (int)reader.ReadInt16());
            }

            if (stream.Length - stream.Position < sizeof(short))
            {
                return false;
            }

            int maxPerSlot = Math.Max(1, (int)reader.ReadInt16());
            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            string itemName = InventoryItemMetadataResolver.TryResolveItemName(itemId, out string resolvedName)
                ? resolvedName
                : $"Item {itemId.ToString(CultureInfo.InvariantCulture)}";

            entry = new ShopItemEntry
            {
                ItemId = itemId,
                ItemName = itemName,
                InventoryType = inventoryType,
                Price = price,
                TokenItemId = tokenItemId,
                TokenPrice = tokenPrice,
                Quantity = quantity,
                MaxPerSlot = maxPerSlot,
                ItemPeriod = itemPeriod,
                LevelLimited = levelLimited,
                DiscountRate = discountRate,
                UnitPrice = unitPrice,
                IsRecharge = isRecharge
            };
            return true;
        }

        private string BuildBuyTabSummary()
        {
            if (_buyItems.Count == 0)
            {
                return "Buy tabs: no decoded shop entries.";
            }

            List<string> segments = new();
            foreach (InventoryType inventoryType in new[]
                     {
                         InventoryType.EQUIP,
                         InventoryType.USE,
                         InventoryType.SETUP,
                         InventoryType.ETC,
                         InventoryType.CASH
                     })
            {
                if (_buyCountsByInventoryType.TryGetValue(inventoryType, out int count) && count > 0)
                {
                    segments.Add($"{inventoryType}:{count.ToString(CultureInfo.InvariantCulture)}");
                }
            }

            return segments.Count > 0
                ? $"Buy tabs: {string.Join(", ", segments)}"
                : "Buy tabs: decoded rows did not surface price-bearing entries.";
        }

        private string BuildActivePanePreview()
        {
            if (string.Equals(_activePane, "Sell", StringComparison.OrdinalIgnoreCase))
            {
                return "Sell pane: packet result 0 matched the client scrollbar/tab refresh branch, but sell rows still come from the local inventory owner rather than packet payload.";
            }

            IReadOnlyList<ShopItemEntry> source = string.Equals(_activePane, "Recharge", StringComparison.OrdinalIgnoreCase)
                ? _rechargeItems
                : _buyItems;
            if (source.Count == 0)
            {
                return string.Equals(_activePane, "Recharge", StringComparison.OrdinalIgnoreCase)
                    ? "Recharge pane: no throwing-star or bullet rows are currently decoded."
                    : "Buy pane: no decoded shop rows are currently available.";
            }

            int previewCount = Math.Min(3, source.Count);
            List<string> preview = new(previewCount);
            for (int i = 0; i < previewCount; i++)
            {
                ShopItemEntry entry = source[i];
                string priceText = entry.IsRecharge
                    ? $"unit={entry.UnitPrice?.ToString("0.##", CultureInfo.InvariantCulture) ?? "?"}"
                    : entry.TokenPrice > 0
                        ? $"{entry.Price.ToString(CultureInfo.InvariantCulture)} mesos + {entry.TokenPrice.ToString(CultureInfo.InvariantCulture)} token"
                        : $"{entry.Price.ToString(CultureInfo.InvariantCulture)} mesos";
                preview.Add($"{entry.ItemName} [{priceText}]");
            }

            return $"{_activePane} preview: {string.Join("; ", preview)}";
        }

        private bool TryApplyResultPacket(byte[] payload, out string message)
        {
            if (payload.Length == 0)
            {
                message = "NPC shop result packet 365 requires at least a subtype byte.";
                return false;
            }

            _resultCount++;
            IsOpen = true;
            _lastSubtype = payload[0];
            _lastTemplateStringPoolId = -1;
            _lastTemplateArgument = 0;

            switch (_lastSubtype)
            {
                case 0:
                    _activePane = "Sell";
                    StatusMessage = "CShopDlg result 0 refreshed the sell snapshot and moved the selection into the matching sell tab.";
                    _lastTemplateNote = "Result subtype 0 follows the client path that repositions the sell scrollbar around m_nSnapshotTI via get_tab_from_item_typeindex.";
                    break;
                case 1:
                case 5:
                case 9:
                    ApplyTemplateNotice(0x365, "CShopDlg result reported the StringPool 0x365 notice path.");
                    break;
                case 2:
                case 10:
                    ApplyTemplateNotice(0x1A8B, "CShopDlg result reported the StringPool 0x1A8B notice path.");
                    break;
                case 3:
                    ApplyTemplateNotice(0x366, "CShopDlg result reported the StringPool 0x366 notice path.");
                    break;
                case 4:
                case 8:
                    StatusMessage = $"CShopDlg result {_lastSubtype.ToString(CultureInfo.InvariantCulture)} completed without a visible dialog change.";
                    _lastTemplateNote = "The client returns immediately for this subtype.";
                    break;
                case 13:
                    ApplyTemplateNotice(0x153F, "CShopDlg result reported the StringPool 0x153F notice path.");
                    break;
                case 14:
                    if (!TryReadInt32(payload, 1, out _lastTemplateArgument))
                    {
                        message = "NPC shop result subtype 14 requires a 4-byte integer argument.";
                        return false;
                    }

                    _lastTemplateStringPoolId = 0x154F;
                    StatusMessage = $"CShopDlg result 14 formatted StringPool 0x154F with argument {_lastTemplateArgument.ToString(CultureInfo.InvariantCulture)}.";
                    _lastTemplateNote = "Subtype 14 is the client branch that formats StringPool 0x154F with a decoded 4-byte value.";
                    break;
                case 15:
                    if (!TryReadInt32(payload, 1, out _lastTemplateArgument))
                    {
                        message = "NPC shop result subtype 15 requires a 4-byte integer argument.";
                        return false;
                    }

                    _lastTemplateStringPoolId = 0x154E;
                    StatusMessage = $"CShopDlg result 15 formatted StringPool 0x154E with argument {_lastTemplateArgument.ToString(CultureInfo.InvariantCulture)}.";
                    _lastTemplateNote = "Subtype 15 is the client branch that formats StringPool 0x154E with a decoded 4-byte value.";
                    break;
                case 16:
                    _activePane = "Recharge";
                    ApplyTemplateNotice(0x368, "CShopDlg result reported the StringPool 0x368 notice path.");
                    break;
                case 17:
                    ApplyTemplateNotice(0x16ED, "CShopDlg result reported the StringPool 0x16ED notice path.");
                    break;
                case 18:
                    ApplyTemplateNotice(0xFB2, "CShopDlg result reported the StringPool 0xFB2 notice path.");
                    break;
                case 19:
                    if (payload.Length > 1 && payload[1] != 0 && TryReadMapleString(payload, 2, out string shopNotice))
                    {
                        StatusMessage = $"CShopDlg result 19 delivered a custom notice: {shopNotice}";
                        _lastTemplateNote = "Subtype 19 used the branch that decodes a packet-authored ZXString<char> notice.";
                    }
                    else
                    {
                        ApplyTemplateNotice(0x369, "CShopDlg result 19 fell back to the default StringPool 0x369 notice path.");
                    }
                    break;
                default:
                    ApplyTemplateNotice(0x369, $"CShopDlg result {_lastSubtype.ToString(CultureInfo.InvariantCulture)} fell back to the default StringPool 0x369 notice path.");
                    break;
            }

            AppendNote(StatusMessage);
            message = StatusMessage;
            return true;
        }

        private void ApplyTemplateNotice(int stringPoolId, string statusMessage)
        {
            _lastTemplateStringPoolId = stringPoolId;
            StatusMessage = statusMessage;
            _lastTemplateNote = $"The simulator tracked the client notice branch for StringPool 0x{stringPoolId.ToString("X", CultureInfo.InvariantCulture)}.";
        }

        private string DescribePacket()
        {
            if (_lastPacketType < 0)
            {
                return "none";
            }

            return _lastPacketType == 365
                ? $"365 / subtype {_lastSubtype.ToString(CultureInfo.InvariantCulture)}"
                : $"364 / npc {_npcTemplateId.ToString(CultureInfo.InvariantCulture)} / items {_lastDecodedOpenItemCount.ToString(CultureInfo.InvariantCulture)}";
        }

        private void AppendNote(string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return;
            }

            _recentNotes.Insert(0, note);
            if (_recentNotes.Count > 3)
            {
                _recentNotes.RemoveAt(_recentNotes.Count - 1);
            }
        }

        private static bool TryReadInt32(byte[] payload, int offset, out int value)
        {
            value = 0;
            if (payload == null || offset < 0 || payload.Length < offset + sizeof(int))
            {
                return false;
            }

            value = BitConverter.ToInt32(payload, offset);
            return true;
        }

        private static bool TryReadMapleString(byte[] payload, int offset, out string value)
        {
            value = string.Empty;
            if (payload == null || offset < 0 || payload.Length < offset + sizeof(short))
            {
                return false;
            }

            short length = BitConverter.ToInt16(payload, offset);
            if (length < 0 || payload.Length < offset + sizeof(short) + length)
            {
                return false;
            }

            value = Encoding.ASCII.GetString(payload, offset + sizeof(short), length).Trim();
            return true;
        }
    }

    internal sealed class PacketOwnedStoreBankDialogRuntime
    {
        private readonly struct StoreInventoryGroup
        {
            internal StoreInventoryGroup(InventoryType inventoryType, int flagMask)
            {
                InventoryType = inventoryType;
                FlagMask = flagMask;
            }

            internal InventoryType InventoryType { get; }
            internal int FlagMask { get; }
        }

        private readonly List<string> _recentNotes = new();
        private readonly Dictionary<InventoryType, int> _decodedCountsByType = new();
        private static readonly StoreInventoryGroup[] StoreInventoryGroups =
        {
            new(InventoryType.EQUIP, 4),
            new(InventoryType.USE, 8),
            new(InventoryType.SETUP, 16),
            new(InventoryType.ETC, 32),
            new(InventoryType.CASH, 64)
        };

        private int _openCount;
        private int _packet369Count;
        private int _packet370Count;
        private int _lastPacketType = -1;
        private int _lastSubtype = -1;
        private int _npcTemplateId;
        private int _slotCount;
        private int _money;
        private int _dbcharFlagMask;
        private int _pendingGetAllPassingDay;
        private int _pendingGetAllFee;
        private int _lastPromptContextValue;
        private int _lastPromptTokenValue;
        private int _lastPromptChannelId = -1;

        internal bool IsOpen { get; private set; }
        internal bool HasPendingGetAllRequest { get; private set; }
        internal bool GetAllRequestWasAccepted { get; private set; }
        internal string StatusMessage { get; private set; } = "CStoreBankDlg::OnPacket idle.";

        internal void Close()
        {
            IsOpen = false;
            StatusMessage = "CStoreBankDlg owner closed locally.";
        }

        internal string ConsumePendingGetAllRequest()
        {
            if (!HasPendingGetAllRequest)
            {
                return "No packet-authored store-bank get-all request is waiting.";
            }

            HasPendingGetAllRequest = false;
            GetAllRequestWasAccepted = true;
            StatusMessage = _pendingGetAllFee > 0
                ? $"Accepted packet-authored store-bank get-all request for {_pendingGetAllPassingDay.ToString(CultureInfo.InvariantCulture)} passing day(s) with fee {_pendingGetAllFee.ToString(CultureInfo.InvariantCulture)}."
                : $"Accepted packet-authored store-bank get-all request for {_pendingGetAllPassingDay.ToString(CultureInfo.InvariantCulture)} passing day(s) with the zero-fee StringPool 0xDC5 branch.";
            AppendNote(StatusMessage);
            return StatusMessage;
        }

        internal bool TryApplyPacket(int packetType, byte[] payload, out string message)
        {
            payload ??= Array.Empty<byte>();
            _lastPacketType = packetType;

            switch (packetType)
            {
                case 369:
                    _packet369Count++;
                    return TryApply369Packet(payload, out message);
                case 370:
                    _packet370Count++;
                    return TryApply370Packet(payload, out message);
                default:
                    message = $"Unsupported store-bank packet type {packetType}.";
                    return false;
            }
        }

        internal IReadOnlyList<string> BuildLines()
        {
            List<string> lines = new()
            {
                "Packet-owned owner: CStoreBankDlg::OnPacket (369/370).",
                $"Dialog: {(IsOpen ? "open" : "closed")} | Pending get-all: {(HasPendingGetAllRequest ? "yes" : "no")} | Accepted: {GetAllRequestWasAccepted}",
                $"Packets: 369={_packet369Count.ToString(CultureInfo.InvariantCulture)}, 370={_packet370Count.ToString(CultureInfo.InvariantCulture)}",
                $"Last packet: {DescribePacket()}",
                _npcTemplateId > 0
                    ? $"NPC template: {_npcTemplateId.ToString(CultureInfo.InvariantCulture)} | Slot count: {_slotCount.ToString(CultureInfo.InvariantCulture)} | Money: {_money.ToString(CultureInfo.InvariantCulture)} | Flags: 0x{_dbcharFlagMask.ToString("X", CultureInfo.InvariantCulture)}"
                    : "No decoded SetStoreBankDlg payload is staged.",
                BuildDecodedInventorySummary(),
                HasPendingGetAllRequest
                    ? (_pendingGetAllFee > 0
                        ? $"Pending get-all modal: {_pendingGetAllPassingDay.ToString(CultureInfo.InvariantCulture)} passing day(s), fee {_pendingGetAllFee.ToString(CultureInfo.InvariantCulture)} (StringPool 0xDC4)."
                        : $"Pending get-all modal: {_pendingGetAllPassingDay.ToString(CultureInfo.InvariantCulture)} passing day(s), no fee (StringPool 0xDC5).")
                    : "No get-all confirmation modal is currently staged.",
                _lastPromptContextValue != 0 || _lastPromptTokenValue != 0 || _lastPromptChannelId >= 0
                    ? BuildShipmentPromptSummary()
                    : "No shipment prompt is currently staged."
            };

            lines.AddRange(_recentNotes);
            return lines;
        }

        internal string BuildFooter()
        {
            return StatusMessage;
        }

        private bool TryApply369Packet(byte[] payload, out string message)
        {
            if (payload.Length == 0)
            {
                message = "Store-bank packet 369 requires a subtype byte.";
                return false;
            }

            IsOpen = true;
            _lastSubtype = payload[0];
            StatusMessage = _lastSubtype switch
            {
                30 => "CStoreBankDlg result 30 cleared the list and disabled the Get button (StringPool 0xDC6 branch).",
                31 => "CStoreBankDlg result 31 reported the StringPool 0xDC7 notice branch.",
                32 => "CStoreBankDlg result 32 reported the StringPool 0xDC8 notice branch.",
                33 => "CStoreBankDlg result 33 reported the StringPool 0xDC9 notice branch.",
                34 => "CStoreBankDlg result 34 reported the StringPool 0xDCA notice branch.",
                _ => $"CStoreBankDlg packet 369 subtype {_lastSubtype.ToString(CultureInfo.InvariantCulture)} is not modeled beyond owner tracking."
            };

            if (_lastSubtype == 30)
            {
                HasPendingGetAllRequest = false;
                GetAllRequestWasAccepted = false;
                _decodedCountsByType.Clear();
            }

            AppendNote(StatusMessage);
            message = StatusMessage;
            return true;
        }

        private bool TryApply370Packet(byte[] payload, out string message)
        {
            if (payload.Length == 0)
            {
                message = "Store-bank packet 370 requires a subtype byte.";
                return false;
            }

            _lastSubtype = payload[0];
            switch (_lastSubtype)
            {
                case 35:
                    _openCount++;
                    return TryApplyOpenPacket(payload, out message);

                case 36:
                    if (payload.Length < 1 + (sizeof(int) * 2))
                    {
                        message = "Store-bank packet 370 subtype 36 requires passing-day and fee integers.";
                        return false;
                    }

                    _pendingGetAllPassingDay = BitConverter.ToInt32(payload, 1);
                    _pendingGetAllFee = BitConverter.ToInt32(payload, 5);
                    HasPendingGetAllRequest = true;
                    GetAllRequestWasAccepted = false;
                    IsOpen = true;
                    StatusMessage = _pendingGetAllFee > 0
                        ? $"CStoreBankDlg staged the SendGetAllRequest modal for {_pendingGetAllPassingDay.ToString(CultureInfo.InvariantCulture)} passing day(s) with fee {_pendingGetAllFee.ToString(CultureInfo.InvariantCulture)}."
                        : $"CStoreBankDlg staged the zero-fee SendGetAllRequest modal for {_pendingGetAllPassingDay.ToString(CultureInfo.InvariantCulture)} passing day(s).";
                    break;

                case 37:
                    if (payload.Length < 1 + (sizeof(int) * 2) + sizeof(byte))
                    {
                        message = "Store-bank packet 370 subtype 37 requires two 4-byte values and a channel byte.";
                        return false;
                    }

                    _lastPromptContextValue = BitConverter.ToInt32(payload, 1);
                    _lastPromptTokenValue = BitConverter.ToInt32(payload, 5);
                    _lastPromptChannelId = payload[9];
                    IsOpen = true;
                    StatusMessage = _lastPromptChannelId >= 0xFE || _lastPromptTokenValue == 999999999
                        ? $"CStoreBankDlg showed the fallback shipment prompt branch for context {_lastPromptContextValue.ToString(CultureInfo.InvariantCulture)}."
                        : $"CStoreBankDlg showed the channel-routed shipment prompt for context {_lastPromptContextValue.ToString(CultureInfo.InvariantCulture)}, token {_lastPromptTokenValue.ToString(CultureInfo.InvariantCulture)}, channel {_lastPromptChannelId.ToString(CultureInfo.InvariantCulture)}.";
                    break;

                case 38:
                    IsOpen = true;
                    StatusMessage = "CStoreBankDlg showed the StringPool 0xDC3 notice branch.";
                    break;

                default:
                    IsOpen = true;
                    StatusMessage = $"CStoreBankDlg packet 370 subtype {_lastSubtype.ToString(CultureInfo.InvariantCulture)} is not modeled beyond owner tracking.";
                    break;
            }

            AppendNote(StatusMessage);
            message = StatusMessage;
            return true;
        }

        private bool TryApplyOpenPacket(byte[] payload, out string message)
        {
            IsOpen = true;
            HasPendingGetAllRequest = false;
            GetAllRequestWasAccepted = false;

            if (!TryParseOpenPayload(payload))
            {
                StatusMessage = $"CStoreBankDlg::SetStoreBankDlg opened the packet-owned store-bank owner from packet 370 subtype 35 with {payload.Length.ToString(CultureInfo.InvariantCulture)} byte(s), but only the owner lifecycle could be confirmed.";
                AppendNote(StatusMessage);
                message = StatusMessage;
                return true;
            }

            StatusMessage = $"CStoreBankDlg::SetStoreBankDlg decoded NPC {_npcTemplateId.ToString(CultureInfo.InvariantCulture)} with slotCount={_slotCount.ToString(CultureInfo.InvariantCulture)}, money={_money.ToString(CultureInfo.InvariantCulture)}, and {BuildDecodedInventorySummaryCore()}.";
            AppendNote(StatusMessage);
            message = StatusMessage;
            return true;
        }

        private bool TryParseOpenPayload(byte[] payload)
        {
            _decodedCountsByType.Clear();
            if (payload.Length < sizeof(int) + sizeof(byte) + sizeof(long))
            {
                return false;
            }

            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);
            _npcTemplateId = reader.ReadInt32();
            _slotCount = reader.ReadByte();
            long flags = reader.ReadInt64();
            _dbcharFlagMask = (int)flags;
            _money = (flags & 2L) != 0 && stream.Length - stream.Position >= sizeof(int)
                ? reader.ReadInt32()
                : 0;

            for (int i = 0; i < StoreInventoryGroups.Length; i++)
            {
                StoreInventoryGroup group = StoreInventoryGroups[i];
                if ((flags & group.FlagMask) == 0)
                {
                    continue;
                }

                if (stream.Length - stream.Position < sizeof(byte))
                {
                    return false;
                }

                _decodedCountsByType[group.InventoryType] = reader.ReadByte();
            }

            return true;
        }

        private string BuildDecodedInventorySummary()
        {
            return _decodedCountsByType.Count > 0
                ? $"Decoded inventory groups: {BuildDecodedInventorySummaryCore()}."
                : "Decoded inventory groups: none surfaced in the staged payload.";
        }

        private string BuildDecodedInventorySummaryCore()
        {
            List<string> segments = new();
            foreach (InventoryType inventoryType in new[]
                     {
                         InventoryType.EQUIP,
                         InventoryType.USE,
                         InventoryType.SETUP,
                         InventoryType.ETC,
                         InventoryType.CASH
                     })
            {
                if (_decodedCountsByType.TryGetValue(inventoryType, out int count))
                {
                    segments.Add($"{inventoryType}:{count.ToString(CultureInfo.InvariantCulture)}");
                }
            }

            return segments.Count > 0 ? string.Join(", ", segments) : "no type counts";
        }

        private string BuildShipmentPromptSummary()
        {
            if (_lastPromptChannelId >= 0xFE || _lastPromptTokenValue == 999999999)
            {
                return $"Shipment prompt: fallback StringPool 0xDC1 branch for context {_lastPromptContextValue.ToString(CultureInfo.InvariantCulture)}.";
            }

            return $"Shipment prompt: context {_lastPromptContextValue.ToString(CultureInfo.InvariantCulture)}, token {_lastPromptTokenValue.ToString(CultureInfo.InvariantCulture)}, channel {_lastPromptChannelId.ToString(CultureInfo.InvariantCulture)}.";
        }

        private string DescribePacket()
        {
            if (_lastPacketType < 0)
            {
                return "none";
            }

            return $"{_lastPacketType.ToString(CultureInfo.InvariantCulture)} / subtype {_lastSubtype.ToString(CultureInfo.InvariantCulture)}";
        }

        private void AppendNote(string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return;
            }

            _recentNotes.Insert(0, note);
            if (_recentNotes.Count > 3)
            {
                _recentNotes.RemoveAt(_recentNotes.Count - 1);
            }
        }
    }

    internal sealed class PacketOwnedBattleRecordRuntime
    {
        private readonly List<string> _recentNotes = new();
        private int _packetCount420;
        private int _packetCount421;
        private int _packetCount422;
        private int _packetCount423;
        private int _lastPacketType = -1;
        private int _pageIndex;
        private bool _armedBySetupPacket;

        internal bool IsOpen { get; private set; }
        internal bool OnCalc { get; private set; }
        internal bool ServerOnCalc { get; private set; }
        internal bool DotTrackingEnabled { get; private set; }
        internal int TotalDamage { get; private set; }
        internal int TotalHits { get; private set; }
        internal int MaxDamage { get; private set; }
        internal int MinDamage { get; private set; }
        internal int LastDotDamage { get; private set; }
        internal int LastDotHitCount { get; private set; }
        internal int? LastAttrRate { get; private set; }
        internal string StatusMessage { get; private set; } = "CBattleRecordMan::OnPacket idle.";

        internal void Close()
        {
            ResetSession(clearNotes: false);
            StatusMessage = "CBattleRecordMan owner closed locally.";
        }

        internal void SelectPage(int pageIndex)
        {
            _pageIndex = Math.Clamp(pageIndex, 0, 2);
            StatusMessage = _pageIndex switch
            {
                1 => "Battle Record page switched to DOT totals.",
                2 => "Battle Record page switched to packet log.",
                _ => "Battle Record page switched to summary."
            };
        }

        internal bool TryApplyPacket(int packetType, byte[] payload, out string message)
        {
            payload ??= Array.Empty<byte>();
            _lastPacketType = packetType;

            switch (packetType)
            {
                case 420:
                    _packetCount420++;
                    ResetSession(clearNotes: false);
                    IsOpen = true;
                    OnCalc = true;
                    DotTrackingEnabled = true;
                    _armedBySetupPacket = true;
                    StatusMessage = "Battle-record packet 420 armed a fresh DOT-tracking session before the manager's 421/422 handlers run.";
                    break;

                case 421:
                    _packetCount421++;
                    if (!TryApplyDotDamageInfo(payload, out message))
                    {
                        return false;
                    }

                    AppendNote(StatusMessage);
                    return true;

                case 422:
                    _packetCount422++;
                    if (payload.Length < 1)
                    {
                        message = "Battle-record packet 422 requires a 1-byte server-on-calc flag.";
                        return false;
                    }

                    ServerOnCalc = payload[0] != 0;
                    if (!ServerOnCalc)
                    {
                        IsOpen = false;
                        OnCalc = false;
                        StatusMessage = "CBattleRecordMan rejected the server-on-calc request result and followed the client UI_Close(35) branch.";
                    }
                    else
                    {
                        if (!_armedBySetupPacket)
                        {
                            OnCalc = true;
                            DotTrackingEnabled = true;
                        }

                        IsOpen = true;
                        StatusMessage = "CBattleRecordMan accepted the server-on-calc request result and kept battle-record collection active.";
                    }
                    break;

                case 423:
                    _packetCount423++;
                    ResetSession(clearNotes: false);
                    StatusMessage = "Battle-record packet 423 tore down the active manager session and cleared the DOT aggregate state.";
                    break;

                default:
                    message = $"Unsupported battle-record packet type {packetType}.";
                    return false;
            }

            AppendNote(StatusMessage);
            message = StatusMessage;
            return true;
        }

        internal IReadOnlyList<string> BuildLines()
        {
            List<string> lines = new()
            {
                "Packet-owned owner: CBattleRecordMan::OnPacket (420-423).",
                $"Page: {ResolvePageName()} | Window: {(IsOpen ? "open" : "closed")}",
                $"Calc flags: onCalc={OnCalc}, serverOnCalc={ServerOnCalc}, dot={DotTrackingEnabled}, armed420={_armedBySetupPacket}",
                $"Packets: 420={_packetCount420.ToString(CultureInfo.InvariantCulture)}, 421={_packetCount421.ToString(CultureInfo.InvariantCulture)}, 422={_packetCount422.ToString(CultureInfo.InvariantCulture)}, 423={_packetCount423.ToString(CultureInfo.InvariantCulture)}"
            };

            switch (_pageIndex)
            {
                case 1:
                    lines.Add($"DOT totals: hits={TotalHits.ToString(CultureInfo.InvariantCulture)}, totalDamage={TotalDamage.ToString(CultureInfo.InvariantCulture)}, lastHit={LastDotDamage.ToString(CultureInfo.InvariantCulture)} x{LastDotHitCount.ToString(CultureInfo.InvariantCulture)}");
                    lines.Add($"Damage bounds: min={FormatDamage(MinDamage)}, max={FormatDamage(MaxDamage)}, avg={FormatAverageDamage()}");
                    lines.Add(LastAttrRate.HasValue
                        ? $"Last attr rate: {LastAttrRate.Value.ToString(CultureInfo.InvariantCulture)}"
                        : "Last attr rate: none");
                    break;

                case 2:
                    lines.Add($"Last packet: {(_lastPacketType < 0 ? "none" : _lastPacketType.ToString(CultureInfo.InvariantCulture))}");
                    lines.Add(StatusMessage);
                    lines.AddRange(_recentNotes);
                    break;

                default:
                    lines.Add($"Total damage: {TotalDamage.ToString(CultureInfo.InvariantCulture)} across {TotalHits.ToString(CultureInfo.InvariantCulture)} DOT hit(s)");
                    lines.Add($"Damage bounds: min={FormatDamage(MinDamage)}, max={FormatDamage(MaxDamage)}, avg={FormatAverageDamage()}");
                    lines.Add(LastAttrRate.HasValue
                        ? $"Last attr rate: {LastAttrRate.Value.ToString(CultureInfo.InvariantCulture)}"
                        : "Last attr rate: none");
                    break;
            }

            return lines;
        }

        internal string BuildFooter()
        {
            return StatusMessage;
        }

        private bool TryApplyDotDamageInfo(byte[] payload, out string message)
        {
            if (payload.Length < (sizeof(int) * 2) + sizeof(byte))
            {
                message = "Battle-record packet 421 requires damage, hit count, and an attr-rate flag.";
                return false;
            }

            int dotDamage = BitConverter.ToInt32(payload, 0);
            int hitCount = BitConverter.ToInt32(payload, 4);
            byte hasAttrRate = payload[8];
            int? attrRate = null;
            if (hasAttrRate != 0)
            {
                if (payload.Length < 13)
                {
                    message = "Battle-record packet 421 reported an attr-rate flag without the trailing 4-byte attr-rate value.";
                    return false;
                }

                attrRate = BitConverter.ToInt32(payload, 9);
            }

            if (!OnCalc || !ServerOnCalc)
            {
                StatusMessage = "CBattleRecordMan ignored DOT damage info because m_bOnCalc and m_bServerOnCalc were not both armed yet.";
                message = StatusMessage;
                return true;
            }

            if (hitCount > 0)
            {
                LastDotDamage = dotDamage;
                LastDotHitCount = hitCount;
                TotalHits += hitCount;
                TotalDamage += dotDamage * hitCount;
                MaxDamage = Math.Max(MaxDamage, dotDamage);
                MinDamage = MinDamage == 0 ? dotDamage : Math.Min(MinDamage, dotDamage);
            }

            LastAttrRate = attrRate;
            StatusMessage = $"CBattleRecordMan applied DOT damage info: {dotDamage.ToString(CultureInfo.InvariantCulture)} x {hitCount.ToString(CultureInfo.InvariantCulture)}.";
            message = StatusMessage;
            return true;
        }

        private void ResetSession(bool clearNotes)
        {
            IsOpen = false;
            OnCalc = false;
            ServerOnCalc = false;
            DotTrackingEnabled = false;
            TotalDamage = 0;
            TotalHits = 0;
            MaxDamage = 0;
            MinDamage = 0;
            LastDotDamage = 0;
            LastDotHitCount = 0;
            LastAttrRate = null;
            _armedBySetupPacket = false;
            if (clearNotes)
            {
                _recentNotes.Clear();
            }
        }

        private string ResolvePageName()
        {
            return _pageIndex switch
            {
                1 => "DOT",
                2 => "Packets",
                _ => "Summary"
            };
        }

        private string FormatAverageDamage()
        {
            if (TotalHits <= 0)
            {
                return "n/a";
            }

            double averageDamage = TotalDamage / (double)TotalHits;
            return averageDamage.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatDamage(int value)
        {
            return value > 0 ? value.ToString(CultureInfo.InvariantCulture) : "n/a";
        }

        private void AppendNote(string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return;
            }

            _recentNotes.Insert(0, note);
            if (_recentNotes.Count > 3)
            {
                _recentNotes.RemoveAt(_recentNotes.Count - 1);
            }
        }
    }
}
