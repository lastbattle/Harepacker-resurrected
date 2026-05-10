using HaCreator.MapSimulator.AI;
using System;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class SummonDamageRuntimeRules
    {
        public readonly record struct BodyContactDamageResult(
            int BaseDamage,
            MobDamageType DamageType,
            int Damage);

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

        public static bool ShouldConsumeClientOwnedHealthFromHit(int damage)
        {
            // CSummoned::SetDamaged / OnHit own hit feedback and hit-period state.
            // Summon removal remains server/expiry-owned; the client does not retire the actor by
            // subtracting packet/body-contact hit damage from an exposed HP value.
            return false;
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

        public static int ResolveBodyContactClientDamage(
            int physicalDamage,
            int currentAttackDamage,
            int magicalDamage,
            bool currentAttackIsMagic,
            Func<int, MobDamageType, int> outgoingDamageResolver)
        {
            return ResolveBodyContactClientDamageResult(
                physicalDamage,
                currentAttackDamage,
                magicalDamage,
                currentAttackIsMagic,
                outgoingDamageResolver).Damage;
        }

        public static BodyContactDamageResult ResolveBodyContactClientDamageResult(
            int physicalDamage,
            int currentAttackDamage,
            int magicalDamage,
            bool currentAttackIsMagic,
            Func<int, MobDamageType, int> outgoingDamageResolver)
        {
            int baseDamage = ResolveBodyContactBaseDamage(
                physicalDamage,
                currentAttackDamage,
                magicalDamage,
                currentAttackIsMagic);
            MobDamageType damageType = ResolveBodyContactDamageType(currentAttackIsMagic);
            int resolvedDamage = outgoingDamageResolver?.Invoke(baseDamage, damageType) ?? baseDamage;
            return new BodyContactDamageResult(baseDamage, damageType, Math.Max(1, resolvedDamage));
        }

        public static int ResolveBodyContactRelativeMotionX(
            float mobCurrentX,
            float mobPreviousX,
            float summonCurrentX,
            float summonPreviousX,
            float summonCurrentY = 0f,
            float summonPreviousY = 0f,
            bool summonOnLadderOrRope = false)
        {
            if (summonOnLadderOrRope && summonCurrentY - summonPreviousY < 0f)
            {
                return int.MaxValue;
            }

            float mobDeltaX = mobCurrentX - mobPreviousX;
            float summonDeltaX = summonCurrentX - summonPreviousX;
            return (int)MathF.Round(mobDeltaX - summonDeltaX);
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
