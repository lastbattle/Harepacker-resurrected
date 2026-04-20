using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator
{
    public class PassiveTransferFieldHandoffParityTests
    {
        [Fact]
        public void EvaluateQueuedRetryDecision_KeepsPending_WhenOneTimeActionStillActive_AndLiveInterfaceOwnerBindingDrops()
        {
            var state = new PassiveTransferFieldQueuedRetryDecisionState(
                HasPendingRequest: true,
                HasOneTimeActionCompleted: false,
                HasReadyFieldInterface: false,
                HasCollidingTransferPortal: false,
                HasLiveFieldInterface: true,
                HasPendingMapChange: false,
                HasBoundPlayer: false,
                IsPlayerActive: false);

            var decision = PassiveTransferFieldReadinessEvaluator.EvaluateQueuedRetryDecision(state);

            Assert.Equal(PassiveTransferFieldReadinessEvaluator.QueuedRetryDecision.KeepPending, decision);
        }

        [Fact]
        public void EvaluateQueuedRetryDecision_KeepsPending_WhenOneTimeActionCompleted_ButInterfaceReadinessUnresolvedWithOwnerDrop()
        {
            var state = new PassiveTransferFieldQueuedRetryDecisionState(
                HasPendingRequest: true,
                HasOneTimeActionCompleted: true,
                HasReadyFieldInterface: false,
                HasCollidingTransferPortal: false,
                HasLiveFieldInterface: true,
                HasPendingMapChange: false,
                HasBoundPlayer: false,
                IsPlayerActive: false);

            var decision = PassiveTransferFieldReadinessEvaluator.EvaluateQueuedRetryDecision(state);

            Assert.Equal(PassiveTransferFieldReadinessEvaluator.QueuedRetryDecision.KeepPending, decision);
        }

        [Fact]
        public void EvaluateQueuedRetryDecision_ReplaysHandleUpKeyDown_WhenReadyAndOwnerBindingRetained()
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

            var decision = PassiveTransferFieldReadinessEvaluator.EvaluateQueuedRetryDecision(state);

            Assert.Equal(PassiveTransferFieldReadinessEvaluator.QueuedRetryDecision.ReplayHandleUpKeyDown, decision);
        }

        [Fact]
        public void EvaluateQueuedRetryDecision_Clears_WhenReadyButReplayOwnerCannotBeRetainedOnLiveInterface()
        {
            var state = new PassiveTransferFieldQueuedRetryDecisionState(
                HasPendingRequest: true,
                HasOneTimeActionCompleted: true,
                HasReadyFieldInterface: true,
                HasCollidingTransferPortal: false,
                HasLiveFieldInterface: true,
                HasPendingMapChange: false,
                HasBoundPlayer: false,
                IsPlayerActive: false);

            var decision = PassiveTransferFieldReadinessEvaluator.EvaluateQueuedRetryDecision(state);

            Assert.Equal(PassiveTransferFieldReadinessEvaluator.QueuedRetryDecision.Clear, decision);
        }

        [Fact]
        public void EvaluateQueuedRetryDecision_KeepsPending_WhenInterfaceAndOwnerBindingBothDrop()
        {
            var state = new PassiveTransferFieldQueuedRetryDecisionState(
                HasPendingRequest: true,
                HasOneTimeActionCompleted: true,
                HasReadyFieldInterface: false,
                HasCollidingTransferPortal: false,
                HasLiveFieldInterface: false,
                HasPendingMapChange: false,
                HasBoundPlayer: false,
                IsPlayerActive: false);

            var decision = PassiveTransferFieldReadinessEvaluator.EvaluateQueuedRetryDecision(state);

            Assert.Equal(PassiveTransferFieldReadinessEvaluator.QueuedRetryDecision.KeepPending, decision);
        }
    }
}
