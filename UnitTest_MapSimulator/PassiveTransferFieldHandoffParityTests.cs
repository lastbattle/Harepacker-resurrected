using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure.Data;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class PassiveTransferFieldHandoffParityTests
    {
        [Fact]
        public void CanRetryFromLiveFieldInterface_ReturnsFalse_DuringPacketOwnedTeleportRegistrationCooldown()
        {
            var state = new PassiveTransferFieldInterfaceState(
                HasLiveFieldInterface: true,
                HasCollidingTransferPortal: true,
                HasActiveVectorControl: true,
                HasPendingMapChange: false,
                HasPlayerInputControl: true,
                HasStandAloneControlOwner: false,
                AllowsTransferField: true,
                HasPendingSpecialTransfer: false,
                HasPendingPacketOwnedTransfer: false,
                HasPacketOwnedTeleportRegistrationCoolingDown: true,
                HasPendingExclusiveTransferRequest: false,
                HasAttachedPacketOwnedDriver: false,
                HasPendingSameMapTransfer: false,
                HasBlockingScriptedSequence: false);

            bool canRetry = PassiveTransferFieldReadinessEvaluator.CanRetryFromLiveFieldInterface(state);

            Assert.False(canRetry);
        }

        [Fact]
        public void EvaluateQueuedRetryDecision_KeepsPending_WhenOneTimeCompletedBeforeInterfaceReadinessRecovers()
        {
            var state = new PassiveTransferFieldQueuedRetryDecisionState(
                HasPendingRequest: true,
                HasOneTimeActionCompleted: true,
                HasReadyFieldInterface: false,
                HasCollidingTransferPortal: false,
                HasLiveFieldInterface: true,
                HasPendingMapChange: false,
                HasBoundPlayer: true,
                IsPlayerActive: true);

            PassiveTransferFieldReadinessEvaluator.QueuedRetryDecision decision =
                PassiveTransferFieldReadinessEvaluator.EvaluateQueuedRetryDecision(state);

            Assert.Equal(PassiveTransferFieldReadinessEvaluator.QueuedRetryDecision.KeepPending, decision);
        }

        [Fact]
        public void EvaluateQueuedRetryDecision_Replays_WhenOneTimeCompletedAndTransferPortalCollisionReturns()
        {
            var state = new PassiveTransferFieldQueuedRetryDecisionState(
                HasPendingRequest: true,
                HasOneTimeActionCompleted: true,
                HasReadyFieldInterface: true,
                HasCollidingTransferPortal: true,
                HasLiveFieldInterface: true,
                HasPendingMapChange: false,
                HasBoundPlayer: true,
                IsPlayerActive: true);

            PassiveTransferFieldReadinessEvaluator.QueuedRetryDecision decision =
                PassiveTransferFieldReadinessEvaluator.EvaluateQueuedRetryDecision(state);

            Assert.Equal(PassiveTransferFieldReadinessEvaluator.QueuedRetryDecision.ReplayHandleUpKeyDown, decision);
        }

        [Fact]
        public void EvaluateQueuedRetryDecision_Clears_WhenOneTimeCompletedAndPortalCollisionIsGoneAtReadiness()
        {
            var state = new PassiveTransferFieldQueuedRetryDecisionState(
                HasPendingRequest: true,
                HasOneTimeActionCompleted: true,
                HasReadyFieldInterface: true,
                HasCollidingTransferPortal: false,
                HasLiveFieldInterface: true,
                HasPendingMapChange: false,
                HasBoundPlayer: true,
                IsPlayerActive: true);

            PassiveTransferFieldReadinessEvaluator.QueuedRetryDecision decision =
                PassiveTransferFieldReadinessEvaluator.EvaluateQueuedRetryDecision(state);

            Assert.Equal(PassiveTransferFieldReadinessEvaluator.QueuedRetryDecision.Clear, decision);
        }

        [Theory]
        [InlineData(1000000, 1000000, PortalType.StartPoint, false)]
        [InlineData(1000000, 1000000, PortalType.Changeable, true)]
        [InlineData(1000000, 1000001, PortalType.StartPoint, true)]
        public void ShouldSendTransferFieldRequestForPortal_RespectsSameMapAndPortalTypeSplit(
            int targetMapId,
            int currentMapId,
            PortalType portalType,
            bool expected)
        {
            bool shouldSend = MapSimulator.ShouldSendTransferFieldRequestForPortal(targetMapId, currentMapId, portalType);

            Assert.Equal(expected, shouldSend);
        }

        [Theory]
        [InlineData(1000000, true)]
        [InlineData(0, false)]
        [InlineData(999999999, false)]
        public void IsPassiveTransferFieldPortalCandidate_UsesMapOwnedTransferCandidateRules(int targetMapId, bool expected)
        {
            bool isCandidate = MapSimulator.IsPassiveTransferFieldPortalCandidate(targetMapId);

            Assert.Equal(expected, isCandidate);
        }

        [Theory]
        [InlineData(true, false, false, true)]
        [InlineData(true, false, true, true)]
        [InlineData(true, true, false, false)]
        [InlineData(false, false, false, false)]
        public void ShouldStopSkillMacroForQueuedReplay_FollowsHandleUpAdmissionOwnership(
            bool oneTimeCompleted,
            bool isImmovable,
            bool isAttractLocked,
            bool expected)
        {
            var state = new PassiveTransferFieldReplayState(
                HasOneTimeActionCompleted: oneTimeCompleted,
                IsImmovable: isImmovable,
                IsAttractLocked: isAttractLocked,
                IsOnFoothold: false);
            bool canAttemptHandleUpKeyDownReplay =
                PassiveTransferFieldReadinessEvaluator.CanAttemptHandleUpKeyDownReplay(state);
            bool shouldStopMacro =
                PassiveTransferFieldReadinessEvaluator.ShouldStopSkillMacroForQueuedReplay(canAttemptHandleUpKeyDownReplay);

            Assert.Equal(expected, shouldStopMacro);
        }

        [Theory]
        [InlineData(true, false, false, true, true)]
        [InlineData(true, false, false, false, true)]
        [InlineData(false, false, false, true, false)]
        [InlineData(true, true, false, true, false)]
        [InlineData(true, false, true, true, false)]
        public void CanAttemptHandleUpKeyDownReplay_UsesPreFootholdHandleUpAdmission(
            bool oneTimeCompleted,
            bool isImmovable,
            bool isAttractLocked,
            bool isOnFoothold,
            bool expected)
        {
            var state = new PassiveTransferFieldReplayState(
                HasOneTimeActionCompleted: oneTimeCompleted,
                IsImmovable: isImmovable,
                IsAttractLocked: isAttractLocked,
                IsOnFoothold: isOnFoothold);

            bool canAttemptHandleUpKeyDownReplay =
                PassiveTransferFieldReadinessEvaluator.CanAttemptHandleUpKeyDownReplay(state);

            Assert.Equal(expected, canAttemptHandleUpKeyDownReplay);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void HasActivePassiveTransferFieldOneTimeAction_UsesAttackOwnedAdmissionOnly(
            bool isPlayingClientOwnedOneTimeAction,
            bool expected)
        {
            bool hasActiveOneTimeAction =
                MapSimulator.HasActivePassiveTransferFieldOneTimeAction(isPlayingClientOwnedOneTimeAction);

            Assert.Equal(expected, hasActiveOneTimeAction);
        }
    }
}
