using System;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace HaCreator.MapSimulator.Combat
{
    internal static class MobStatusRewardParity
    {
        public static int ResolveKillExperience(MobItem mob)
        {
            int baseExp = Math.Max(0, mob?.AI?.Exp ?? 0);
            if (baseExp <= 0)
            {
                return 0;
            }

            int showdownBonusPercent = ResolveShowdownBonusPercent(mob?.AI);
            return ApplyRewardBonus(baseExp, showdownBonusPercent);
        }

        public static int ResolveMesoAmount(MobItem mob, int baseAmount)
        {
            int mesoAmount = Math.Max(0, baseAmount);
            MobAI mobAI = mob?.AI;
            int totalBonusPercent = ResolveMesoBonusPercent(mobAI);
            if (totalBonusPercent <= 0)
            {
                return mesoAmount;
            }

            return ApplyRewardBonus(mesoAmount, totalBonusPercent);
        }

        public static int ResolveItemQuantity(MobItem mob, int baseQuantity)
        {
            return ResolveItemQuantity(mob?.AI, baseQuantity);
        }

        public static int ResolveDropItemQuantity(MobItem mob, int itemId, int baseQuantity)
        {
            return ResolveDropItemQuantity(mob?.AI, itemId, baseQuantity);
        }

        internal static int ApplyRewardBonus(int baseAmount, int percentBonus)
        {
            int amount = Math.Max(0, baseAmount);
            int bonusPercent = Math.Max(0, percentBonus);
            if (amount <= 0 || bonusPercent <= 0)
            {
                return amount;
            }

            return Math.Max(0, (int)MathF.Round(amount * (1f + bonusPercent / 100f)));
        }

        internal static int ResolveItemQuantity(MobAI mobAI, int baseQuantity, int bonusRollPercent = -1)
        {
            int quantity = Math.Max(0, baseQuantity);
            if (quantity <= 0)
            {
                return 0;
            }

            int percentBonus = ResolveShowdownBonusPercent(mobAI);
            if (percentBonus <= 0)
            {
                return quantity;
            }

            int scaledBonus = quantity * percentBonus;
            int guaranteedExtra = scaledBonus / 100;
            int remainderPercent = scaledBonus % 100;
            if (remainderPercent > 0)
            {
                int roll = bonusRollPercent >= 0
                    ? Math.Clamp(bonusRollPercent, 0, 99)
                    : Random.Shared.Next(100);
                if (roll < remainderPercent)
                {
                    guaranteedExtra++;
                }
            }

            return quantity + guaranteedExtra;
        }

        internal static int ResolveDropItemQuantity(MobAI mobAI, int itemId, int baseQuantity, int bonusRollPercent = -1)
        {
            int quantity = Math.Max(0, baseQuantity);
            if (quantity <= 0)
            {
                return 0;
            }

            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            if (InventoryItemMetadataResolver.ResolveMaxStack(inventoryType) <= 1)
            {
                return quantity;
            }

            return ResolveItemQuantity(mobAI, quantity, bonusRollPercent);
        }

        internal static int ResolveAuthoredRewardItemId(System.Collections.Generic.IReadOnlyList<int> rewardItemIds, int selectionRoll = -1)
        {
            if (rewardItemIds == null || rewardItemIds.Count == 0)
            {
                return 0;
            }

            int index = selectionRoll >= 0
                ? Math.Abs(selectionRoll) % rewardItemIds.Count
                : Random.Shared.Next(rewardItemIds.Count);
            return Math.Max(0, rewardItemIds[index]);
        }

        internal static int ResolveStableAuthoredRewardItemId(
            int mobId,
            int poolId,
            System.Collections.Generic.IReadOnlyList<int> rewardItemIds)
        {
            if (rewardItemIds == null || rewardItemIds.Count == 0)
            {
                return 0;
            }

            int stableSeed = HashCode.Combine(Math.Max(0, mobId), Math.Max(0, poolId));
            int index = (stableSeed & int.MaxValue) % rewardItemIds.Count;
            return Math.Max(0, rewardItemIds[index]);
        }

        private static int ResolveShowdownBonusPercent(MobAI mobAI)
        {
            return Math.Max(0, mobAI?.GetStatusEffectValue(MobStatusEffect.Showdown) ?? 0);
        }

        internal static int ResolveMesoBonusPercent(MobAI mobAI)
        {
            int showdownBonusPercent = ResolveShowdownBonusPercent(mobAI);
            int richBonusPercent = ResolveRichBonusPercent(mobAI);
            return showdownBonusPercent + richBonusPercent;
        }

        private static int ResolveRichBonusPercent(MobAI mobAI)
        {
            int richValue = Math.Max(0, mobAI?.GetStatusEffectValue(MobStatusEffect.Rich) ?? 0);
            return richValue <= 0 ? 0 : richValue * 100;
        }
    }
}
