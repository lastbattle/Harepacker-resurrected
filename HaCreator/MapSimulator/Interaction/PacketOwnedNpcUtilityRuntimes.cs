using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal readonly record struct PacketOwnedNpcUtilityOutboundRequest(
        int Opcode,
        IReadOnlyList<byte> Payload,
        string Summary);

    internal readonly record struct StoreBankDecodedItemSnapshot(
        byte SlotType,
        int ItemId,
        int ClientStock,
        int Quantity,
        InventoryType InventoryType,
        bool HasCashSerialNumber,
        long CashSerialNumber,
        long BaseExpirationTime,
        string ClientDisplayName,
        string Title,
        string MetadataSummary,
        int EncodedByteLength);

    internal readonly record struct StoreBankOwnerRowSnapshot(
        int OwnerRowIndex,
        int PacketGroupRowIndex,
        InventoryType InventoryType,
        int ItemId,
        int ClientStock,
        string PrimaryText,
        string SecondaryText,
        string SelectionSummary,
        bool ShowsClientStock,
        bool IsCashItem,
        bool HasCashSerialNumber,
        bool IsRechargeBundle,
        bool WasRetainedFromPreviousSnapshot);

    internal sealed class PacketOwnedShopDialogRuntime
    {
        private sealed class ShopItemEntry
        {
            public int PacketIndex { get; init; }
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
        internal int NpcTemplateId => _npcTemplateId;
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

        internal bool TryBuildBuyOutboundRequest(int itemId, int quantity, out PacketOwnedNpcUtilityOutboundRequest request, out string message)
        {
            request = default;
            message = null;
            if (!IsOpen)
            {
                message = "NPC shop buy outbound request was ignored because the packet-owned CShopDlg owner is not open.";
                return false;
            }

            if (quantity <= 0)
            {
                message = "NPC shop buy outbound request requires a positive quantity.";
                return false;
            }

            ShopItemEntry entry = FindBuyEntry(itemId);
            if (entry == null)
            {
                message = $"NPC shop buy outbound request could not find item {itemId.ToString(CultureInfo.InvariantCulture)} in the decoded buy list.";
                return false;
            }

            int encodedPrice = ResolveEncodedBuyPrice(entry);
            byte[] payload = BuildShopBuyPayload(entry.PacketIndex, entry.ItemId, quantity, encodedPrice);
            request = new PacketOwnedNpcUtilityOutboundRequest(
                66,
                payload,
                $"Mirrored CShopDlg::SendBuyRequest row {entry.PacketIndex.ToString(CultureInfo.InvariantCulture)} for {entry.ItemName} x{quantity.ToString(CultureInfo.InvariantCulture)} (opcode 66, mode 0, price {encodedPrice.ToString(CultureInfo.InvariantCulture)}).");
            return true;
        }

        internal bool TryApplyLocalBuy(IInventoryRuntime inventory, int itemId, int quantity, out string message)
        {
            message = null;
            if (!IsOpen)
            {
                message = "NPC shop buy request was ignored because the packet-owned CShopDlg owner is not open.";
                return false;
            }

            if (inventory == null)
            {
                message = "NPC shop buy request requires a live inventory runtime.";
                return false;
            }

            if (quantity <= 0)
            {
                message = "NPC shop buy quantity must be positive.";
                return false;
            }

            ShopItemEntry entry = FindBuyEntry(itemId);
            if (entry == null)
            {
                message = $"NPC shop buy request could not find item {itemId.ToString(CultureInfo.InvariantCulture)} in the decoded buy list.";
                return false;
            }

            int deliveredQuantity = Math.Max(1, entry.Quantity) * quantity;
            if (!inventory.CanAcceptItem(entry.InventoryType, entry.ItemId, deliveredQuantity, InventoryItemMetadataResolver.ResolveMaxStack(entry.InventoryType)))
            {
                message = $"NPC shop buy request for {entry.ItemName} x{deliveredQuantity.ToString(CultureInfo.InvariantCulture)} could not fit in {entry.InventoryType}.";
                return false;
            }

            long mesosCost = Math.Max(0, entry.Price) * (long)quantity;
            if (mesosCost > 0 && !inventory.TryConsumeMeso(mesosCost))
            {
                message = $"NPC shop buy request for {entry.ItemName} failed because mesos ({mesosCost.ToString(CultureInfo.InvariantCulture)}) were insufficient.";
                return false;
            }

            if (entry.TokenItemId > 0 && entry.TokenPrice > 0)
            {
                int tokenQuantity = checked(entry.TokenPrice * quantity);
                InventoryType tokenInventoryType = InventoryItemMetadataResolver.ResolveInventoryType(entry.TokenItemId);
                if (tokenInventoryType == InventoryType.NONE || !inventory.TryConsumeItem(tokenInventoryType, entry.TokenItemId, tokenQuantity))
                {
                    if (mesosCost > 0)
                    {
                        inventory.AddMeso(mesosCost);
                    }

                    message = $"NPC shop buy request for {entry.ItemName} failed because token item {entry.TokenItemId.ToString(CultureInfo.InvariantCulture)} x{tokenQuantity.ToString(CultureInfo.InvariantCulture)} was unavailable.";
                    return false;
                }
            }

            for (int i = 0; i < deliveredQuantity; i++)
            {
                inventory.AddItem(entry.InventoryType, entry.ItemId, texture: null, quantity: 1);
            }

            _activePane = "Buy";
            StatusMessage = $"Applied local NPC-shop buy mutation for {entry.ItemName} x{deliveredQuantity.ToString(CultureInfo.InvariantCulture)} (mesos {mesosCost.ToString(CultureInfo.InvariantCulture)}).";
            AppendNote(StatusMessage);
            message = StatusMessage;
            return true;
        }

        internal bool TryApplyLocalSell(IInventoryRuntime inventory, int itemId, int quantity, out string message)
        {
            message = null;
            if (!IsOpen)
            {
                message = "NPC shop sell request was ignored because the packet-owned CShopDlg owner is not open.";
                return false;
            }

            if (inventory == null)
            {
                message = "NPC shop sell request requires a live inventory runtime.";
                return false;
            }

            if (itemId <= 0 || quantity <= 0)
            {
                message = "NPC shop sell request requires a positive item id and quantity.";
                return false;
            }

            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            if (inventoryType == InventoryType.NONE)
            {
                message = $"NPC shop sell request could not resolve inventory type for item {itemId.ToString(CultureInfo.InvariantCulture)}.";
                return false;
            }

            if (!inventory.TryConsumeItem(inventoryType, itemId, quantity))
            {
                message = $"NPC shop sell request failed because item {itemId.ToString(CultureInfo.InvariantCulture)} x{quantity.ToString(CultureInfo.InvariantCulture)} was unavailable.";
                return false;
            }

            int basePrice = ResolveSellPrice(itemId);
            long mesosGain = Math.Max(1, basePrice) * (long)quantity;
            inventory.AddMeso(mesosGain);

            string itemName = InventoryItemMetadataResolver.TryResolveItemName(itemId, out string resolvedName)
                ? resolvedName
                : $"Item {itemId.ToString(CultureInfo.InvariantCulture)}";

            _activePane = "Sell";
            StatusMessage = $"Applied local NPC-shop sell mutation for {itemName} x{quantity.ToString(CultureInfo.InvariantCulture)} (+{mesosGain.ToString(CultureInfo.InvariantCulture)} mesos).";
            AppendNote(StatusMessage);
            message = StatusMessage;
            return true;
        }

        internal bool TryBuildSellOutboundRequest(IInventoryRuntime inventory, int itemId, int quantity, out PacketOwnedNpcUtilityOutboundRequest request, out string message)
        {
            request = default;
            message = null;
            if (!IsOpen)
            {
                message = "NPC shop sell outbound request was ignored because the packet-owned CShopDlg owner is not open.";
                return false;
            }

            if (inventory == null)
            {
                message = "NPC shop sell outbound request requires a live inventory runtime.";
                return false;
            }

            if (itemId <= 0 || quantity <= 0)
            {
                message = "NPC shop sell outbound request requires a positive item id and quantity.";
                return false;
            }

            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            if (inventoryType == InventoryType.NONE)
            {
                message = $"NPC shop sell outbound request could not resolve inventory type for item {itemId.ToString(CultureInfo.InvariantCulture)}.";
                return false;
            }

            if (!TryResolveInventorySlotIndex(inventory, inventoryType, itemId, quantity, out int slotIndex))
            {
                message = $"NPC shop sell outbound request could not resolve a live slot for item {itemId.ToString(CultureInfo.InvariantCulture)} x{quantity.ToString(CultureInfo.InvariantCulture)}.";
                return false;
            }

            string itemName = InventoryItemMetadataResolver.TryResolveItemName(itemId, out string resolvedName)
                ? resolvedName
                : $"Item {itemId.ToString(CultureInfo.InvariantCulture)}";
            byte[] payload = BuildShopSellPayload(slotIndex, itemId, quantity);
            request = new PacketOwnedNpcUtilityOutboundRequest(
                66,
                payload,
                $"Mirrored CShopDlg::SendSellRequest slot {slotIndex.ToString(CultureInfo.InvariantCulture)} for {itemName} x{quantity.ToString(CultureInfo.InvariantCulture)} (opcode 66, mode 1).");
            return true;
        }

        internal bool TryApplyLocalRecharge(IInventoryRuntime inventory, int itemId, int targetQuantity, out string message)
        {
            message = null;
            if (!IsOpen)
            {
                message = "NPC shop recharge request was ignored because the packet-owned CShopDlg owner is not open.";
                return false;
            }

            if (inventory == null)
            {
                message = "NPC shop recharge request requires a live inventory runtime.";
                return false;
            }

            ShopItemEntry entry = FindRechargeEntry(itemId);
            if (entry == null)
            {
                message = $"NPC shop recharge request could not find item {itemId.ToString(CultureInfo.InvariantCulture)} in the decoded recharge list.";
                return false;
            }

            int currentQuantity = inventory.GetItemCount(entry.InventoryType, entry.ItemId);
            int resolvedTargetQuantity = targetQuantity > 0 ? targetQuantity : Math.Max(1, entry.MaxPerSlot);
            if (resolvedTargetQuantity <= currentQuantity)
            {
                message = $"NPC shop recharge request for {entry.ItemName} did not change quantity ({currentQuantity.ToString(CultureInfo.InvariantCulture)} already available).";
                return false;
            }

            int refillQuantity = resolvedTargetQuantity - currentQuantity;
            if (!inventory.CanAcceptItem(entry.InventoryType, entry.ItemId, refillQuantity, InventoryItemMetadataResolver.ResolveMaxStack(entry.InventoryType)))
            {
                message = $"NPC shop recharge request for {entry.ItemName} could not fit {refillQuantity.ToString(CultureInfo.InvariantCulture)} item(s) into {entry.InventoryType}.";
                return false;
            }

            double unitPrice = entry.UnitPrice ?? 0d;
            long mesosCost = (long)Math.Ceiling(Math.Max(0d, unitPrice) * refillQuantity);
            if (mesosCost > 0 && !inventory.TryConsumeMeso(mesosCost))
            {
                message = $"NPC shop recharge request for {entry.ItemName} failed because mesos ({mesosCost.ToString(CultureInfo.InvariantCulture)}) were insufficient.";
                return false;
            }

            inventory.AddItem(entry.InventoryType, entry.ItemId, texture: null, quantity: refillQuantity);

            _activePane = "Recharge";
            StatusMessage = $"Applied local NPC-shop recharge mutation for {entry.ItemName}: {currentQuantity.ToString(CultureInfo.InvariantCulture)} -> {resolvedTargetQuantity.ToString(CultureInfo.InvariantCulture)} (mesos {mesosCost.ToString(CultureInfo.InvariantCulture)}).";
            AppendNote(StatusMessage);
            message = StatusMessage;
            return true;
        }

        internal bool TryBuildRechargeOutboundRequest(IInventoryRuntime inventory, int itemId, out PacketOwnedNpcUtilityOutboundRequest request, out string message)
        {
            request = default;
            message = null;
            if (!IsOpen)
            {
                message = "NPC shop recharge outbound request was ignored because the packet-owned CShopDlg owner is not open.";
                return false;
            }

            if (inventory == null)
            {
                message = "NPC shop recharge outbound request requires a live inventory runtime.";
                return false;
            }

            ShopItemEntry entry = FindRechargeEntry(itemId);
            if (entry == null)
            {
                message = $"NPC shop recharge outbound request could not find item {itemId.ToString(CultureInfo.InvariantCulture)} in the decoded recharge list.";
                return false;
            }

            if (!TryResolveInventorySlotIndex(inventory, entry.InventoryType, itemId, 1, out int slotIndex))
            {
                message = $"NPC shop recharge outbound request could not resolve a live slot for {entry.ItemName}.";
                return false;
            }

            byte[] payload = BuildShopRechargePayload(slotIndex);
            request = new PacketOwnedNpcUtilityOutboundRequest(
                66,
                payload,
                $"Mirrored CShopDlg::SendRechargeRequest slot {slotIndex.ToString(CultureInfo.InvariantCulture)} for {entry.ItemName} (opcode 66, mode 2).");
            return true;
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
                if (!TryReadShopItem(reader, i, out ShopItemEntry entry))
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

        private bool TryReadShopItem(BinaryReader reader, int packetIndex, out ShopItemEntry entry)
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
                PacketIndex = packetIndex,
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

        private ShopItemEntry FindBuyEntry(int itemId)
        {
            for (int i = 0; i < _buyItems.Count; i++)
            {
                if (_buyItems[i].ItemId == itemId)
                {
                    return _buyItems[i];
                }
            }

            return null;
        }

        private ShopItemEntry FindRechargeEntry(int itemId)
        {
            for (int i = 0; i < _rechargeItems.Count; i++)
            {
                if (_rechargeItems[i].ItemId == itemId)
                {
                    return _rechargeItems[i];
                }
            }

            return null;
        }

        private static int ResolveEncodedBuyPrice(ShopItemEntry entry)
        {
            if (entry == null)
            {
                return 0;
            }

            int discountedByRate = 0;
            if (entry.DiscountRate > 0 && entry.Price > 0)
            {
                double value = (100d - entry.DiscountRate) * entry.Price / 100d;
                discountedByRate = ((int)(value * 10d)) % 10 >= 5
                    ? (int)(value + 1d)
                    : (int)value;
            }

            return discountedByRate > 0 ? discountedByRate : Math.Max(0, entry.Price);
        }

        private static byte[] BuildShopBuyPayload(int packetIndex, int itemId, int quantity, int encodedPrice)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write((byte)0);
            writer.Write((short)Math.Clamp(packetIndex, 0, ushort.MaxValue));
            writer.Write(itemId);
            writer.Write((short)Math.Clamp(quantity, 1, ushort.MaxValue));
            writer.Write(encodedPrice);
            return stream.ToArray();
        }

        private static byte[] BuildShopSellPayload(int slotIndex, int itemId, int quantity)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write((byte)1);
            writer.Write((short)Math.Clamp(slotIndex, 1, ushort.MaxValue));
            writer.Write(itemId);
            writer.Write((short)Math.Clamp(quantity, 1, ushort.MaxValue));
            return stream.ToArray();
        }

        private static byte[] BuildShopRechargePayload(int slotIndex)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write((byte)2);
            writer.Write((short)Math.Clamp(slotIndex, 1, ushort.MaxValue));
            return stream.ToArray();
        }

        private static bool TryResolveInventorySlotIndex(IInventoryRuntime inventory, InventoryType inventoryType, int itemId, int minimumQuantity, out int slotIndex)
        {
            slotIndex = -1;
            if (inventory == null)
            {
                return false;
            }

            IReadOnlyList<InventorySlotData> slots = inventory.GetSlots(inventoryType);
            if (slots == null)
            {
                return false;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotData slot = slots[i];
                if (slot == null
                    || slot.ItemId != itemId
                    || slot.IsDisabled
                    || slot.Quantity < Math.Max(1, minimumQuantity))
                {
                    continue;
                }

                slotIndex = i + 1;
                return true;
            }

            return false;
        }

        private static int ResolveSellPrice(int itemId)
        {
            InventoryItemTooltipMetadata metadata = InventoryItemMetadataResolver.ResolveTooltipMetadata(itemId);
            if (metadata?.Price is int listedPrice && listedPrice > 0)
            {
                return Math.Max(1, listedPrice / 2);
            }

            return 1;
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
        private sealed class StoreBankEquipData
        {
            internal short RemainingUpgradeCount { get; init; }
            internal byte UpgradeCount { get; init; }
            internal short Strength { get; init; }
            internal short Dexterity { get; init; }
            internal short Intelligence { get; init; }
            internal short Luck { get; init; }
            internal short Hp { get; init; }
            internal short Mp { get; init; }
            internal short WeaponAttack { get; init; }
            internal short MagicAttack { get; init; }
            internal short WeaponDefense { get; init; }
            internal short MagicDefense { get; init; }
            internal short Accuracy { get; init; }
            internal short Avoidability { get; init; }
            internal short Craft { get; init; }
            internal short Speed { get; init; }
            internal short Jump { get; init; }
            internal short Attribute { get; init; }
            internal byte LevelUpType { get; init; }
            internal byte Level { get; init; }
            internal int Experience { get; init; }
            internal int Durability { get; init; }
            internal int ItemUpgradeCount { get; init; }
            internal byte Grade { get; init; }
            internal byte BonusUpgradeCount { get; init; }
            internal short Option1 { get; init; }
            internal short Option2 { get; init; }
            internal short Option3 { get; init; }
            internal short Socket1 { get; init; }
            internal short Socket2 { get; init; }
            internal long? NonCashSerialNumber { get; init; }
            internal short Hands { get; init; }
            internal long ExpirationTime { get; init; }
            internal int IucOrExp { get; init; }
            internal long EquippedTime { get; init; }
            internal int PreviousBonusExpRate { get; init; }
        }

        private sealed class StoreBankBundleData
        {
            internal short Attribute { get; init; }
            internal long RechargeableSerialNumber { get; init; }
        }

        private sealed class StoreBankPetData
        {
            internal byte Level { get; init; }
            internal short Closeness { get; init; }
            internal byte Fullness { get; init; }
            internal long ExpirationTime { get; init; }
            internal long DateDead { get; init; }
            internal short PetAttribute { get; init; }
            internal ushort Skill { get; init; }
            internal int RemainingLife { get; init; }
            internal short Attribute { get; init; }
            internal short Fatigue { get; init; }
        }

        private sealed class StoreBankItemEntry
        {
            internal int ClientRowIndex { get; init; }
            internal string ClientDisplayName { get; init; } = string.Empty;
            internal int ClientStock { get; init; }
            internal int EncodedByteLength { get; init; }
            internal int ItemId { get; init; }
            internal string ItemName { get; init; } = string.Empty;
            internal string Title { get; init; } = string.Empty;
            internal InventoryType InventoryType { get; init; }
            internal InventoryType PacketGroupInventoryType { get; init; }
            internal int PacketGroupRowIndex { get; init; }
            internal bool WasRetainedFromPreviousSnapshot { get; init; }
            internal int Quantity { get; init; }
            internal byte SlotType { get; init; }
            internal bool HasCashSerialNumber { get; init; }
            internal long ItemSerialNumber { get; init; }
            internal long CashSerialNumber { get; init; }
            internal long BaseExpirationTime { get; init; }
            internal bool IsRechargeBundle { get; init; }
            internal string MetadataSummary { get; init; } = string.Empty;
            internal StoreBankEquipData EquipData { get; init; }
            internal StoreBankBundleData BundleData { get; init; }
            internal StoreBankPetData PetData { get; init; }
        }

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
        private readonly List<StoreBankItemEntry> _decodedItems = new();
        private readonly Dictionary<InventoryType, int> _decodedCountsByType = new();
        private readonly Dictionary<InventoryType, int> _retainedCountsByType = new();
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
        private int _pendingFeeCalculationOwnerRowIndex = -1;
        private int _pendingFeeCalculationPacketRowIndex = -1;
        private int _lastPromptContextValue;
        private int _lastPromptTokenValue;
        private int _lastPromptChannelId = -1;

        internal bool IsOpen { get; private set; }
        internal int NpcTemplateId => _npcTemplateId;
        internal bool HasPendingGetAllRequest { get; private set; }
        internal bool HasPendingFeeCalculationRequest { get; private set; }
        internal bool GetAllRequestWasAccepted { get; private set; }
        internal bool HasDecodedItems => _decodedItems.Count > 0;
        internal bool IsOwnerGetButtonEnabled => IsOpen && !HasPendingFeeCalculationRequest && (HasPendingGetAllRequest || HasDecodedItems);
        internal int OwnerMoney => _money;
        internal string StatusMessage { get; private set; } = "CStoreBankDlg::OnPacket idle.";

        internal void Close()
        {
            IsOpen = false;
            ResetPendingFeeCalculationRequest();
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

        internal bool TryBuildPendingGetAllOutboundRequest(out PacketOwnedNpcUtilityOutboundRequest request, out string message)
        {
            request = default;
            message = null;
            if (!HasPendingGetAllRequest)
            {
                message = "Store-bank get-all outbound request was ignored because no packet-authored prompt is waiting.";
                return false;
            }

            request = new PacketOwnedNpcUtilityOutboundRequest(
                69,
                new byte[] { 27 },
                $"Mirrored CStoreBankDlg::SendGetAllRequest for {_pendingGetAllPassingDay.ToString(CultureInfo.InvariantCulture)} passing day(s) and fee {_pendingGetAllFee.ToString(CultureInfo.InvariantCulture)} (opcode 69, mode 27).");
            return true;
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
                BuildRetainedInventorySummary(),
                HasPendingFeeCalculationRequest
                    ? $"Pending BtGet fee request: owner row {_pendingFeeCalculationOwnerRowIndex.ToString(CultureInfo.InvariantCulture)}, packet row {_pendingFeeCalculationPacketRowIndex.ToString(CultureInfo.InvariantCulture)} (opcode 69, mode 26)."
                    : "No BtGet fee request is currently staged.",
                HasPendingGetAllRequest
                    ? (_pendingGetAllFee > 0
                        ? $"Pending get-all modal: {_pendingGetAllPassingDay.ToString(CultureInfo.InvariantCulture)} passing day(s), fee {_pendingGetAllFee.ToString(CultureInfo.InvariantCulture)} (StringPool 0xDC4)."
                        : $"Pending get-all modal: {_pendingGetAllPassingDay.ToString(CultureInfo.InvariantCulture)} passing day(s), no fee (StringPool 0xDC5).")
                    : "No get-all confirmation modal is currently staged.",
                _lastPromptContextValue != 0 || _lastPromptTokenValue != 0 || _lastPromptChannelId >= 0
                    ? BuildShipmentPromptSummary()
                    : "No shipment prompt is currently staged."
            };

            lines.AddRange(BuildDecodedItemPreviewLines());
            lines.AddRange(_recentNotes);
            return lines;
        }

        internal string BuildFooter()
        {
            return StatusMessage;
        }

        internal IReadOnlyList<StoreBankOwnerRowSnapshot> BuildOwnerRows()
        {
            if (_decodedItems.Count == 0)
            {
                return Array.Empty<StoreBankOwnerRowSnapshot>();
            }

            StoreBankOwnerRowSnapshot[] rows = new StoreBankOwnerRowSnapshot[_decodedItems.Count];
            for (int i = 0; i < _decodedItems.Count; i++)
            {
                StoreBankItemEntry item = _decodedItems[i];
                string primaryText = item.Quantity > 1
                    ? $"{item.ItemName} x{item.Quantity.ToString(CultureInfo.InvariantCulture)}"
                    : item.ItemName;

                List<string> secondaryParts = new()
                {
                    item.InventoryType.ToString(),
                    $"row {item.PacketGroupRowIndex.ToString(CultureInfo.InvariantCulture)}"
                };

                if (item.IsRechargeBundle)
                {
                    secondaryParts.Add("recharge");
                }

                if (item.HasCashSerialNumber)
                {
                    secondaryParts.Add("cash");
                }

                if (item.WasRetainedFromPreviousSnapshot)
                {
                    secondaryParts.Add("retained");
                }

                if (!string.IsNullOrWhiteSpace(item.MetadataSummary))
                {
                    secondaryParts.Add(item.MetadataSummary);
                }

                rows[i] = new StoreBankOwnerRowSnapshot(
                    i,
                    item.PacketGroupRowIndex,
                    item.InventoryType,
                    item.ItemId,
                    item.ClientStock,
                    primaryText,
                    string.Join(" | ", secondaryParts),
                    BuildOwnerSelectionSummary(item),
                    ShowsClientStock(item),
                    IsCashItem(item),
                    item.HasCashSerialNumber,
                    item.IsRechargeBundle,
                    item.WasRetainedFromPreviousSnapshot);
            }

            return rows;
        }

        internal bool TryBuildSelectedGetOutboundRequest(int ownerRowIndex, out PacketOwnedNpcUtilityOutboundRequest request, out string message)
        {
            request = default;
            if (!IsOpen)
            {
                StatusMessage = "CStoreBankDlg ignored BtGet because the owner is closed.";
                AppendNote(StatusMessage);
                message = StatusMessage;
                return false;
            }

            if (HasPendingGetAllRequest)
            {
                StatusMessage = "CStoreBankDlg BtGet is currently owned by the staged SendGetAllRequest modal.";
                AppendNote(StatusMessage);
                message = StatusMessage;
                return false;
            }

            if (HasPendingFeeCalculationRequest)
            {
                StatusMessage = _pendingFeeCalculationPacketRowIndex > 0
                    ? $"CStoreBankDlg ignored repeated BtGet while fee calculation for packet row {_pendingFeeCalculationPacketRowIndex.ToString(CultureInfo.InvariantCulture)} is still pending."
                    : "CStoreBankDlg ignored repeated BtGet while a fee calculation request is still pending.";
                AppendNote(StatusMessage);
                message = StatusMessage;
                return false;
            }

            if (ownerRowIndex < 0 || ownerRowIndex >= _decodedItems.Count)
            {
                NotifyOwnerGetButtonPressed(ownerRowIndex);
                message = StatusMessage;
                return false;
            }

            StoreBankItemEntry selectedItem = _decodedItems[ownerRowIndex];
            HasPendingFeeCalculationRequest = true;
            _pendingFeeCalculationOwnerRowIndex = ownerRowIndex + 1;
            _pendingFeeCalculationPacketRowIndex = selectedItem.PacketGroupRowIndex;
            StatusMessage = $"CStoreBankDlg BtGet mirrored SendCalculateFeeRequest for selected row {selectedItem.PacketGroupRowIndex.ToString(CultureInfo.InvariantCulture)} ({selectedItem.ItemName}) and armed opcode 69, mode 26.";
            AppendNote(StatusMessage);
            request = new PacketOwnedNpcUtilityOutboundRequest(
                69,
                new byte[] { 26 },
                $"Mirrored CStoreBankDlg::SendCalculateFeeRequest for owner row {_pendingFeeCalculationOwnerRowIndex.ToString(CultureInfo.InvariantCulture)} / packet row {selectedItem.PacketGroupRowIndex.ToString(CultureInfo.InvariantCulture)} ({selectedItem.ItemName}) (opcode 69, mode 26).");
            message = StatusMessage;
            return true;
        }

        internal void NotifyOwnerGetButtonPressed()
        {
            NotifyOwnerGetButtonPressed(-1);
        }

        internal void NotifyOwnerGetButtonPressed(int ownerRowIndex)
        {
            if (!IsOpen)
            {
                StatusMessage = "CStoreBankDlg ignored BtGet because the owner is closed.";
            }
            else if (HasPendingGetAllRequest)
            {
                StatusMessage = "CStoreBankDlg BtGet accepted the staged SendGetAllRequest prompt.";
            }
            else if (HasPendingFeeCalculationRequest)
            {
                StatusMessage = _pendingFeeCalculationPacketRowIndex > 0
                    ? $"CStoreBankDlg ignored repeated BtGet while fee calculation for packet row {_pendingFeeCalculationPacketRowIndex.ToString(CultureInfo.InvariantCulture)} is still pending."
                    : "CStoreBankDlg ignored repeated BtGet while a fee calculation request is still pending.";
            }
            else if (ownerRowIndex >= 0 && ownerRowIndex < _decodedItems.Count)
            {
                StoreBankItemEntry selectedItem = _decodedItems[ownerRowIndex];
                StatusMessage = $"CStoreBankDlg BtGet acknowledged selected row {selectedItem.PacketGroupRowIndex.ToString(CultureInfo.InvariantCulture)} ({selectedItem.ItemName}), but the native retrieval follow-up after SendCalculateFeeRequest is still not modeled.";
            }
            else if (HasDecodedItems)
            {
                StatusMessage = "CStoreBankDlg BtGet is selection-owned when packet rows are staged; select a row first.";
            }
            else
            {
                StatusMessage = "CStoreBankDlg BtGet is disabled because no staged rows or get-all prompt are available.";
            }

            AppendNote(StatusMessage);
        }

        private bool TryApply369Packet(byte[] payload, out string message)
        {
            if (payload.Length == 0)
            {
                message = "Store-bank packet 369 requires a subtype byte.";
                return false;
            }

            IsOpen = true;
            ResetPendingFeeCalculationRequest();
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
                _decodedItems.Clear();
                _decodedCountsByType.Clear();
                _retainedCountsByType.Clear();
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

            ResetPendingFeeCalculationRequest();
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
            ResetShipmentPromptState();

            if (payload.Length <= 1 || !TryParseOpenPayload(payload, 1))
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

        private void ResetShipmentPromptState()
        {
            _lastPromptContextValue = 0;
            _lastPromptTokenValue = 0;
            _lastPromptChannelId = -1;
        }

        private void ResetPendingFeeCalculationRequest()
        {
            HasPendingFeeCalculationRequest = false;
            _pendingFeeCalculationOwnerRowIndex = -1;
            _pendingFeeCalculationPacketRowIndex = -1;
        }

        private bool TryParseOpenPayload(byte[] payload, int startOffset)
        {
            Dictionary<InventoryType, int> previousCountsByType = new(_decodedCountsByType);
            List<StoreBankItemEntry> previousItems = new(_decodedItems);
            _decodedCountsByType.Clear();
            _retainedCountsByType.Clear();
            _decodedItems.Clear();
            if (payload == null
                || startOffset < 0
                || payload.Length - startOffset < sizeof(int) + sizeof(byte) + sizeof(long))
            {
                return false;
            }

            using MemoryStream stream = new(payload, startOffset, payload.Length - startOffset, writable: false);
            using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);
            _npcTemplateId = reader.ReadInt32();
            _slotCount = reader.ReadByte();
            long flags = reader.ReadInt64();
            _dbcharFlagMask = (int)flags;
            HashSet<InventoryType> decodedGroups = new();
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

                int groupCount = reader.ReadByte();
                decodedGroups.Add(group.InventoryType);
                _decodedCountsByType[group.InventoryType] = groupCount;
                for (int itemIndex = 0; itemIndex < groupCount; itemIndex++)
                {
                    if (!TryReadStoreBankItem(reader, group.InventoryType, itemIndex, out StoreBankItemEntry entry))
                    {
                        return false;
                    }

                    _decodedItems.Add(entry);
                }
            }

            // CStoreBankDlg::SetItems reuses existing rows for inventory groups that are absent
            // from the new dbchar flag mask instead of dropping them from the dialog outright.
            for (int i = 0; i < StoreInventoryGroups.Length; i++)
            {
                InventoryType inventoryType = StoreInventoryGroups[i].InventoryType;
                if (decodedGroups.Contains(inventoryType))
                {
                    continue;
                }

                if (previousCountsByType.TryGetValue(inventoryType, out int previousCount))
                {
                    _decodedCountsByType[inventoryType] = previousCount;
                }

                for (int itemIndex = 0; itemIndex < previousItems.Count; itemIndex++)
                {
                    StoreBankItemEntry previousItem = previousItems[itemIndex];
                    if (previousItem.PacketGroupInventoryType == inventoryType)
                    {
                        _decodedItems.Add(CloneRetainedItem(previousItem));
                        _retainedCountsByType[inventoryType] = _retainedCountsByType.TryGetValue(inventoryType, out int retainedCount)
                            ? retainedCount + 1
                            : 1;
                    }
                }
            }

            return true;
        }

        private string BuildDecodedInventorySummary()
        {
            return _decodedCountsByType.Count > 0
                ? $"Decoded inventory groups: {BuildDecodedInventorySummaryCore()}."
                : "Decoded inventory groups: none surfaced in the staged payload.";
        }

        private string BuildRetainedInventorySummary()
        {
            if (_retainedCountsByType.Count == 0)
            {
                return "Retained rows: none copied from omitted packet groups.";
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
                if (_retainedCountsByType.TryGetValue(inventoryType, out int count) && count > 0)
                {
                    segments.Add($"{inventoryType}:{count.ToString(CultureInfo.InvariantCulture)}");
                }
            }

            return segments.Count > 0
                ? $"Retained rows from omitted packet groups: {string.Join(", ", segments)}."
                : "Retained rows: none copied from omitted packet groups.";
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

        private IReadOnlyList<string> BuildDecodedItemPreviewLines()
        {
            if (_decodedItems.Count == 0)
            {
                return new[]
                {
                    "Decoded item rows: none surfaced in the staged payload."
                };
            }

            List<string> lines = new();
            lines.Add($"Decoded item rows: {_decodedItems.Count.ToString(CultureInfo.InvariantCulture)} row(s) staged from SetStoreBankDlg.");
            foreach (InventoryType inventoryType in new[]
                     {
                         InventoryType.EQUIP,
                         InventoryType.USE,
                         InventoryType.SETUP,
                         InventoryType.ETC,
                         InventoryType.CASH
                     })
            {
                List<string> names = new();
                for (int i = 0; i < _decodedItems.Count; i++)
                {
                    StoreBankItemEntry item = _decodedItems[i];
                    if (item.PacketGroupInventoryType != inventoryType)
                    {
                        continue;
                    }

                    string quantitySuffix = item.Quantity > 1
                        ? $" x{item.Quantity.ToString(CultureInfo.InvariantCulture)}"
                        : string.Empty;
                    string metadataSuffix = string.IsNullOrWhiteSpace(item.MetadataSummary)
                        ? string.Empty
                        : $" | {item.MetadataSummary}";
                    string clientObjectSuffix = BuildDecodedItemClientObjectSuffix(item);
                    string bodySuffix = BuildDecodedItemBodySuffix(item);
                    names.Add($"#{item.PacketGroupRowIndex.ToString(CultureInfo.InvariantCulture)} {item.ItemName}{quantitySuffix}{BuildDecodedItemMarker(item)}{BuildDecodedItemSerialMarker(item)}{clientObjectSuffix}{metadataSuffix}{bodySuffix}");
                    if (names.Count >= 4)
                    {
                        break;
                    }
                }

                if (names.Count > 0)
                {
                    lines.Add($"{inventoryType}: {string.Join(", ", names)}");
                }
            }

            return lines.Count > 1
                ? lines
                : new[]
                {
                    $"Decoded item rows: {_decodedItems.Count.ToString(CultureInfo.InvariantCulture)} row(s) decoded, but none mapped into supported inventory previews."
                };
        }

        private string BuildShipmentPromptSummary()
        {
            if (_lastPromptChannelId >= 0xFE || _lastPromptTokenValue == 999999999)
            {
                return $"Shipment prompt: fallback StringPool 0xDC1 branch for context {_lastPromptContextValue.ToString(CultureInfo.InvariantCulture)}.";
            }

            return $"Shipment prompt: context {_lastPromptContextValue.ToString(CultureInfo.InvariantCulture)}, token {_lastPromptTokenValue.ToString(CultureInfo.InvariantCulture)}, channel {_lastPromptChannelId.ToString(CultureInfo.InvariantCulture)}.";
        }

        private static string BuildOwnerSelectionSummary(StoreBankItemEntry item)
        {
            List<string> parts = new()
            {
                $"packet row {item.PacketGroupRowIndex.ToString(CultureInfo.InvariantCulture)}",
                item.InventoryType.ToString()
            };

            if (item.Quantity > 1)
            {
                parts.Add($"qty {item.Quantity.ToString(CultureInfo.InvariantCulture)}");
            }

            if (item.HasCashSerialNumber)
            {
                parts.Add("cash serial");
            }

            if (item.IsRechargeBundle)
            {
                parts.Add("recharge");
            }

            if (!string.IsNullOrWhiteSpace(item.Title))
            {
                parts.Add($"title {item.Title}");
            }

            return string.Join(" | ", parts);
        }

        private static string BuildDecodedItemMarker(StoreBankItemEntry item)
        {
            List<string> markers = new();
            markers.Add($"row{item.PacketGroupRowIndex.ToString(CultureInfo.InvariantCulture)}");
            markers.Add(item.SlotType switch
            {
                1 => "equip",
                2 => item.IsRechargeBundle ? "recharge" : "bundle",
                3 => "pet",
                _ => "item"
            });

            if (item.HasCashSerialNumber)
            {
                markers.Add("cash");
            }

            if (item.WasRetainedFromPreviousSnapshot)
            {
                markers.Add("retained");
            }

            return markers.Count > 0
                ? $" [{string.Join("/", markers)}]"
                : string.Empty;
        }

        private static string BuildDecodedItemSerialMarker(StoreBankItemEntry item)
        {
            if (item.ItemSerialNumber <= 0 && item.CashSerialNumber <= 0)
            {
                return string.Empty;
            }

            return item.CashSerialNumber > 0
                ? $" (itemSN {item.ItemSerialNumber.ToString(CultureInfo.InvariantCulture)}, cashSN {item.CashSerialNumber.ToString(CultureInfo.InvariantCulture)})"
                : $" (itemSN {item.ItemSerialNumber.ToString(CultureInfo.InvariantCulture)})";
        }

        private static string BuildDecodedItemClientObjectSuffix(StoreBankItemEntry item)
        {
            List<string> parts = new()
            {
                $"clientRow {item.ClientRowIndex.ToString(CultureInfo.InvariantCulture)}",
                $"stock {item.ClientStock.ToString(CultureInfo.InvariantCulture)}"
            };

            if (!string.IsNullOrWhiteSpace(item.ClientDisplayName)
                && !string.Equals(item.ClientDisplayName, item.ItemName, StringComparison.Ordinal))
            {
                parts.Add($"name '{item.ClientDisplayName}'");
            }

            if (item.EncodedByteLength > 0)
            {
                parts.Add($"decodeBytes {item.EncodedByteLength.ToString(CultureInfo.InvariantCulture)}");
            }

            return parts.Count > 0
                ? $" | {string.Join(", ", parts)}"
                : string.Empty;
        }

        private static string BuildDecodedItemBodySuffix(StoreBankItemEntry item)
        {
            List<string> parts = new();
            if (!string.IsNullOrWhiteSpace(item.Title))
            {
                parts.Add($"Title '{item.Title}'");
            }

            if (item.EquipData != null)
            {
                if (item.EquipData.NonCashSerialNumber.HasValue && item.EquipData.NonCashSerialNumber.Value > 0)
                {
                    parts.Add($"EquipSN {item.EquipData.NonCashSerialNumber.Value.ToString(CultureInfo.InvariantCulture)}");
                }

                string expiration = FormatFileTime(item.EquipData.ExpirationTime);
                if (!string.IsNullOrEmpty(expiration))
                {
                    parts.Add($"Expire {expiration}");
                }

                if (item.EquipData.IucOrExp > 0)
                {
                    parts.Add($"IUC/EXP {item.EquipData.IucOrExp.ToString(CultureInfo.InvariantCulture)}");
                }
            }

            if (item.BundleData != null && item.BundleData.RechargeableSerialNumber > 0)
            {
                parts.Add($"RechargeSN {item.BundleData.RechargeableSerialNumber.ToString(CultureInfo.InvariantCulture)}");
            }

            if (item.PetData != null)
            {
                string expiration = FormatFileTime(item.PetData.ExpirationTime);
                if (!string.IsNullOrEmpty(expiration))
                {
                    parts.Add($"Expire {expiration}");
                }
            }

            return parts.Count > 0
                ? $" | {string.Join(", ", parts)}"
                : string.Empty;
        }

        private static bool ShowsClientStock(StoreBankItemEntry item)
        {
            if (item == null || item.ClientStock <= 0)
            {
                return false;
            }

            return item.ItemId / 1000000 is 2 or 3 or 4
                || item.InventoryType == InventoryType.CASH;
        }

        private static bool IsCashItem(StoreBankItemEntry item)
        {
            return item != null && item.InventoryType == InventoryType.CASH;
        }

        private static string FormatFileTime(long value)
        {
            if (value <= 0)
            {
                return string.Empty;
            }

            try
            {
                return DateTime.FromFileTimeUtc(value).ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                return $"raw {value.ToString(CultureInfo.InvariantCulture)}";
            }
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

        private static bool TryReadStoreBankItem(BinaryReader reader, InventoryType expectedInventoryType, int packetGroupRowIndex, out StoreBankItemEntry entry)
        {
            entry = null;
            Stream stream = reader.BaseStream;
            long itemStartPosition = stream.Position;
            if (stream.Length - stream.Position < sizeof(byte) + sizeof(int) + sizeof(byte) + sizeof(long))
            {
                return false;
            }

            byte slotType = reader.ReadByte();
            if (slotType is not 1 and not 2 and not 3)
            {
                return false;
            }

            int itemId = reader.ReadInt32();
            bool hasCashSerialNumber = reader.ReadByte() != 0;
            long cashSerialNumber = 0;
            if (hasCashSerialNumber)
            {
                if (stream.Length - stream.Position < sizeof(long))
                {
                    return false;
                }

                cashSerialNumber = reader.ReadInt64();
            }

            if (stream.Length - stream.Position < sizeof(long))
            {
                return false;
            }

            long itemSerialNumber = reader.ReadInt64();

            int quantity = 1;
            string title = string.Empty;
            string metadataSummary = string.Empty;
            StoreBankEquipData equipData = null;
            StoreBankBundleData bundleData = null;
            StoreBankPetData petData = null;
            switch (slotType)
            {
                case 1:
                    if (!TryReadEquipBody(reader, hasCashSerialNumber, out title, out metadataSummary, out equipData))
                    {
                        return false;
                    }

                    break;

                case 2:
                    if (!TryReadBundleBody(reader, itemId, out quantity, out title, out metadataSummary, out bundleData))
                    {
                        return false;
                    }

                    break;

                case 3:
                    if (!TryReadPetBody(reader, out title, out metadataSummary, out petData))
                    {
                        return false;
                    }

                    break;
            }

            InventoryType resolvedInventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            if (resolvedInventoryType == InventoryType.NONE)
            {
                resolvedInventoryType = expectedInventoryType;
            }

            string itemName = InventoryItemMetadataResolver.TryResolveItemName(itemId, out string resolvedName)
                ? resolvedName
                : $"Item {itemId.ToString(CultureInfo.InvariantCulture)}";
            if (!string.IsNullOrWhiteSpace(title) && !string.Equals(title, itemName, StringComparison.OrdinalIgnoreCase))
            {
                itemName = $"{itemName} ({title})";
            }

            int clientStock = ResolveClientStock(slotType, quantity);
            int encodedByteLength = checked((int)(stream.Position - itemStartPosition));

            entry = new StoreBankItemEntry
            {
                ClientRowIndex = Math.Max(0, packetGroupRowIndex),
                ClientDisplayName = resolvedName ?? itemName,
                ClientStock = clientStock,
                EncodedByteLength = encodedByteLength,
                ItemId = itemId,
                ItemName = itemName,
                Title = title,
                InventoryType = resolvedInventoryType,
                PacketGroupInventoryType = expectedInventoryType,
                PacketGroupRowIndex = Math.Max(1, packetGroupRowIndex + 1),
                WasRetainedFromPreviousSnapshot = false,
                Quantity = clientStock,
                SlotType = slotType,
                HasCashSerialNumber = hasCashSerialNumber,
                ItemSerialNumber = itemSerialNumber,
                CashSerialNumber = cashSerialNumber,
                IsRechargeBundle = slotType == 2 && itemId / 10000 is 207 or 233,
                MetadataSummary = metadataSummary,
                EquipData = equipData,
                BundleData = bundleData,
                PetData = petData
            };
            return true;
        }

        internal static bool TryDecodeStoreBankItemForTest(byte[] payload, InventoryType expectedInventoryType, out StoreBankDecodedItemSnapshot snapshot, out string error)
        {
            snapshot = default;
            error = null;
            if (payload == null || payload.Length == 0)
            {
                error = "Store-bank item payload is empty.";
                return false;
            }

            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);
            if (!TryReadStoreBankItem(reader, expectedInventoryType, packetGroupRowIndex: 0, out StoreBankItemEntry entry))
            {
                error = "Store-bank item payload could not be decoded.";
                return false;
            }

            snapshot = new StoreBankDecodedItemSnapshot(
                entry.SlotType,
                entry.ItemId,
                entry.ClientStock,
                entry.Quantity,
                entry.InventoryType,
                entry.HasCashSerialNumber,
                entry.ItemSerialNumber,
                entry.CashSerialNumber,
                entry.ClientDisplayName,
                entry.Title,
                entry.MetadataSummary,
                entry.EncodedByteLength);
            return true;
        }

        private static StoreBankItemEntry CloneRetainedItem(StoreBankItemEntry item)
        {
            if (item == null)
            {
                return null;
            }

            return new StoreBankItemEntry
            {
                ClientRowIndex = item.ClientRowIndex,
                ClientDisplayName = item.ClientDisplayName,
                ClientStock = item.ClientStock,
                EncodedByteLength = item.EncodedByteLength,
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                Title = item.Title,
                InventoryType = item.InventoryType,
                PacketGroupInventoryType = item.PacketGroupInventoryType,
                PacketGroupRowIndex = item.PacketGroupRowIndex,
                WasRetainedFromPreviousSnapshot = true,
                Quantity = item.Quantity,
                SlotType = item.SlotType,
                HasCashSerialNumber = item.HasCashSerialNumber,
                ItemSerialNumber = item.ItemSerialNumber,
                CashSerialNumber = item.CashSerialNumber,
                IsRechargeBundle = item.IsRechargeBundle,
                MetadataSummary = item.MetadataSummary,
                EquipData = item.EquipData,
                BundleData = item.BundleData,
                PetData = item.PetData
            };
        }

        private static int ResolveClientStock(byte slotType, int quantity)
        {
            return slotType switch
            {
                2 => Math.Max(1, quantity),
                _ => 1
            };
        }

        private static bool TryReadEquipBody(BinaryReader reader, bool hasCashSerialNumber, out string title, out string metadataSummary, out StoreBankEquipData equipData)
        {
            title = string.Empty;
            metadataSummary = string.Empty;
            equipData = null;
            Stream stream = reader.BaseStream;
            const int equipStatsByteLength = (sizeof(byte) * 2) + (sizeof(short) * 15);
            if (stream.Length - stream.Position < equipStatsByteLength)
            {
                return false;
            }

            short remainingUpgradeCount = reader.ReadInt16();
            byte upgradeCount = reader.ReadByte();
            short strength = reader.ReadInt16();
            short dexterity = reader.ReadInt16();
            short intelligence = reader.ReadInt16();
            short luck = reader.ReadInt16();
            short hp = reader.ReadInt16();
            short mp = reader.ReadInt16();
            short weaponAttack = reader.ReadInt16();
            short magicAttack = reader.ReadInt16();
            short weaponDefense = reader.ReadInt16();
            short magicDefense = reader.ReadInt16();
            short accuracy = reader.ReadInt16();
            short avoidability = reader.ReadInt16();
            short hands = reader.ReadInt16();
            short speed = reader.ReadInt16();
            short jump = reader.ReadInt16();
            if (!TryReadMapleString(reader, out title))
            {
                return false;
            }

            const int tailLength = sizeof(short) + (sizeof(byte) * 2) + (sizeof(int) * 3) + (sizeof(byte) * 2) + (sizeof(short) * 5);
            if (stream.Length - stream.Position < tailLength + sizeof(long) + sizeof(int))
            {
                return false;
            }

            short attribute = reader.ReadInt16();
            byte levelUpType = reader.ReadByte();
            byte level = reader.ReadByte();
            int experience = reader.ReadInt32();
            int durability = reader.ReadInt32();
            int itemUpgradeCount = reader.ReadInt32();
            byte grade = reader.ReadByte();
            byte bonusUpgradeCount = reader.ReadByte();
            short option1 = reader.ReadInt16();
            short option2 = reader.ReadInt16();
            short option3 = reader.ReadInt16();
            short socket1 = reader.ReadInt16();
            short socket2 = reader.ReadInt16();
            long? nonCashSerialNumber = null;
            if (!hasCashSerialNumber)
            {
                if (stream.Length - stream.Position < sizeof(long))
                {
                    return false;
                }

                nonCashSerialNumber = reader.ReadInt64();
            }

            long expirationTime = reader.ReadInt64();
            int iucOrExp = reader.ReadInt32();
            metadataSummary = BuildEquipMetadataSummary(
                remainingUpgradeCount,
                upgradeCount,
                strength,
                dexterity,
                intelligence,
                luck,
                hp,
                mp,
                weaponAttack,
                magicAttack,
                weaponDefense,
                magicDefense,
                accuracy,
                avoidability,
                hands,
                speed,
                jump,
                attribute,
                levelUpType,
                level,
                experience,
                durability,
                itemUpgradeCount,
                grade,
                bonusUpgradeCount,
                option1,
                option2,
                option3,
                socket1,
                socket2);
            equipData = new StoreBankEquipData
            {
                RemainingUpgradeCount = remainingUpgradeCount,
                UpgradeCount = upgradeCount,
                Strength = strength,
                Dexterity = dexterity,
                Intelligence = intelligence,
                Luck = luck,
                Hp = hp,
                Mp = mp,
                WeaponAttack = weaponAttack,
                MagicAttack = magicAttack,
                WeaponDefense = weaponDefense,
                MagicDefense = magicDefense,
                Accuracy = accuracy,
                Avoidability = avoidability,
                Hands = hands,
                Speed = speed,
                Jump = jump,
                Attribute = attribute,
                LevelUpType = levelUpType,
                Level = level,
                Experience = experience,
                Durability = durability,
                ItemUpgradeCount = itemUpgradeCount,
                Grade = grade,
                BonusUpgradeCount = bonusUpgradeCount,
                Option1 = option1,
                Option2 = option2,
                Option3 = option3,
                Socket1 = socket1,
                Socket2 = socket2,
                NonCashSerialNumber = nonCashSerialNumber,
                ExpirationTime = expirationTime,
                IucOrExp = iucOrExp
            };
            return true;
        }

        private static bool TryReadBundleBody(BinaryReader reader, int itemId, out int quantity, out string title, out string metadataSummary, out StoreBankBundleData bundleData)
        {
            quantity = 1;
            title = string.Empty;
            metadataSummary = string.Empty;
            bundleData = null;
            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < sizeof(ushort))
            {
                return false;
            }

            quantity = Math.Max(1, (int)reader.ReadUInt16());
            if (!TryReadMapleString(reader, out title))
            {
                return false;
            }

            if (stream.Length - stream.Position < sizeof(short))
            {
                return false;
            }

            short attribute = reader.ReadInt16();
            if (itemId / 10000 is 207 or 233)
            {
                if (stream.Length - stream.Position < sizeof(long))
                {
                    return false;
                }

                long rechargeableSerialNumber = reader.ReadInt64();
                metadataSummary = BuildBundleMetadataSummary(attribute, rechargeableSerialNumber);
                bundleData = new StoreBankBundleData
                {
                    Attribute = attribute,
                    RechargeableSerialNumber = rechargeableSerialNumber
                };
                return true;
            }

            metadataSummary = BuildBundleMetadataSummary(attribute, rechargeableSerialNumber: 0);
            bundleData = new StoreBankBundleData
            {
                Attribute = attribute,
                RechargeableSerialNumber = 0
            };
            return true;
        }

        private static bool TryReadPetBody(BinaryReader reader, out string title, out string metadataSummary, out StoreBankPetData petData)
        {
            title = string.Empty;
            metadataSummary = string.Empty;
            petData = null;
            Stream stream = reader.BaseStream;
            const int petNameLength = 13;
            const int petTailLength = sizeof(byte) + sizeof(short) + sizeof(byte) + sizeof(long) + sizeof(short) + sizeof(ushort) + sizeof(int) + sizeof(short);
            if (stream.Length - stream.Position < petNameLength + petTailLength)
            {
                return false;
            }

            byte[] petNameBytes = reader.ReadBytes(petNameLength);
            title = Encoding.ASCII.GetString(petNameBytes).TrimEnd('\0', ' ');
            byte level = reader.ReadByte();
            short closeness = reader.ReadInt16();
            byte fullness = reader.ReadByte();
            long expirationTime = reader.ReadInt64();
            short attribute = reader.ReadInt16();
            ushort skill = reader.ReadUInt16();
            int remainingLife = reader.ReadInt32();
            short fatigue = reader.ReadInt16();
            metadataSummary = BuildPetMetadataSummary(level, closeness, fullness, attribute, skill, remainingLife, fatigue);
            petData = new StoreBankPetData
            {
                Level = level,
                Closeness = closeness,
                Fullness = fullness,
                ExpirationTime = expirationTime,
                Attribute = attribute,
                Skill = skill,
                RemainingLife = remainingLife,
                Fatigue = fatigue
            };
            return true;
        }

        private static string BuildEquipMetadataSummary(
            short remainingUpgradeCount,
            byte upgradeCount,
            short strength,
            short dexterity,
            short intelligence,
            short luck,
            short hp,
            short mp,
            short weaponAttack,
            short magicAttack,
            short weaponDefense,
            short magicDefense,
            short accuracy,
            short avoidability,
            short hands,
            short speed,
            short jump,
            short attribute,
            byte levelUpType,
            byte level,
            int experience,
            int durability,
            int itemUpgradeCount,
            byte grade,
            byte bonusUpgradeCount,
            short option1,
            short option2,
            short option3,
            short socket1,
            short socket2)
        {
            List<string> parts = new()
            {
                $"RUC {remainingUpgradeCount}"
            };
            if (upgradeCount > 0)
            {
                parts.Add($"CUC {upgradeCount}");
            }

            AppendMetadataPart(parts, "STR", strength);
            AppendMetadataPart(parts, "DEX", dexterity);
            AppendMetadataPart(parts, "INT", intelligence);
            AppendMetadataPart(parts, "LUK", luck);
            AppendMetadataPart(parts, "HP", hp);
            AppendMetadataPart(parts, "MP", mp);
            AppendMetadataPart(parts, "PAD", weaponAttack);
            AppendMetadataPart(parts, "MAD", magicAttack);
            AppendMetadataPart(parts, "PDD", weaponDefense);
            AppendMetadataPart(parts, "MDD", magicDefense);
            AppendMetadataPart(parts, "ACC", accuracy);
            AppendMetadataPart(parts, "EVA", avoidability);
            AppendMetadataPart(parts, "Hands", hands);
            AppendMetadataPart(parts, "Speed", speed);
            AppendMetadataPart(parts, "Jump", jump);

            if (attribute != 0)
            {
                parts.Add($"Attr 0x{(ushort)attribute:X4}");
            }

            if (levelUpType > 0)
            {
                parts.Add($"LvType {levelUpType}");
            }

            if (level > 0)
            {
                parts.Add($"Lv {level}");
            }

            if (experience > 0)
            {
                parts.Add($"EXP {experience}");
            }

            if (durability > 0)
            {
                parts.Add($"Dur {durability}");
            }

            if (itemUpgradeCount > 0)
            {
                parts.Add($"IUC {itemUpgradeCount}");
            }

            if (grade > 0)
            {
                parts.Add($"Grade {grade}");
            }

            if (bonusUpgradeCount > 0)
            {
                parts.Add($"CHUC {bonusUpgradeCount}");
            }

            if (option1 != 0 || option2 != 0 || option3 != 0)
            {
                parts.Add($"Opt {option1}/{option2}/{option3}");
            }

            if (socket1 != 0 || socket2 != 0)
            {
                parts.Add($"Socket {socket1}/{socket2}");
            }

            return string.Join(", ", parts);
        }

        private static string BuildBundleMetadataSummary(short attribute, long rechargeableSerialNumber)
        {
            List<string> parts = new();
            if (attribute != 0)
            {
                parts.Add($"Attr 0x{(ushort)attribute:X4}");
            }

            if (rechargeableSerialNumber > 0)
            {
                parts.Add($"RechargeSN {rechargeableSerialNumber}");
            }

            return string.Join(", ", parts);
        }

        private static string BuildPetMetadataSummary(
            byte level,
            short closeness,
            byte fullness,
            short attribute,
            ushort skill,
            int remainingLife,
            short fatigue)
        {
            List<string> parts = new()
            {
                $"PetLv {level}",
                $"Closeness {closeness}",
                $"Fullness {fullness}"
            };
            if (attribute != 0)
            {
                parts.Add($"Attr 0x{(ushort)attribute:X4}");
            }

            if (skill > 0)
            {
                parts.Add($"Skill 0x{skill:X4}");
            }

            if (remainingLife > 0)
            {
                parts.Add($"Life {remainingLife}");
            }

            if (fatigue > 0)
            {
                parts.Add($"Fatigue {fatigue}");
            }

            return string.Join(", ", parts);
        }

        private static void AppendMetadataPart(List<string> parts, string label, short value)
        {
            if (value != 0)
            {
                parts.Add($"{label} {value:+#;-#;0}");
            }
        }

        private static bool TryReadMapleString(BinaryReader reader, out string value)
        {
            value = string.Empty;
            Stream stream = reader.BaseStream;
            if (stream.Length - stream.Position < sizeof(short))
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

    internal sealed class PacketOwnedBattleRecordRuntime
    {
        private readonly List<string> _recentNotes = new();
        private int _packetCount420;
        private int _packetCount421;
        private int _packetCount422;
        private int _packetCount423;
        private int _lastPacketType = -1;
        private int _pageIndex;

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
                    StatusMessage = "CBattleRecordMan::OnPacket ignored packet 420 because the v95 decompile only dispatches 421 and 422.";
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
                        StatusMessage = "CBattleRecordMan accepted the server-on-calc request result. The client decompile only flips m_bServerOnCalc here; it does not open the window or arm m_bOnCalc in packet 422.";
                    }
                    break;

                case 423:
                    _packetCount423++;
                    StatusMessage = "CBattleRecordMan::OnPacket ignored packet 423 because the v95 decompile only dispatches 421 and 422.";
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
                $"Calc flags: onCalc={OnCalc}, serverOnCalc={ServerOnCalc}, dot={DotTrackingEnabled}, ready421={OnCalc && ServerOnCalc}",
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
            if (!OnCalc || !ServerOnCalc)
            {
                StatusMessage = "CBattleRecordMan ignored DOT damage info because m_bOnCalc and m_bServerOnCalc were not both armed yet.";
                message = StatusMessage;
                return true;
            }

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
