namespace HaCreator.MapSimulator.Character.Skills
{
    internal enum SkillCooldownNotificationTransition
    {
        None,
        Started,
        Ready
    }

    internal static class SkillCooldownNotificationTransitionResolver
    {
        public static SkillCooldownNotificationTransition ResolvePacketOwnedTransition(
            bool hadActiveCooldownBefore,
            bool hasActiveCooldownAfter)
        {
            if (!hadActiveCooldownBefore && hasActiveCooldownAfter)
            {
                return SkillCooldownNotificationTransition.Started;
            }

            if (hadActiveCooldownBefore && !hasActiveCooldownAfter)
            {
                return SkillCooldownNotificationTransition.Ready;
            }

            return SkillCooldownNotificationTransition.None;
        }
    }
}
