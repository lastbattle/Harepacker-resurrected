using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal readonly record struct PacketOwnedCommodityMetadataCandidate(
        int SerialNumber,
        int ItemId,
        long Price,
        bool OnSale,
        int Count,
        int PeriodDays,
        int Priority);

    internal static class AdminShopPacketOwnedSellTemplateParity
    {
        internal static bool IsClientSetUserItemsSlotEligible(
            bool isCashItem,
            bool isNotForSale,
            bool isQuestItem,
            bool isCashOwnershipLocked,
            long? cashItemSerialNumber)
        {
            return !isCashItem
                && !isNotForSale
                && !isQuestItem
                && !isCashOwnershipLocked
                && cashItemSerialNumber.GetValueOrDefault() == 0L;
        }

        internal static T SelectFirstMatchingTemplate<T>(IReadOnlyList<T> templates)
        {
            return templates == null || templates.Count == 0
                ? default
                : templates[0];
        }

        internal static bool HasEnoughMesoForSendTradeRequest(long currentMeso, long unitPrice, int requestCount)
        {
            long totalPrice = Math.Max(0L, unitPrice) * Math.Max(1, requestCount);
            return totalPrice <= 0L || Math.Max(0L, currentMeso) >= totalPrice;
        }

        internal static bool CanBuildSendTradeRequestPosition(bool requiresInventorySource, int position)
        {
            return !requiresInventorySource || position > 0;
        }

        internal static int ResolveSourceRequestCountCap(
            bool honorConfiguredMaxRequestCount,
            int configuredMaxRequestCount,
            int sourceItemQuantity,
            int selectedSlotQuantity)
        {
            int requestUnit = Math.Max(1, sourceItemQuantity);
            int stackCountCap = Math.Max(1, Math.Max(0, selectedSlotQuantity) / requestUnit);
            if (!honorConfiguredMaxRequestCount)
            {
                return stackCountCap;
            }

            int configuredCap = Math.Max(1, configuredMaxRequestCount);
            return Math.Max(1, Math.Min(configuredCap, stackCountCap));
        }

        internal static int SelectBestPacketOwnedCommodityMetadataSerial(
            int packetSerialNumber,
            int packetItemId,
            long packetPrice,
            int packetMaxPerSlot,
            bool? expectedOnSale,
            IReadOnlyList<PacketOwnedCommodityMetadataCandidate> candidates)
        {
            if (packetItemId <= 0 || candidates == null || candidates.Count == 0)
            {
                return 0;
            }

            long normalizedPacketPrice = Math.Max(0L, Math.Abs(packetPrice));
            bool hasBest = false;
            PacketOwnedCommodityMetadataCandidate best = default;
            for (int i = 0; i < candidates.Count; i++)
            {
                PacketOwnedCommodityMetadataCandidate candidate = candidates[i];
                if (candidate.ItemId != packetItemId || candidate.SerialNumber <= 0)
                {
                    continue;
                }

                if (!hasBest || IsPreferredCandidate(
                        candidate,
                        best,
                        packetSerialNumber,
                        normalizedPacketPrice,
                        packetMaxPerSlot,
                        expectedOnSale))
                {
                    best = candidate;
                    hasBest = true;
                }
            }

            return hasBest ? best.SerialNumber : 0;
        }

        internal static bool CanHydratePacketOwnedCommodityFromMetadata(int packetItemId, int metadataItemId)
        {
            if (metadataItemId <= 0)
            {
                return false;
            }

            return packetItemId <= 0 || packetItemId == metadataItemId;
        }

        internal static int ResolvePacketOwnedCommodityItemId(int packetItemId, int metadataItemId)
        {
            if (packetItemId > 0)
            {
                return packetItemId;
            }

            return Math.Max(0, metadataItemId);
        }

        internal static bool CanCreateFallbackPacketOwnedCommodityRow(
            int packetSerialNumber,
            int packetItemId,
            long packetPrice)
        {
            return packetPrice > 0
                && packetSerialNumber > 0
                && packetItemId <= 0;
        }

        internal static bool CanCreateFallbackPacketOwnedSellTemplateRow(
            int packetSerialNumber,
            int packetItemId,
            long packetPrice)
        {
            return packetPrice <= 0
                && packetSerialNumber > 0
                && packetItemId <= 0;
        }

        internal static bool ShouldUseNonSaleIconVisual(
            bool isPacketOwnedSnapshotRow,
            int commoditySerialNumber,
            int packetSaleState,
            bool commodityOnSale,
            bool isPreviewOnly)
        {
            bool hasClientCommodityIdentity = isPacketOwnedSnapshotRow || commoditySerialNumber > 0;
            if (!hasClientCommodityIdentity)
            {
                return false;
            }

            if (packetSaleState != 0)
            {
                return true;
            }

            if (!commodityOnSale)
            {
                return true;
            }

            return isPacketOwnedSnapshotRow && isPreviewOnly;
        }

        private static bool IsPreferredCandidate(
            PacketOwnedCommodityMetadataCandidate candidate,
            PacketOwnedCommodityMetadataCandidate existing,
            int packetSerialNumber,
            long normalizedPacketPrice,
            int packetMaxPerSlot,
            bool? expectedOnSale)
        {
            bool candidateExactSerial = packetSerialNumber > 0 && candidate.SerialNumber == packetSerialNumber;
            bool existingExactSerial = packetSerialNumber > 0 && existing.SerialNumber == packetSerialNumber;
            if (candidateExactSerial != existingExactSerial)
            {
                return candidateExactSerial;
            }

            bool candidateExactPrice = Math.Max(0L, candidate.Price) == normalizedPacketPrice;
            bool existingExactPrice = Math.Max(0L, existing.Price) == normalizedPacketPrice;
            if (candidateExactPrice != existingExactPrice)
            {
                return candidateExactPrice;
            }

            int normalizedPacketCount = Math.Max(1, packetMaxPerSlot);
            bool preferCountMatch = packetMaxPerSlot > 0;
            if (preferCountMatch)
            {
                bool candidateCountMatch = candidate.Count == normalizedPacketCount;
                bool existingCountMatch = existing.Count == normalizedPacketCount;
                if (candidateCountMatch != existingCountMatch)
                {
                    return candidateCountMatch;
                }
            }

            if (expectedOnSale.HasValue)
            {
                bool candidateMatchesPacketSaleState = candidate.OnSale == expectedOnSale.Value;
                bool existingMatchesPacketSaleState = existing.OnSale == expectedOnSale.Value;
                if (candidateMatchesPacketSaleState != existingMatchesPacketSaleState)
                {
                    return candidateMatchesPacketSaleState;
                }
            }

            if (candidate.OnSale != existing.OnSale)
            {
                return candidate.OnSale;
            }

            if (candidate.Priority != existing.Priority)
            {
                return candidate.Priority > existing.Priority;
            }

            if (candidate.Count != existing.Count)
            {
                return candidate.Count > existing.Count;
            }

            if (candidate.PeriodDays != existing.PeriodDays)
            {
                return candidate.PeriodDays > existing.PeriodDays;
            }

            if (candidate.Price != existing.Price)
            {
                return candidate.Price < existing.Price;
            }

            return candidate.SerialNumber < existing.SerialNumber;
        }
    }
}
