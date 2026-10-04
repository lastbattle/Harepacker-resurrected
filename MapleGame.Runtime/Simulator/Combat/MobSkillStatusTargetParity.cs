namespace HaCreator.MapSimulator.Combat
{
    internal static class MobSkillStatusTargetParity
    {
        internal static bool AreEncounterTeamsCompatible(int? sourceTeam, int? targetTeam)
        {
            return !sourceTeam.HasValue
                   || !targetTeam.HasValue
                   || sourceTeam.Value == targetTeam.Value;
        }
    }
}
