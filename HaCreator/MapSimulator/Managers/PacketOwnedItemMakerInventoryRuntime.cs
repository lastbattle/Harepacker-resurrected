using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Managers
{
    internal readonly record struct PacketOwnedItemMakerMaterialCost(InventoryType InventoryType, int ItemId, int Quantity);

    internal sealed class PacketOwnedItemMakerPendingRequest
    {
        public bool IsDisassembly { get; init; }
        public string RecipeKey { get; init; } = string.Empty;
        public bool IsHiddenRecipe { get; init; }
        public int ExpectedRewardBucketKey { get; init; } = -1;
        public int RecipeOutputItemId { get; init; }
        public int SourceSlotIndex { get; init; } = -1;
        public int SourceItemId { get; init; }
        public int ExpectedRewardItemId { get; init; }
        public int ExpectedRewardQuantity { get; init; }
        public int MesoCost { get; init; }
        public int CatalystItemId { get; init; }
        public IReadOnlyList<PacketOwnedItemMakerMaterialCost> Materials { get; init; }
            = Array.Empty<PacketOwnedItemMakerMaterialCost>();
    }

    internal readonly record struct PacketOwnedItemMakerInventoryReconciliationResult(
        bool Applied,
        bool ClearedSourceSlot,
        int FailedConsumptionCount,
        int FailedGrantCount,
        int GrantedEntryCount);

    internal static class PacketOwnedItemMakerInventoryRuntime
    {
        public static PacketOwnedItemMakerInventoryReconciliationResult Apply(
            IInventoryRuntime inventory,
            PacketOwnedItemMakerPendingRequest pendingRequest,
            PacketOwnedItemMakerResult packetResult)
        {
            if (inventory == null || pendingRequest == null || packetResult == null || packetResult.ResultCode > 1)
            {
                return default;
            }

            int failedConsumptionCount = 0;
            int failedGrantCount = 0;
            int grantedEntryCount = 0;
            bool clearedSourceSlot = false;

            if (pendingRequest.IsDisassembly)
            {
                bool shouldConsumeMountedSourceSlot = packetResult.ResultType != 3;
                if (shouldConsumeMountedSourceSlot
                    && pendingRequest.SourceSlotIndex >= 0
                    && pendingRequest.SourceItemId > 0)
                {
                    clearedSourceSlot = inventory.TryConsumeItemAtSlot(
                        InventoryType.EQUIP,
                        pendingRequest.SourceSlotIndex,
                        pendingRequest.SourceItemId,
                        quantity: 1);
                    if (!clearedSourceSlot)
                    {
                        failedConsumptionCount++;
                    }
                }
            }
            else
            {
                foreach (PacketOwnedItemMakerMaterialCost material in pendingRequest.Materials ?? Array.Empty<PacketOwnedItemMakerMaterialCost>())
                {
                    if (material.ItemId <= 0 || material.Quantity <= 0)
                    {
                        continue;
                    }

                    if (!inventory.TryConsumeItem(material.InventoryType, material.ItemId, material.Quantity))
                    {
                        failedConsumptionCount++;
                    }
                }

                if (pendingRequest.CatalystItemId > 0)
                {
                    InventoryType catalystType = ResolveInventoryType(pendingRequest.CatalystItemId);
                    if (catalystType != InventoryType.NONE
                        && !inventory.TryConsumeItem(catalystType, pendingRequest.CatalystItemId, quantity: 1))
                    {
                        failedConsumptionCount++;
                    }
                }
            }

            int mesoDelta = packetResult.MesoDelta != 0 ? packetResult.MesoDelta : pendingRequest.MesoCost;
            if (mesoDelta > 0)
            {
                if (!inventory.TryConsumeMeso(mesoDelta))
                {
                    failedConsumptionCount++;
                }
            }
            else if (mesoDelta < 0)
            {
                inventory.AddMeso(-mesoDelta);
            }

            foreach ((int itemId, int quantity) in EnumerateGrantedItems(packetResult, pendingRequest))
            {
                if (itemId <= 0 || quantity <= 0)
                {
                    continue;
                }

                InventoryType inventoryType = ResolveInventoryType(itemId);
                if (inventoryType == InventoryType.NONE || !inventory.CanAcceptItem(inventoryType, itemId, quantity))
                {
                    failedGrantCount++;
                    continue;
                }

                inventory.AddItem(inventoryType, itemId, texture: null, quantity);
                grantedEntryCount++;
            }

            return new PacketOwnedItemMakerInventoryReconciliationResult(
                Applied: true,
                ClearedSourceSlot: clearedSourceSlot,
                FailedConsumptionCount: failedConsumptionCount,
                FailedGrantCount: failedGrantCount,
                GrantedEntryCount: grantedEntryCount);
        }

        internal static IReadOnlyList<(int ItemId, int Quantity)> EnumerateGrantedItems(PacketOwnedItemMakerResult packetResult)
        {
            return EnumerateGrantedItems(packetResult, pendingRequest: null);
        }

        internal static IReadOnlyList<(int ItemId, int Quantity)> EnumerateGrantedItems(
            PacketOwnedItemMakerResult packetResult,
            PacketOwnedItemMakerPendingRequest pendingRequest)
        {
            List<(int ItemId, int Quantity)> items = new();
            if (packetResult == null || packetResult.ResultCode > 1)
            {
                return items;
            }

            switch (packetResult.ResultType)
            {
                case 1:
                case 2:
                    if (TryResolvePrimaryGrantedItem(packetResult, pendingRequest, out int primaryItemId, out int primaryQuantity))
                    {
                        items.Add((primaryItemId, primaryQuantity));
                    }

                    AppendRewardItems(items, packetResult.RewardItems);
                    AppendBonusItems(items, packetResult.BonusItemIds);
                    if (packetResult.HasAuxiliaryItem && packetResult.AuxiliaryItemId > 0)
                    {
                        items.Add((packetResult.AuxiliaryItemId, 1));
                    }
                    break;

                case 3:
                    if (packetResult.TargetItemId > 0)
                    {
                        items.Add((packetResult.TargetItemId, Math.Max(1, packetResult.TargetItemCount)));
                    }

                    if (packetResult.GeneratedItemId > 0)
                    {
                        items.Add((packetResult.GeneratedItemId, Math.Max(1, packetResult.GeneratedItemCount)));
                    }
                    break;

                case 4:
                    AppendRewardItems(items, packetResult.RewardItems);
                    break;
            }

            return items;
        }

        internal static bool TryResolvePrimaryGrantedItem(
            PacketOwnedItemMakerResult packetResult,
            PacketOwnedItemMakerPendingRequest pendingRequest,
            out int itemId,
            out int quantity)
        {
            itemId = 0;
            quantity = 0;
            if (packetResult == null || packetResult.ResultCode > 1)
            {
                return false;
            }

            switch (packetResult.ResultType)
            {
                case 1:
                case 2:
                    if (!packetResult.SuppressedPrimaryTargetNotice && packetResult.TargetItemId > 0)
                    {
                        itemId = packetResult.TargetItemId;
                        quantity = Math.Max(1, packetResult.TargetItemCount);
                        return true;
                    }

                    if (pendingRequest != null
                        && !pendingRequest.IsDisassembly
                        && packetResult.SuppressedPrimaryTargetNotice
                        && pendingRequest.ExpectedRewardItemId > 0)
                    {
                        itemId = pendingRequest.ExpectedRewardItemId;
                        quantity = Math.Max(1, pendingRequest.ExpectedRewardQuantity);
                        return true;
                    }

                    return false;

                case 3:
                    if (packetResult.TargetItemId > 0)
                    {
                        itemId = packetResult.TargetItemId;
                        quantity = Math.Max(1, packetResult.TargetItemCount);
                        return true;
                    }

                    return false;

                default:
                    return false;
            }
        }

        private static void AppendRewardItems(List<(int ItemId, int Quantity)> items, IReadOnlyList<PacketOwnedItemMakerResultItemEntry> rewardItems)
        {
            if (rewardItems == null)
            {
                return;
            }

            for (int i = 0; i < rewardItems.Count; i++)
            {
                PacketOwnedItemMakerResultItemEntry rewardItem = rewardItems[i];
                if (rewardItem.ItemId > 0)
                {
                    items.Add((rewardItem.ItemId, Math.Max(1, rewardItem.Quantity)));
                }
            }
        }

        private static void AppendBonusItems(List<(int ItemId, int Quantity)> items, IReadOnlyList<int> bonusItemIds)
        {
            if (bonusItemIds == null)
            {
                return;
            }

            for (int i = 0; i < bonusItemIds.Count; i++)
            {
                int itemId = bonusItemIds[i];
                if (itemId > 0)
                {
                    items.Add((itemId, 1));
                }
            }
        }

        internal static InventoryType ResolveInventoryType(int itemId)
        {
            int typeBucket = itemId / 1000000;
            return typeBucket switch
            {
                1 => InventoryType.EQUIP,
                2 => InventoryType.USE,
                3 => InventoryType.SETUP,
                4 => InventoryType.ETC,
                5 => InventoryType.CASH,
                _ => InventoryType.NONE
            };
        }
    }
}
