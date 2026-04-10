namespace HaCreator.MapSimulator.Fields
{
    public static class PassiveTransferFieldReadinessEvaluator
    {
        public static bool CanRetryFromLiveFieldInterface(long fieldLimit, bool hasLiveFieldInterface)
        {
            return hasLiveFieldInterface
                   && FieldInteractionRestrictionEvaluator.CanTransferField(fieldLimit);
        }
    }
}
