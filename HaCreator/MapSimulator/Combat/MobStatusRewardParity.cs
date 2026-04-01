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
            return showdownBonusPercent > 0
                ? Math.Max(0, (int)MathF.Round(baseExp * (1f + showdownBonusPercent / 100f)))
                : baseExp;
        }

        public static int ResolveMesoAmount(MobItem mob, int baseAmount)
        {
            int mesoAmount = Math.Max(0, baseAmount);
            int showdownBonusPercent = ResolveShowdownBonusPercent(mob?.AI);
            if (showdownBonusPercent <= 0)
            {
                return mesoAmount;
            }

            return Math.Max(0, (int)MathF.Round(mesoAmount * (1f + showdownBonusPercent / 100f)));
        }

        private static int ResolveShowdownBonusPercent(MobAI mobAI)
        {
            return Math.Max(0, mobAI?.GetStatusEffectValue(MobStatusEffect.Showdown) ?? 0);
        }
    }
}
