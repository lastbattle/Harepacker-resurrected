namespace HaCreator.MapSimulator.Character
{
    internal static class PlayerMobConsumableBlockEvaluator
    {
        public static bool IsStopPotionBlocked(
            bool hasStopPotionStatus,
            bool hasSupportedRecovery,
            bool hasSupportedCure)
        {
            return hasStopPotionStatus && (hasSupportedRecovery || hasSupportedCure);
        }
    }
}
