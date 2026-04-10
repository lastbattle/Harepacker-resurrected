namespace HaCreator.MapSimulator.Fields
{
    public readonly record struct PassiveTransferFieldInterfaceState(
        bool HasLiveFieldInterface,
        bool AllowsTransferField,
        bool HasPendingSpecialTransfer,
        bool HasPendingPacketOwnedTransfer,
        bool HasPendingSameMapTransfer,
        bool HasBlockingScriptedSequence);

    public readonly record struct PassiveTransferFieldReplayState(
        bool HasOneTimeActionCompleted,
        bool IsImmovable,
        bool IsAttractLocked,
        bool IsOnFoothold);

    public static class PassiveTransferFieldReadinessEvaluator
    {
        public static bool CanRetryFromLiveFieldInterface(PassiveTransferFieldInterfaceState state)
        {
            return state.HasLiveFieldInterface
                   && state.AllowsTransferField
                   && !state.HasPendingSpecialTransfer
                   && !state.HasPendingPacketOwnedTransfer
                   && !state.HasPendingSameMapTransfer
                   && !state.HasBlockingScriptedSequence;
        }

        public static bool CanReplayHandleUpKeyDown(PassiveTransferFieldReplayState state)
        {
            return state.HasOneTimeActionCompleted
                   && !state.IsImmovable
                   && !state.IsAttractLocked
                   && state.IsOnFoothold;
        }
    }
}
