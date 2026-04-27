using HaCreator.MapSimulator.AI;
using System;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class SummonDamageRuntimeRules
    {
        public static int ResolveRemainingHealth(int currentHealth, int maxHealth, int damage)
        {
            int resolvedMaxHealth = Math.Max(1, maxHealth);
            int resolvedCurrentHealth = currentHealth > 0 ? currentHealth : resolvedMaxHealth;
            if (damage <= 0)
            {
                return resolvedCurrentHealth;
            }

            return Math.Max(0, resolvedCurrentHealth - damage);
        }

        public static int ResolveHitPeriodRemainingMs(int damage, int hitPeriodDurationMs)
        {
            int duration = Math.Max(0, hitPeriodDurationMs);
            return damage > 0 ? duration : -duration;
        }

        public static bool ShouldPlaySummonHitAction(int damage)
        {
            return damage > 0;
        }

        public static int ResolveBodyContactBaseDamage(
            int physicalDamage,
            int currentAttackDamage,
            int magicalDamage,
            bool currentAttackIsMagic = false)
        {
            if (currentAttackIsMagic)
            {
                if (magicalDamage > 0)
                {
                    return magicalDamage;
                }

                if (currentAttackDamage > 0)
                {
                    return currentAttackDamage;
                }
            }

            if (physicalDamage > 0)
            {
                return physicalDamage;
            }

            if (currentAttackDamage > 0)
            {
                return currentAttackDamage;
            }

            if (magicalDamage > 0)
            {
                return magicalDamage;
            }

            return 1;
        }

        public static MobDamageType ResolveBodyContactDamageType(bool currentAttackIsMagic)
        {
            return currentAttackIsMagic
                ? MobDamageType.Magical
                : MobDamageType.Physical;
        }

        public static int ResolveBodyContactRelativeMotionX(
            float mobCurrentX,
            float mobVelocityX,
            float summonCurrentX,
            float summonPreviousX)
        {
            float summonDeltaX = summonCurrentX - summonPreviousX;
            return (int)MathF.Round(mobVelocityX - summonDeltaX);
        }

        public static bool? ResolveBodyContactHitFacingRight(int relativeMotionX)
        {
            if (relativeMotionX == 0)
            {
                return null;
            }

            return relativeMotionX > 0;
        }
    }
}
