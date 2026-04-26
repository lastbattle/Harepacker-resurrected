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
        bool IsPlayerActive,
        bool HasReadyFieldInterface);

    public readonly record struct PassiveTransferFieldInterfaceGateState(
        bool HasLiveFieldInterface,
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
            return state.HasCollidingTransferPortal
                   && state.HasActiveVectorControl
                   && CanAdmitQueuedRetryInterfaceGate(
                       new PassiveTransferFieldInterfaceGateState(
                           state.HasLiveFieldInterface,
                           state.HasPendingMapChange,
                           state.HasPlayerInputControl,
                           state.HasStandAloneControlOwner,
                           state.AllowsTransferField,
                           state.HasPendingSpecialTransfer,
                           state.HasPendingPacketOwnedTransfer,
                           state.HasPacketOwnedTeleportRegistrationCoolingDown,
                           state.HasPendingExclusiveTransferRequest,
                           state.HasAttachedPacketOwnedDriver,
                           state.HasPendingSameMapTransfer,
                           state.HasBlockingScriptedSequence));
        }

        public static bool CanAdmitQueuedRetryInterfaceGate(PassiveTransferFieldInterfaceGateState state)
        {
            return state.HasLiveFieldInterface
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
            // `TryPassiveTransferField` clears pending ownership only after it can admit the
            // interface gate again. Keep pending while readiness is unresolved, even if map
            // transitions or local owner bindings (player bound/active) are transiently
            // unavailable in that window.
            return !state.HasReadyFieldInterface
                   || state.HasPendingMapChange
                   || !state.HasBoundPlayer
                   || !state.IsPlayerActive
                   || !state.HasLiveFieldInterface;
        }

        public static QueuedRetryDecision EvaluateQueuedRetryDecision(PassiveTransferFieldQueuedRetryDecisionState state)
        {
            if (!state.HasPendingRequest)
            {
                return QueuedRetryDecision.Clear;
            }

            if (!state.HasOneTimeActionCompleted)
            {
                // `TryPassiveTransferField` keeps the pending owner armed while one-time actions
                // are still active. Admission/clear only occurs after one-time completion.
                return QueuedRetryDecision.KeepPending;
            }

            bool shouldKeepPending = ShouldKeepQueuedRetryPending(
                new PassiveTransferFieldQueuedRetryState(
                    state.HasLiveFieldInterface,
                    state.HasPendingMapChange,
                    state.HasBoundPlayer,
                    state.IsPlayerActive,
                    state.HasReadyFieldInterface));

            if (shouldKeepPending)
            {
                return QueuedRetryDecision.KeepPending;
            }

            // Once one-time ownership is complete and interface admission is recovered,
            // the queued path must replay through HandleUpKeyDown so the pending owner is
            // consumed by the same admission seam used by TryPassiveTransferField.
            return QueuedRetryDecision.ReplayHandleUpKeyDown;
        }

        public static bool ShouldClearQueuedRetryAfterInterfaceGateAdmission(
            bool hasPendingRequest,
            QueuedRetryDecision decision)
        {
            return hasPendingRequest
                   && decision == QueuedRetryDecision.ReplayHandleUpKeyDown;
        }

        public static bool ShouldClearQueuedRetryFromTransferLifecycle(bool hasPendingRequest)
        {
            return hasPendingRequest;
        }

        public static bool ShouldCancelQueuedRetryOnHorizontalKeyDown(
            bool hasPendingRequest,
            bool leftKeyPressed,
            bool rightKeyPressed)
        {
            return hasPendingRequest && (leftKeyPressed || rightKeyPressed);
        }

        public static bool ShouldStopSkillMacroForHorizontalQueuedCancel(bool shouldCancelQueuedRetry)
        {
            return shouldCancelQueuedRetry;
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
            bool hasPassiveTransferFieldPortalCollision,
            bool allowsTransferField)
        {
            return hasClientOwnedOneTimeAction
                   && hasPassiveTransferFieldPortalCollision
                   && allowsTransferField;
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
