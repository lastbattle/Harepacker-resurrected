using System;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class SummonDamageRuntimeRules
    {
        public static int ResolveRemainingHealth(int currentHealth, int maxHealth, int damage)
        {
            int resolvedMaxHealth = Math.Max(1, maxHealth);
            int resolvedCurrentHealth = currentHealth > 0 ? currentHealth : resolvedMaxHealth;
            int resolvedDamage = Math.Max(1, damage);
            return Math.Max(0, resolvedCurrentHealth - resolvedDamage);
        }

        public static int ResolveBodyContactBaseDamage(int physicalDamage, int currentAttackDamage, int magicalDamage)
        {
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
    }
}
