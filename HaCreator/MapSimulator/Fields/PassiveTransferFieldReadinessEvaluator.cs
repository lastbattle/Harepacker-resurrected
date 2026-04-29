using System;
using MapleLib.WzLib.WzStructure.Data;

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

    public readonly record struct PassiveTransferFieldHorizontalOnKeyDownDecision(
        bool ShouldStopSkillMacro,
        bool ShouldClearQueuedRetry);

    public readonly record struct PassiveTransferFieldQueuedReplayDecision(
        bool ShouldStopSkillMacro,
        bool ShouldReplayHandleUpKeyDown,
        bool ShouldClearQueuedRetry);

    public readonly record struct PassiveTransferFieldPortalRoutingDecision(
        bool IsPassiveTransferFieldPortal,
        bool ShouldSendTransferFieldRequest,
        bool ShouldPlayTransferFieldPortalSound);

    public static class PassiveTransferFieldReadinessEvaluator
    {
        public enum QueuedRetryDecision
        {
            Clear = 0,
            KeepPending = 1,
            ReplayHandleUpKeyDown = 2
        }

        public enum QueuedRetryLifecycleClearOwner
        {
            None = 0,
            MapTransferAdmission = 1,
            TransferResponseLifecycle = 2,
            FieldInterfaceTeardown = 3
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

        public static PassiveTransferFieldQueuedReplayDecision EvaluateQueuedReplayDecision(
            bool hasPendingRequest,
            QueuedRetryDecision decision,
            PassiveTransferFieldReplayState replayState)
        {
            if (decision != QueuedRetryDecision.ReplayHandleUpKeyDown)
            {
                return new PassiveTransferFieldQueuedReplayDecision(
                    ShouldStopSkillMacro: false,
                    ShouldReplayHandleUpKeyDown: false,
                    ShouldClearQueuedRetry: false);
            }

            bool canAttemptHandleUpKeyDownReplay = CanAttemptHandleUpKeyDownReplay(replayState);

            return new PassiveTransferFieldQueuedReplayDecision(
                ShouldStopSkillMacro: ShouldStopSkillMacroForQueuedReplay(canAttemptHandleUpKeyDownReplay),
                ShouldReplayHandleUpKeyDown: CanReplayHandleUpKeyDown(replayState),
                ShouldClearQueuedRetry: ShouldClearQueuedRetryAfterInterfaceGateAdmission(
                    hasPendingRequest,
                    decision));
        }

        public static bool ShouldClearQueuedRetryFromTransferLifecycle(bool hasPendingRequest)
        {
            return ShouldClearQueuedRetryFromLifecycleOwner(
                hasPendingRequest,
                QueuedRetryLifecycleClearOwner.TransferResponseLifecycle);
        }

        public static bool ShouldConsumeQueuedRetryOnMapTransferAdmission(bool hasPendingRequest)
        {
            return ShouldClearQueuedRetryFromLifecycleOwner(
                hasPendingRequest,
                QueuedRetryLifecycleClearOwner.MapTransferAdmission);
        }

        public static bool ShouldClearQueuedRetryFromFieldInterfaceTeardown(bool hasPendingRequest)
        {
            return ShouldClearQueuedRetryFromLifecycleOwner(
                hasPendingRequest,
                QueuedRetryLifecycleClearOwner.FieldInterfaceTeardown);
        }

        public static bool ShouldClearQueuedRetryFromLifecycleOwner(
            bool hasPendingRequest,
            QueuedRetryLifecycleClearOwner owner)
        {
            return hasPendingRequest
                   && owner != QueuedRetryLifecycleClearOwner.None;
        }

        public static bool ShouldClearQueuedRetryAfterFreshHandleUpKeyDown(
            bool hasPendingRequest,
            bool handledPortalInteraction)
        {
            return hasPendingRequest && handledPortalInteraction;
        }

        public static bool ShouldCancelQueuedRetryOnHorizontalKeyDown(
            bool hasPendingRequest,
            bool leftKeyPressed,
            bool rightKeyPressed)
        {
            return hasPendingRequest && (leftKeyPressed || rightKeyPressed);
        }

        public static PassiveTransferFieldHorizontalOnKeyDownDecision EvaluateHorizontalOnKeyDown(
            bool hasPendingRequest,
            bool leftKeyPressed,
            bool rightKeyPressed)
        {
            return new PassiveTransferFieldHorizontalOnKeyDownDecision(
                ShouldStopSkillMacro: ShouldStopSkillMacroForHorizontalOnKeyDown(leftKeyPressed, rightKeyPressed),
                ShouldClearQueuedRetry: ShouldCancelQueuedRetryOnHorizontalKeyDown(
                    hasPendingRequest,
                    leftKeyPressed,
                    rightKeyPressed));
        }

        public static bool ShouldStopSkillMacroForHorizontalQueuedCancel(bool shouldCancelQueuedRetry)
        {
            return shouldCancelQueuedRetry;
        }

        public static bool ShouldStopSkillMacroForHorizontalOnKeyDown(
            bool leftKeyPressed,
            bool rightKeyPressed)
        {
            return leftKeyPressed || rightKeyPressed;
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

        public static bool ShouldStopSkillMacroForFreshUpKeyDown(bool isFreshUpKeyDown)
        {
            return isFreshUpKeyDown;
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

        public static PassiveTransferFieldPortalRoutingDecision EvaluatePortalRouting(
            int targetMapId,
            int currentMapId,
            PortalType portalType)
        {
            bool isPassiveTransferFieldPortal =
                IsPassiveTransferFieldPortalType(portalType)
                && IsPassiveTransferFieldPortalCandidate(targetMapId);

            return new PassiveTransferFieldPortalRoutingDecision(
                IsPassiveTransferFieldPortal: isPassiveTransferFieldPortal,
                ShouldSendTransferFieldRequest: ShouldSendTransferFieldRequestForPortal(
                    targetMapId,
                    currentMapId,
                    portalType),
                ShouldPlayTransferFieldPortalSound: ShouldPlayTransferFieldPortalSound(portalType));
        }

        public static bool IsPassiveTransferFieldPortalType(PortalType portalType)
        {
            return portalType != PortalType.CollisionScript
                   && portalType != PortalType.CollisionVerticalJump
                   && portalType != PortalType.CollisionCustomImpact
                   && portalType != PortalType.CollisionCustomImpact2;
        }

        public static bool IsPassiveTransferFieldPortalCandidate(int targetMapId)
        {
            return targetMapId > 0
                   && targetMapId != MapConstants.MaxMap;
        }

        public static bool ShouldSendTransferFieldRequestForPortal(
            int targetMapId,
            int currentMapId,
            PortalType portalType)
        {
            return IsPassiveTransferFieldPortalCandidate(targetMapId)
                   && (targetMapId != currentMapId || IsChangeablePortalType(portalType));
        }

        public static bool ShouldPlayTransferFieldPortalSound(PortalType portalType)
        {
            return !IsChangeablePortalType(portalType);
        }

        private static bool IsChangeablePortalType(PortalType portalType)
        {
            return portalType == PortalType.Changeable
                   || portalType == PortalType.ChangeableInvisible;
        }

        public static bool ShouldArmQueuedRetryFromHandleUpKeyDown(
            bool hasPendingRequest,
            bool hasClientOwnedOneTimeAction,
            bool hasPassiveTransferFieldPortalCollision,
            bool allowsTransferField)
        {
            return !hasPendingRequest
                   && CanQueuePassiveTransferFieldRequest(
                       hasClientOwnedOneTimeAction,
                       hasPassiveTransferFieldPortalCollision,
                       allowsTransferField);
        }

        public static bool ShouldArmQueuedRetryFromFollowCharacterTransferDetach(
            bool isLocalUser,
            bool transferField)
        {
            return isLocalUser && transferField;
        }

        public static bool ShouldClearQueuedRetryFromFollowCharacterFailure(
            bool hasPendingRequest,
            bool clearsPendingFollowRequest)
        {
            return hasPendingRequest && clearsPendingFollowRequest;
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
