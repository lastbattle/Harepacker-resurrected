using HaCreator.MapSimulator.AI;

namespace HaCreator.MapSimulator.Combat
{
    internal enum MobSkillStatusTargetMode
    {
        Self,
        NearbyMobs
    }

    internal readonly record struct MobSkillStatusDefinition(
        int SkillId,
        MobStatusEffect Effect,
        MobSkillStatusTargetMode TargetMode);

    internal static class MobSkillStatusMapper
    {
        public static bool TryGetDefinition(int skillId, out MobSkillStatusDefinition definition)
        {
            switch (skillId)
            {
                case 100:
                    definition = new MobSkillStatusDefinition(skillId, MobStatusEffect.PowerUp, MobSkillStatusTargetMode.Self);
                    return true;
                case 101:
                    definition = new MobSkillStatusDefinition(skillId, MobStatusEffect.MagicUp, MobSkillStatusTargetMode.Self);
                    return true;
                case 102:
                    definition = new MobSkillStatusDefinition(skillId, MobStatusEffect.PGuardUp, MobSkillStatusTargetMode.Self);
                    return true;
                case 103:
                    definition = new MobSkillStatusDefinition(skillId, MobStatusEffect.MGuardUp, MobSkillStatusTargetMode.Self);
                    return true;
                case 110:
                    definition = new MobSkillStatusDefinition(skillId, MobStatusEffect.PowerUp, MobSkillStatusTargetMode.NearbyMobs);
                    return true;
                case 111:
                    definition = new MobSkillStatusDefinition(skillId, MobStatusEffect.MagicUp, MobSkillStatusTargetMode.NearbyMobs);
                    return true;
                case 112:
                    definition = new MobSkillStatusDefinition(skillId, MobStatusEffect.PGuardUp, MobSkillStatusTargetMode.NearbyMobs);
                    return true;
                case 113:
                    definition = new MobSkillStatusDefinition(skillId, MobStatusEffect.MGuardUp, MobSkillStatusTargetMode.NearbyMobs);
                    return true;
                case 115:
                    definition = new MobSkillStatusDefinition(skillId, MobStatusEffect.Speed, MobSkillStatusTargetMode.NearbyMobs);
                    return true;
                default:
                    definition = default;
                    return false;
            }
        }

        public static int ResolveStatusValue(MobStatusEffect effect, int x, int y, int hp)
        {
            return effect switch
            {
                MobStatusEffect.PowerUp => x,
                MobStatusEffect.MagicUp => x,
                MobStatusEffect.PGuardUp => x,
                MobStatusEffect.MGuardUp => x,
                MobStatusEffect.Speed => x,
                _ => 0
            };
        }
    }
}
