using System;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Entities;

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
            int showdownBonusPercent = ResolveShowdownBonusPercent(mob?.AI);
            if (showdownBonusPercent <= 0)
            {
                return mesoAmount;
            }

            return ApplyRewardBonus(mesoAmount, showdownBonusPercent);
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

        private static int ResolveShowdownBonusPercent(MobAI mobAI)
        {
            return Math.Max(0, mobAI?.GetStatusEffectValue(MobStatusEffect.Showdown) ?? 0);
        }
    }
}
