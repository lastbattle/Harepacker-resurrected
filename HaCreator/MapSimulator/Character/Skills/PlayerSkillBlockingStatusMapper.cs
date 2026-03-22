namespace HaCreator.MapSimulator.Character.Skills
{
    public enum PlayerSkillBlockingStatus
    {
        Stun,
        Seal,
        Attract
    }

    public static class PlayerSkillBlockingStatusMapper
    {
        public static bool TryMapMobSkill(int skillId, out PlayerSkillBlockingStatus status)
        {
            switch (skillId)
            {
                case 120:
                    status = PlayerSkillBlockingStatus.Seal;
                    return true;
                case 123:
                case 131:
                    status = PlayerSkillBlockingStatus.Stun;
                    return true;
                case 128:
                    status = PlayerSkillBlockingStatus.Attract;
                    return true;
                default:
                    status = default;
                    return false;
            }
        }

        public static string GetRestrictionMessage(PlayerSkillBlockingStatus status)
        {
            return status switch
            {
                PlayerSkillBlockingStatus.Stun => "Skills cannot be used while stunned.",
                PlayerSkillBlockingStatus.Seal => "Skills cannot be used while sealed.",
                PlayerSkillBlockingStatus.Attract => "Skills cannot be used while seduced.",
                _ => null
            };
        }
    }
}
