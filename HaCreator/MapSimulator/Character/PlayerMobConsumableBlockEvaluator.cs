namespace HaCreator.MapSimulator.Character
{
    internal static class PlayerMobConsumableBlockEvaluator
    {
        public static bool IsStopPotionBlocked(
            bool hasStopPotionStatus,
            bool hasSupportedRecovery,
            bool hasSupportedCure,
            bool hasSupportedMovement,
            bool hasSupportedMorph,
            bool hasSupportedTemporaryBuff)
        {
            return hasStopPotionStatus &&
                   (hasSupportedRecovery ||
                    hasSupportedCure ||
                    hasSupportedMovement ||
                    hasSupportedMorph ||
                    hasSupportedTemporaryBuff);
        }
    }
}
