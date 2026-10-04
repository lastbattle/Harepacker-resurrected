using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Entities;
using MapleLib.WzLib.WzStructure.Data.MobStructure;
using System;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class SummonDamageRuntimeRules
    {
        public enum BodyContactDamageFormulaKind
        {
            PDamageSummoned,
            MDamageSummoned
        }

        public readonly record struct BodyContactDamageFormulaTrace(
            BodyContactDamageFormulaKind FormulaKind,
            int AttackValue,
            uint DamageRandom,
            double RolledBaseDamage,
            int AdditiveDefense,
            int AttackLevel,
            int TargetLevel,
            int PassiveDamageReductionPercent,
            double DefenseAdjustedDamage,
            int InvinciblePercent,
            int SwallowDefensePercent,
            int PowerOrMagicUpPercent,
            double FinalUnclampedDamage);

        public readonly record struct BodyContactDamageFormulaInput(
            BodyContactDamageFormulaKind FormulaKind,
            int AttackValue,
            uint DamageRandom,
            int AdditiveDefense = 0,
            int AttackLevel = 0,
            int TargetLevel = 0,
            int PassiveDamageReductionPercent = 0,
            int InvinciblePercent = 0,
            int SwallowDefensePercent = 0,
            int PowerOrMagicUpPercent = 0);

        public readonly record struct BodyContactDamageResult(
            int BaseDamage,
            MobDamageType DamageType,
            int Damage)
        {
            public BodyContactDamageFormulaTrace FormulaTrace { get; init; }
        }

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

        public static int ResolvePhysicalDamageSummoned(
            int baseDamage,
            Func<int, MobDamageType, int> outgoingDamageResolver)
        {
            int resolvedDamage = outgoingDamageResolver?.Invoke(baseDamage, MobDamageType.Physical) ?? baseDamage;
            return Math.Max(1, resolvedDamage);
        }

        public static int ResolveMagicalDamageSummoned(
            int baseDamage,
            Func<int, MobDamageType, int> outgoingDamageResolver)
        {
            int resolvedDamage = outgoingDamageResolver?.Invoke(baseDamage, MobDamageType.Magical) ?? baseDamage;
            return Math.Max(1, resolvedDamage);
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
            int resolvedDamage = currentAttackIsMagic
                ? ResolveMagicalDamageSummoned(baseDamage, outgoingDamageResolver)
                : ResolvePhysicalDamageSummoned(baseDamage, outgoingDamageResolver);
            BodyContactDamageFormulaKind formulaKind = currentAttackIsMagic
                ? BodyContactDamageFormulaKind.MDamageSummoned
                : BodyContactDamageFormulaKind.PDamageSummoned;
            return new BodyContactDamageResult(baseDamage, damageType, resolvedDamage)
            {
                FormulaTrace = BuildDelegatedDamageFormulaTrace(
                    formulaKind,
                    baseDamage,
                    resolvedDamage)
            };
        }

        public static BodyContactDamageResult ResolveBodyContactClientDamageResult(
            MobItem mob,
            uint damageRandom = 0,
            int targetLevel = 0,
            int targetPhysicalDefense = 0,
            int targetMagicalDefense = 0,
            int targetPhysicalDamageReductionPercent = 0,
            int targetMagicalDamageReductionPercent = 0,
            int targetSwallowDefensePercent = 0,
            int targetInvinciblePercent = 0)
        {
            MobData mobData = mob?.MobData;
            MobAttackEntry currentAttack = mob?.AI?.GetCurrentAttack();
            bool currentAttackIsMagic = currentAttack?.MagicAttack == true;
            int baseDamage = ResolveBodyContactBaseDamage(
                mobData?.PADamage ?? 0,
                currentAttack?.Damage ?? 0,
                mobData?.MADamage ?? 0,
                currentAttackIsMagic);
            MobDamageType damageType = ResolveBodyContactDamageType(currentAttackIsMagic);
            BodyContactDamageFormulaKind formulaKind = currentAttackIsMagic
                ? BodyContactDamageFormulaKind.MDamageSummoned
                : BodyContactDamageFormulaKind.PDamageSummoned;
            int additiveDefense = currentAttackIsMagic
                ? targetMagicalDefense
                : targetPhysicalDefense;
            int passiveReduction = currentAttackIsMagic
                ? targetMagicalDamageReductionPercent
                : targetPhysicalDamageReductionPercent;
            int powerOrMagicUpPercent = currentAttackIsMagic
                ? ResolveMobStatusPercent(mob?.AI, MobStatusEffect.MADamage)
                  + ResolveMobStatusPercent(mob?.AI, MobStatusEffect.MagicUp)
                : ResolveMobStatusPercent(mob?.AI, MobStatusEffect.PADamage)
                  + ResolveMobStatusPercent(mob?.AI, MobStatusEffect.PowerUp);
            BodyContactDamageFormulaInput input = new(
                formulaKind,
                baseDamage,
                damageRandom,
                additiveDefense,
                mobData != null ? mobData.Level : mob?.AI?.Level ?? 0,
                targetLevel,
                passiveReduction,
                targetInvinciblePercent,
                targetSwallowDefensePercent,
                powerOrMagicUpPercent);
            int resolvedDamage = ResolveDamageSummonedFormula(input, out BodyContactDamageFormulaTrace trace);
            return new BodyContactDamageResult(baseDamage, damageType, resolvedDamage)
            {
                FormulaTrace = trace
            };
        }

        public static int ResolveDamageSummonedFormula(
            BodyContactDamageFormulaInput input,
            out BodyContactDamageFormulaTrace trace)
        {
            trace = ResolveDamageSummonedFormulaTrace(input);
            return ClampClientSummonedDamage(trace.FinalUnclampedDamage);
        }

        public static BodyContactDamageFormulaTrace ResolveDamageSummonedFormulaTrace(
            BodyContactDamageFormulaInput input)
        {
            int attackValue = Math.Clamp(input.AttackValue, 0, 29999);
            double rolledDamage = ResolveClientMobBaseDamage(attackValue, input.DamageRandom);
            double defenseAdjustedDamage = ResolveClientAdjustedBaseDefense(
                rolledDamage,
                input.AdditiveDefense,
                input.AttackLevel,
                input.TargetLevel,
                input.PassiveDamageReductionPercent);

            double reducedDamage = defenseAdjustedDamage;
            int invinciblePercent = input.FormulaKind == BodyContactDamageFormulaKind.PDamageSummoned
                ? Math.Clamp(input.InvinciblePercent, 0, 100)
                : 0;
            if (invinciblePercent > 0)
            {
                reducedDamage -= invinciblePercent * reducedDamage / 100.0;
            }

            int swallowDefensePercent = Math.Clamp(input.SwallowDefensePercent, 0, 100);
            if (swallowDefensePercent > 0)
            {
                reducedDamage -= swallowDefensePercent * reducedDamage / 100.0;
            }

            int powerOrMagicUpPercent = Math.Max(0, input.PowerOrMagicUpPercent);
            double finalDamage = powerOrMagicUpPercent > 0
                ? powerOrMagicUpPercent * reducedDamage / 100.0
                : reducedDamage;

            return new BodyContactDamageFormulaTrace(
                input.FormulaKind,
                attackValue,
                input.DamageRandom,
                rolledDamage,
                Math.Max(0, input.AdditiveDefense),
                Math.Max(0, input.AttackLevel),
                Math.Max(0, input.TargetLevel),
                Math.Max(0, input.PassiveDamageReductionPercent),
                defenseAdjustedDamage,
                invinciblePercent,
                swallowDefensePercent,
                powerOrMagicUpPercent,
                finalDamage);
        }

        public static double ResolveClientMobBaseDamage(int attackValue, uint randomValue)
        {
            int clampedAttackValue = Math.Clamp(attackValue, 0, 29999);
            return ResolveClientRandomRange(
                randomValue,
                clampedAttackValue,
                0.85 * clampedAttackValue);
        }

        public static double ResolveClientAdjustedBaseDefense(
            double damage,
            int additiveDefense,
            int attackLevel,
            int targetLevel,
            int passiveDamageReductionPercent)
        {
            int defense = Math.Max(0, additiveDefense);
            double quarterDefense = defense * 0.25;
            int fixedCanceling = (int)(0.5 + quarterDefense);
            int sqrtDefense = (int)Math.Sqrt(quarterDefense);
            int percentCanceling = sqrtDefense
                                   + Math.Max(0, passiveDamageReductionPercent) * sqrtDefense / 100;

            int resolvedAttackLevel = Math.Max(0, attackLevel);
            int resolvedTargetLevel = Math.Max(0, targetLevel);
            if (resolvedTargetLevel < resolvedAttackLevel)
            {
                int levelDifference = Math.Abs(resolvedAttackLevel - resolvedTargetLevel);
                fixedCanceling -= Math.Min(levelDifference * 4, fixedCanceling);
                percentCanceling -= Math.Min(levelDifference * 2, percentCanceling);
            }

            double fixedAdjustedDamage = damage - fixedCanceling;
            double percentAdjustedDamage = damage * (100 - percentCanceling) / 100.0;
            double result = Math.Min(fixedAdjustedDamage, percentAdjustedDamage);
            return result <= 1.0 ? 1.0 : result;
        }

        public static double ResolveClientRandomRange(uint randomValue, double first, double second)
        {
            if (first.Equals(second))
            {
                return first;
            }

            double low = Math.Min(first, second);
            double high = Math.Max(first, second);
            return low + randomValue % 10000000 * (high - low) / 9999999.0;
        }

        public static int ClampClientSummonedDamage(double damage)
        {
            if (damage <= 1.0)
            {
                return 1;
            }

            if (damage > 999999.0)
            {
                return 999999;
            }

            return (int)damage;
        }

        private static BodyContactDamageFormulaTrace BuildDelegatedDamageFormulaTrace(
            BodyContactDamageFormulaKind formulaKind,
            int baseDamage,
            int resolvedDamage)
        {
            int attackValue = Math.Clamp(baseDamage, 0, 29999);
            double finalDamage = Math.Max(1, resolvedDamage);
            return new BodyContactDamageFormulaTrace(
                formulaKind,
                attackValue,
                DamageRandom: 0,
                RolledBaseDamage: attackValue,
                AdditiveDefense: 0,
                AttackLevel: 0,
                TargetLevel: 0,
                PassiveDamageReductionPercent: 0,
                DefenseAdjustedDamage: attackValue,
                InvinciblePercent: 0,
                SwallowDefensePercent: 0,
                PowerOrMagicUpPercent: 0,
                FinalUnclampedDamage: finalDamage);
        }

        private static int ResolveMobStatusPercent(MobAI mobAI, MobStatusEffect effect)
        {
            return mobAI?.GetClientStatusPercentForDamageFormula(effect) ?? 0;
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
