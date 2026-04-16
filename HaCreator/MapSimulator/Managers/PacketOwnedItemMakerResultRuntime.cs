using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    internal readonly record struct PacketOwnedItemMakerResultItemEntry(int ItemId, int Quantity);

    internal sealed class PacketOwnedItemMakerResult
    {
        public int ResultCode { get; init; }
        public int ResultType { get; init; }
        public int TargetItemId { get; init; }
        public int TargetItemCount { get; init; }
        public int DisassembledItemId { get; init; }
        public int GeneratedItemId { get; init; }
        public int GeneratedItemCount { get; init; } = 100;
        public bool SuppressedPrimaryTargetNotice { get; init; }
        public bool HasAuxiliaryItem { get; init; }
        public int AuxiliaryItemId { get; init; }
        public int MesoDelta { get; init; }
        public bool ResetItemSlot { get; init; }
        public IReadOnlyList<PacketOwnedItemMakerResultItemEntry> RewardItems { get; init; } = Array.Empty<PacketOwnedItemMakerResultItemEntry>();
        public IReadOnlyList<int> BonusItemIds { get; init; } = Array.Empty<int>();

        public bool RepresentsDisassemblyResult =>
            ResultCode <= 1
            && ResultType is 3 or 4;

        public bool RepresentsSuccessfulCraft =>
            ResultCode == 0
            && ResultType is 1 or 2
            && TargetItemId > 0
            && TargetItemCount > 0;
    }

    internal static class PacketOwnedItemMakerResultRuntime
    {
        public const int ClientPacketType = 248;
        private const int ItemMakerPrimaryGainStringPoolId = 5442;
        private const int ItemMakerSecondaryGainStringPoolId = 306;
        private const int LostMesosStringPoolId = 305;
        private const int DisassemblySuccessStringPoolId = 307;
        private const int ItemMakerDisassemblyNoticeStringPoolId = 768;
        private const int ItemMakerResultNoticeStringPoolId = 770;
        private const int ItemMakerIncorrectRequestStringPoolId = 766;
        private const int ItemMakerNoEmptySlotDisassemblyStringPoolId = 769;
        private const int ItemMakerNoEmptySlotSuffixStringPoolId = 754;
        private const int EquipInventoryStringPoolId = 10;
        private const int SetupInventoryStringPoolId = 11;
        private const int EtcInventoryStringPoolId = 6712;
        private const int UseInventoryStringPoolId = 6791;

        public static bool TryDecode(byte[] payload, out PacketOwnedItemMakerResult result, out string error)
        {
            result = null;
            error = null;

            if (payload == null || payload.Length < sizeof(int))
            {
                error = "Maker-result payload must include at least the Int32 result code.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);

                int resultCode = reader.ReadInt32();
                int resultType = 0;
                int targetItemId = 0;
                int targetItemCount = 0;
                int disassembledItemId = 0;
                int generatedItemId = 0;
                int generatedItemCount = 100;
                bool suppressedPrimaryTargetNotice = false;
                bool hasAuxiliaryItem = false;
                int auxiliaryItemId = 0;
                int mesoDelta = 0;
                bool resetItemSlot = false;
                List<PacketOwnedItemMakerResultItemEntry> rewardItems = null;
                List<int> bonusItemIds = null;

                if (resultCode <= 1)
                {
                    EnsureReadable(reader, sizeof(int), "Maker-result payload is missing its Int32 result subtype.");
                    resultType = reader.ReadInt32();

                    switch (resultType)
                    {
                        case 1:
                        case 2:
                            EnsureReadable(reader, sizeof(byte), "Maker-result subtype 1/2 is missing the target-item suppression flag.");
                            suppressedPrimaryTargetNotice = reader.ReadByte() != 0;
                            if (!suppressedPrimaryTargetNotice)
                            {
                                EnsureReadable(reader, sizeof(int) * 2, "Maker-result subtype 1/2 is missing the crafted target item payload.");
                                targetItemId = reader.ReadInt32();
                                targetItemCount = Math.Max(1, reader.ReadInt32());
                            }

                            int rewardCount = ReadNonNegativeCount(reader, "Maker-result subtype 1/2 reward item count is missing or negative.");
                            if (rewardCount > 0)
                            {
                                rewardItems = new List<PacketOwnedItemMakerResultItemEntry>(rewardCount);
                                for (int i = 0; i < rewardCount; i++)
                                {
                                    EnsureReadable(reader, sizeof(int) * 2, "Maker-result subtype 1/2 reward item entry is truncated.");
                                    rewardItems.Add(new PacketOwnedItemMakerResultItemEntry(
                                        reader.ReadInt32(),
                                        Math.Max(1, reader.ReadInt32())));
                                }
                            }

                            int bonusCount = ReadNonNegativeCount(reader, "Maker-result subtype 1/2 bonus item count is missing or negative.");
                            if (bonusCount > 0)
                            {
                                bonusItemIds = new List<int>(bonusCount);
                                for (int i = 0; i < bonusCount; i++)
                                {
                                    EnsureReadable(reader, sizeof(int), "Maker-result subtype 1/2 bonus item entry is truncated.");
                                    bonusItemIds.Add(reader.ReadInt32());
                                }
                            }

                            EnsureReadable(reader, sizeof(byte), "Maker-result subtype 1/2 is missing the auxiliary-item flag.");
                            hasAuxiliaryItem = reader.ReadByte() != 0;
                            if (hasAuxiliaryItem)
                            {
                                EnsureReadable(reader, sizeof(int), "Maker-result subtype 1/2 auxiliary item id is truncated.");
                                auxiliaryItemId = reader.ReadInt32();
                            }

                            EnsureReadable(reader, sizeof(int), "Maker-result subtype 1/2 meso delta is truncated.");
                            mesoDelta = reader.ReadInt32();
                            break;

                        case 3:
                            EnsureReadable(reader, sizeof(int) * 2, "Maker-result subtype 3 is missing the disassembly target and generated item ids.");
                            targetItemId = reader.ReadInt32();
                            targetItemCount = 1;
                            generatedItemId = reader.ReadInt32();
                            break;

                        case 4:
                            EnsureReadable(reader, sizeof(int), "Maker-result subtype 4 is missing the disassembled item id.");
                            disassembledItemId = reader.ReadInt32();

                            int disassemblyRewardCount = ReadNonNegativeCount(reader, "Maker-result subtype 4 reward item count is missing or negative.");
                            if (disassemblyRewardCount > 0)
                            {
                                rewardItems = new List<PacketOwnedItemMakerResultItemEntry>(disassemblyRewardCount);
                                for (int i = 0; i < disassemblyRewardCount; i++)
                                {
                                    EnsureReadable(reader, sizeof(int) * 2, "Maker-result subtype 4 reward item entry is truncated.");
                                    rewardItems.Add(new PacketOwnedItemMakerResultItemEntry(
                                        reader.ReadInt32(),
                                        Math.Max(1, reader.ReadInt32())));
                                }
                            }

                            EnsureReadable(reader, sizeof(int), "Maker-result subtype 4 meso delta is truncated.");
                            mesoDelta = reader.ReadInt32();
                            resetItemSlot = true;
                            break;
                    }
                }

                if (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    error = $"Maker-result payload left {reader.BaseStream.Length - reader.BaseStream.Position} unread byte(s).";
                    return false;
                }

                result = new PacketOwnedItemMakerResult
                {
                    ResultCode = resultCode,
                    ResultType = resultType,
                    TargetItemId = targetItemId,
                    TargetItemCount = targetItemCount,
                    DisassembledItemId = disassembledItemId,
                    GeneratedItemId = generatedItemId,
                    GeneratedItemCount = generatedItemCount,
                    SuppressedPrimaryTargetNotice = suppressedPrimaryTargetNotice,
                    HasAuxiliaryItem = hasAuxiliaryItem,
                    AuxiliaryItemId = auxiliaryItemId,
                    MesoDelta = mesoDelta,
                    ResetItemSlot = resetItemSlot,
                    RewardItems = rewardItems ?? (IReadOnlyList<PacketOwnedItemMakerResultItemEntry>)Array.Empty<PacketOwnedItemMakerResultItemEntry>(),
                    BonusItemIds = bonusItemIds ?? (IReadOnlyList<int>)Array.Empty<int>()
                };
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException or InvalidDataException)
            {
                error = $"Maker-result payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        public static string BuildStatusMessage(
            PacketOwnedItemMakerResult result,
            PacketOwnedItemMakerPendingRequest pendingRequest = null,
            bool disassemblyMode = false)
        {
            if (result == null)
            {
                return "Item Maker result is unavailable.";
            }

            if (result.ResultCode == 0
                && result.ResultType is 1 or 2
                && PacketOwnedItemMakerInventoryRuntime.TryResolvePrimaryGrantedItem(
                    result,
                    pendingRequest,
                    out int craftedItemId,
                    out int craftedQuantity))
            {
                return $"Created {ResolveItemName(craftedItemId)} x{craftedQuantity}.";
            }

            if (result.ResultType == 3)
            {
                return FormatDisassemblyYieldSummary(result);
            }

            if (result.DisassembledItemId > 0)
            {
                return FormatDisassemblyNotice(result.DisassembledItemId);
            }

            return result.ResultCode switch
            {
                1 => MapleStoryStringPool.GetOrFallback(ItemMakerResultNoticeStringPoolId, "This item cannot be disassembled."),
                2 => MapleStoryStringPool.GetOrFallback(ItemMakerIncorrectRequestStringPoolId, "You have made an incorrect request."),
                3 => BuildNoEmptySlotNotice(InventoryType.SETUP, disassemblyMode),
                4 => BuildNoEmptySlotNotice(InventoryType.ETC, disassemblyMode),
                _ => $"Packet-owned maker result code {result.ResultCode} subtype {result.ResultType}."
            };
        }

        internal static string BuildNoEmptySlotNotice(InventoryType inventoryType, bool disassemblyMode = false)
        {
            if (disassemblyMode)
            {
                return MapleStoryStringPool.GetOrFallback(
                    ItemMakerNoEmptySlotDisassemblyStringPoolId,
                    "Please make some room in your Etc inventory.");
            }

            string inventoryLabel = ResolveInventoryLabel(inventoryType);
            string suffix = MapleStoryStringPool.GetOrFallback(
                ItemMakerNoEmptySlotSuffixStringPoolId,
                "inventory has no empty slot.");
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}", inventoryLabel, suffix).Trim();
        }

        public static IReadOnlyList<string> BuildFeedbackLines(PacketOwnedItemMakerResult result)
        {
            if (result == null)
            {
                return Array.Empty<string>();
            }

            List<string> lines = new();
            if (result.ResultCode > 1)
            {
                return lines;
            }

            switch (result.ResultType)
            {
                case 1:
                case 2:
                    if (!result.SuppressedPrimaryTargetNotice)
                    {
                        AppendPrimaryTargetGainLine(lines, result.TargetItemId, result.TargetItemCount);
                    }

                    AppendSecondaryRewardLines(lines, result.RewardItems);
                    AppendSecondaryBonusLines(lines, result.BonusItemIds);
                    if (result.HasAuxiliaryItem)
                    {
                        AppendSecondaryGainLine(lines, result.AuxiliaryItemId, 1);
                    }

                    AppendMesoLossLine(lines, result.MesoDelta);
                    break;

                case 3:
                    if (result.TargetItemId > 0)
                    {
                        AppendPrimaryTargetGainLine(lines, result.TargetItemId, 1);
                    }

                    AppendSecondaryGainLine(lines, result.GeneratedItemId, result.GeneratedItemCount);
                    break;

                case 4:
                    if (result.DisassembledItemId > 0)
                    {
                        lines.Add(FormatDisassemblySuccess(result.DisassembledItemId));
                    }

                    AppendPrimaryRewardLines(lines, result.RewardItems);
                    AppendMesoLossLine(lines, result.MesoDelta);
                    break;
            }

            return lines;
        }

        private static void EnsureReadable(BinaryReader reader, int requiredBytes, string message)
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < requiredBytes)
            {
                throw new InvalidDataException(message);
            }
        }

        private static int ReadNonNegativeCount(BinaryReader reader, string message)
        {
            EnsureReadable(reader, sizeof(int), message);
            int count = reader.ReadInt32();
            if (count < 0)
            {
                throw new InvalidDataException(message);
            }

            return count;
        }

        private static void AppendPrimaryRewardLines(List<string> lines, IReadOnlyList<PacketOwnedItemMakerResultItemEntry> items)
        {
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                PacketOwnedItemMakerResultItemEntry item = items[i];
                AppendPrimaryTargetGainLine(lines, item.ItemId, item.Quantity);
            }
        }

        private static void AppendSecondaryRewardLines(List<string> lines, IReadOnlyList<PacketOwnedItemMakerResultItemEntry> items)
        {
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                PacketOwnedItemMakerResultItemEntry item = items[i];
                AppendSecondaryGainLine(lines, item.ItemId, item.Quantity);
            }
        }

        private static void AppendSecondaryBonusLines(List<string> lines, IReadOnlyList<int> bonusItemIds)
        {
            if (bonusItemIds == null)
            {
                return;
            }

            for (int i = 0; i < bonusItemIds.Count; i++)
            {
                AppendSecondaryGainLine(lines, bonusItemIds[i], 1);
            }
        }

        private static void AppendPrimaryTargetGainLine(List<string> lines, int itemId, int quantity)
        {
            if (lines == null || itemId <= 0)
            {
                return;
            }

            int clampedQuantity = Math.Max(1, quantity);
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                ItemMakerPrimaryGainStringPoolId,
                "You have gained %1 x%3 (%2).",
                maxPlaceholderCount: 3,
                out _);
            string itemTypeName = ResolveItemTypeName(itemId);
            string itemName = ResolveItemName(itemId);
            lines.Add(string.Format(CultureInfo.InvariantCulture, format, itemTypeName, itemName, clampedQuantity));
        }

        private static void AppendSecondaryGainLine(List<string> lines, int itemId, int quantity)
        {
            if (lines == null || itemId <= 0)
            {
                return;
            }

            int clampedQuantity = Math.Max(1, quantity);
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                ItemMakerSecondaryGainStringPoolId,
                "You have gained {0} x{1}.",
                maxPlaceholderCount: 2,
                out _);
            lines.Add(string.Format(CultureInfo.InvariantCulture, format, ResolveItemName(itemId), clampedQuantity));
        }

        private static void AppendMesoLossLine(List<string> lines, int mesoDelta)
        {
            if (lines == null || mesoDelta <= 0)
            {
                return;
            }

            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                LostMesosStringPoolId,
                "You have lost mesos. ({0})",
                maxPlaceholderCount: 1,
                out _);
            lines.Add(string.Format(CultureInfo.InvariantCulture, format, -mesoDelta));
        }

        private static string FormatDisassemblySuccess(int itemId)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                DisassemblySuccessStringPoolId,
                "You have successfully disassembled the {0}.",
                maxPlaceholderCount: 1,
                out _);
            return string.Format(CultureInfo.InvariantCulture, format, ResolveItemName(itemId));
        }

        private static string FormatDisassemblyNotice(int itemId)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                ItemMakerDisassemblyNoticeStringPoolId,
                "You have successfully disassembled the {0}.",
                maxPlaceholderCount: 1,
                out _);
            return string.Format(CultureInfo.InvariantCulture, format, ResolveItemName(itemId));
        }

        private static string FormatDisassemblyYieldSummary(PacketOwnedItemMakerResult result)
        {
            List<string> rewardParts = new();
            if (result.TargetItemId > 0)
            {
                rewardParts.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} x{1}",
                    ResolveItemName(result.TargetItemId),
                    Math.Max(1, result.TargetItemCount)));
            }

            if (result.GeneratedItemId > 0)
            {
                rewardParts.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} x{1}",
                    ResolveItemName(result.GeneratedItemId),
                    Math.Max(1, result.GeneratedItemCount)));
            }

            if (rewardParts.Count == 0)
            {
                return "Disassembly completed.";
            }

            return $"Disassembly yielded {string.Join(" and ", rewardParts)}.";
        }

        private static string ResolveInventoryLabel(InventoryType inventoryType)
        {
            return inventoryType switch
            {
                InventoryType.EQUIP => MapleStoryStringPool.GetOrFallback(EquipInventoryStringPoolId, "Equip"),
                InventoryType.USE => MapleStoryStringPool.GetOrFallback(UseInventoryStringPoolId, "Use"),
                InventoryType.SETUP => MapleStoryStringPool.GetOrFallback(SetupInventoryStringPoolId, "Setup"),
                InventoryType.ETC => MapleStoryStringPool.GetOrFallback(EtcInventoryStringPoolId, "Etc"),
                _ => "Inventory"
            };
        }

        private static string ResolveItemName(int itemId)
        {
            return HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                && !string.IsNullOrWhiteSpace(itemInfo?.Item2)
                ? itemInfo.Item2
                : $"Item #{itemId}";
        }

        private static string ResolveItemTypeName(int itemId)
        {
            return HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                && !string.IsNullOrWhiteSpace(itemInfo?.Item1)
                ? itemInfo.Item1.Trim()
                : "Item";
        }
    }
}
