namespace HaCreator.MapSimulator.Fields
{
    public readonly record struct PassiveTransferFieldInterfaceState(
        bool HasLiveFieldInterface,
        bool AllowsTransferField,
        bool HasPendingSpecialTransfer);

    public static class PassiveTransferFieldReadinessEvaluator
    {
        public static bool CanRetryFromLiveFieldInterface(PassiveTransferFieldInterfaceState state)
        {
            return state.HasLiveFieldInterface
                   && state.AllowsTransferField
                   && !state.HasPendingSpecialTransfer;
        }
    }
}
