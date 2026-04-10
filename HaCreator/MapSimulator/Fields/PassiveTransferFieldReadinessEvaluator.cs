namespace HaCreator.MapSimulator.Fields
{
    public readonly record struct PassiveTransferFieldInterfaceState(
        bool HasLiveFieldInterface,
        bool HasPendingMapChange,
        bool HasPlayerInputControl,
        bool HasStandAloneControlOwner,
        bool AllowsTransferField,
        bool HasPendingSpecialTransfer,
        bool HasPendingPacketOwnedTransfer,
        bool HasAttachedPacketOwnedDriver,
        bool HasPendingSameMapTransfer,
        bool HasBlockingScriptedSequence);

    public readonly record struct PassiveTransferFieldQueuedRetryState(
        bool HasLiveFieldInterface,
        bool HasPendingMapChange,
        bool HasBoundPlayer,
        bool IsPlayerActive);

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
                   && !state.HasPendingMapChange
                   && state.HasPlayerInputControl
                   && !state.HasStandAloneControlOwner
                   && state.AllowsTransferField
                   && !state.HasPendingSpecialTransfer
                   && !state.HasPendingPacketOwnedTransfer
                   && !state.HasAttachedPacketOwnedDriver
                   && !state.HasPendingSameMapTransfer
                   && !state.HasBlockingScriptedSequence;
        }

        public static bool ShouldKeepQueuedRetryPending(PassiveTransferFieldQueuedRetryState state)
        {
            return state.HasLiveFieldInterface
                   && !state.HasPendingMapChange
                   && state.HasBoundPlayer
                   && state.IsPlayerActive;
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
