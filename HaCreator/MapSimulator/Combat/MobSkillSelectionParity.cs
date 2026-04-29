using System.Collections.Generic;
using HaCreator.MapSimulator.AI;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Combat;

internal static class MobSkillSelectionParity
{
    public static bool ShouldAutoSelectMobStatusSkill(
        MobSkillStatusDefinition definition,
        MobSkillRuntimeData runtimeData,
        IEnumerable<MobAI> candidateTargets)
    {
        if (candidateTargets == null)
        {
            return false;
        }

        switch (definition.Operation)
        {
            case MobSkillOperation.Heal:
                int healThresholdPercent = ResolveHealThresholdPercent(runtimeData);
                foreach (MobAI target in candidateTargets)
                {
                    if (target != null
                        && !target.IsDead
                        && target.HpPercent * 100f < healThresholdPercent)
                    {
                        return true;
                    }
                }

                return false;

            case MobSkillOperation.ClearNegativeStatuses:
                foreach (MobAI target in candidateTargets)
                {
                    if (target?.IsDead != false)
                    {
                        continue;
                    }

                    if (target.HasNegativeStatusEffects())
                    {
                        return true;
                    }
                }

                return false;

            case MobSkillOperation.ApplyStatus:
                int statusValue = MobSkillStatusMapper.ResolveStatusValue(
                    definition.Effect,
                    runtimeData?.X ?? 0,
                    runtimeData?.Y ?? 0,
                    runtimeData?.Hp ?? 0);
                if (statusValue <= 0)
                {
                    return false;
                }

                foreach (MobAI target in candidateTargets)
                {
                    if (ShouldApplyStatusToTarget(target, definition, runtimeData, statusValue))
                    {
                        return true;
                    }
                }

                return false;

            default:
                return false;
        }
    }

    public static bool ShouldAutoSelectPlayerTargetSkill(Rectangle area, Rectangle playerHitbox)
    {
        return !area.IsEmpty
            && !playerHitbox.IsEmpty
            && playerHitbox.Intersects(area);
    }

    internal static bool ShouldApplyStatusToTarget(
        MobAI target,
        MobSkillStatusDefinition definition,
        MobSkillRuntimeData runtimeData)
    {
        int statusValue = MobSkillStatusMapper.ResolveStatusValue(
            definition.Effect,
            runtimeData?.X ?? 0,
            runtimeData?.Y ?? 0,
            runtimeData?.Hp ?? 0);
        return ShouldApplyStatusToTarget(target, definition, runtimeData, statusValue);
    }

    private static bool ShouldApplyStatusToTarget(
        MobAI target,
        MobSkillStatusDefinition definition,
        MobSkillRuntimeData runtimeData,
        int statusValue)
    {
        if (target?.IsDead != false)
        {
            return false;
        }

        if (!IsAuthoredHpGateOpen(target, definition, runtimeData))
        {
            return false;
        }

        if (!target.HasStatusEffect(definition.Effect))
        {
            return true;
        }

        return target.GetStatusEffectValue(definition.Effect) < statusValue;
    }

    private static bool IsAuthoredHpGateOpen(
        MobAI target,
        MobSkillStatusDefinition definition,
        MobSkillRuntimeData runtimeData)
    {
        if (definition.Operation != MobSkillOperation.ApplyStatus || runtimeData == null)
        {
            return true;
        }

        int hpThresholdPercent = runtimeData.Hp;
        if (hpThresholdPercent <= 0 || hpThresholdPercent >= 100)
        {
            return true;
        }

        float hpPercent = System.Math.Clamp(target.HpPercent, 0f, 1f) * 100f;
        return hpPercent <= hpThresholdPercent;
    }

    private static int ResolveHealThresholdPercent(MobSkillRuntimeData runtimeData)
    {
        int healThresholdPercent = runtimeData?.Hp ?? 0;
        if (healThresholdPercent <= 0)
        {
            healThresholdPercent = 100;
        }

        return System.Math.Clamp(healThresholdPercent, 1, 100);
    }
}
