using System;

namespace HaCreator.MapSimulator.Fields
{
    public readonly record struct PassiveTransferFieldInterfaceState(
        bool HasLiveFieldInterface,
        bool HasCollidingTransferPortal,
        bool HasActiveVectorControl,
        bool HasPendingMapChange,
        bool HasPlayerInputControl,
        bool HasStandAloneControlOwner,
        bool AllowsTransferField,
        bool HasPendingSpecialTransfer,
        bool HasPendingPacketOwnedTransfer,
        bool HasPacketOwnedTeleportRegistrationCoolingDown,
        bool HasPendingExclusiveTransferRequest,
        bool HasAttachedPacketOwnedDriver,
        bool HasPendingSameMapTransfer,
        bool HasBlockingScriptedSequence);

    public readonly record struct PassiveTransferFieldQueuedRetryState(
        bool HasLiveFieldInterface,
        bool HasPendingMapChange,
        bool HasBoundPlayer,
        bool IsPlayerActive);

    public readonly record struct PassiveTransferFieldQueuedRetryDecisionState(
        bool HasPendingRequest,
        bool HasOneTimeActionCompleted,
        bool HasReadyFieldInterface,
        bool HasCollidingTransferPortal,
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
        public enum QueuedRetryDecision
        {
            Clear = 0,
            KeepPending = 1,
            ReplayHandleUpKeyDown = 2
        }

        public static bool CanRetryFromLiveFieldInterface(PassiveTransferFieldInterfaceState state)
        {
            return state.HasLiveFieldInterface
                   && state.HasCollidingTransferPortal
                   && state.HasActiveVectorControl
                   && !state.HasPendingMapChange
                   && state.HasPlayerInputControl
                   && !state.HasStandAloneControlOwner
                   && state.AllowsTransferField
                   && !state.HasPendingSpecialTransfer
                   && !state.HasPendingPacketOwnedTransfer
                   && !state.HasPacketOwnedTeleportRegistrationCoolingDown
                   && !state.HasPendingExclusiveTransferRequest
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

        public static QueuedRetryDecision EvaluateQueuedRetryDecision(PassiveTransferFieldQueuedRetryDecisionState state)
        {
            if (!state.HasPendingRequest)
            {
                return QueuedRetryDecision.Clear;
            }

            bool shouldKeepPending = ShouldKeepQueuedRetryPending(
                new PassiveTransferFieldQueuedRetryState(
                    state.HasLiveFieldInterface,
                    state.HasPendingMapChange,
                    state.HasBoundPlayer,
                    state.IsPlayerActive));

            if (state.HasOneTimeActionCompleted && state.HasReadyFieldInterface)
            {
                if (!shouldKeepPending)
                {
                    return QueuedRetryDecision.Clear;
                }

                return state.HasCollidingTransferPortal
                    ? QueuedRetryDecision.ReplayHandleUpKeyDown
                    : QueuedRetryDecision.Clear;
            }

            return shouldKeepPending
                ? QueuedRetryDecision.KeepPending
                : QueuedRetryDecision.Clear;
        }

        public static bool ShouldCancelQueuedRetryOnHorizontalKeyDown(
            bool hasPendingRequest,
            bool leftKeyPressed,
            bool rightKeyPressed)
        {
            return hasPendingRequest && (leftKeyPressed || rightKeyPressed);
        }

        public static bool IsExclusiveTransferRequestInFlight(
            bool requestSent,
            int requestSentTick,
            int currentTick,
            int cooldownMs)
        {
            if (!requestSent)
            {
                return false;
            }

            if (requestSentTick == int.MinValue)
            {
                return true;
            }

            return unchecked(currentTick - requestSentTick) < Math.Max(0, cooldownMs);
        }

        public static bool CanHandleFreshUpKeyDown(bool hasAttachedPacketOwnedDriver)
        {
            return !hasAttachedPacketOwnedDriver;
        }

        public static bool CanQueuePassiveTransferFieldRequest(
            bool hasClientOwnedOneTimeAction,
            bool hasPassiveTransferFieldPortalCollision)
        {
            return hasClientOwnedOneTimeAction && hasPassiveTransferFieldPortalCollision;
        }

        public static bool ShouldClearQueuedRetryOnChairGetUp(
            bool hasPendingRequest,
            bool consumedChairGetUpBranch)
        {
            return hasPendingRequest && consumedChairGetUpBranch;
        }

        public static bool CanReplayHandleUpKeyDown(PassiveTransferFieldReplayState state)
        {
            return CanAttemptHandleUpKeyDownReplay(state)
                   && state.IsOnFoothold;
        }

        public static bool CanAttemptHandleUpKeyDownReplay(PassiveTransferFieldReplayState state)
        {
            return state.HasOneTimeActionCompleted
                   && !state.IsImmovable
                   && !state.IsAttractLocked;
        }

        public static bool ShouldStopSkillMacroForQueuedReplay(bool canAttemptHandleUpKeyDownReplay)
        {
            return canAttemptHandleUpKeyDownReplay;
        }
    }
}
