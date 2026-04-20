using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator;

public class PassiveTransferFieldHandoffParityTests
{
    [Fact]
    public void EvaluateQueuedRetryDecision_KeepsPending_WhenInterfaceDropsAndOneTimeActionStillActive()
    {
        var decision = PassiveTransferFieldReadinessEvaluator.EvaluateQueuedRetryDecision(
            new PassiveTransferFieldQueuedRetryDecisionState(
                HasPendingRequest: true,
                HasOneTimeActionCompleted: false,
                HasReadyFieldInterface: false,
                HasCollidingTransferPortal: false,
                HasLiveFieldInterface: false,
                HasPendingMapChange: false,
                HasBoundPlayer: true,
                IsPlayerActive: true));

        Assert.Equal(PassiveTransferFieldReadinessEvaluator.QueuedRetryDecision.KeepPending, decision);
    }

    [Fact]
    public void EvaluateQueuedRetryDecision_KeepsPending_WhenOneTimeActionCompletesBeforeInterfaceReadinessRecovers()
    {
        var decision = PassiveTransferFieldReadinessEvaluator.EvaluateQueuedRetryDecision(
            new PassiveTransferFieldQueuedRetryDecisionState(
                HasPendingRequest: true,
                HasOneTimeActionCompleted: true,
                HasReadyFieldInterface: false,
                HasCollidingTransferPortal: false,
                HasLiveFieldInterface: false,
                HasPendingMapChange: false,
                HasBoundPlayer: true,
                IsPlayerActive: true));

        Assert.Equal(PassiveTransferFieldReadinessEvaluator.QueuedRetryDecision.KeepPending, decision);
    }

    [Fact]
    public void EvaluateQueuedRetryDecision_ReplaysWhenInterfaceReadinessRecoversWithoutCurrentPortalCollision()
    {
        var decision = PassiveTransferFieldReadinessEvaluator.EvaluateQueuedRetryDecision(
            new PassiveTransferFieldQueuedRetryDecisionState(
                HasPendingRequest: true,
                HasOneTimeActionCompleted: true,
                HasReadyFieldInterface: true,
                HasCollidingTransferPortal: false,
                HasLiveFieldInterface: true,
                HasPendingMapChange: false,
                HasBoundPlayer: true,
                IsPlayerActive: true));

        Assert.Equal(PassiveTransferFieldReadinessEvaluator.QueuedRetryDecision.ReplayHandleUpKeyDown, decision);
    }

    [Fact]
    public void EvaluateQueuedRetryDecision_ClearsWhenInterfaceReadinessRecoversButOwnerCannotKeepPending()
    {
        var decision = PassiveTransferFieldReadinessEvaluator.EvaluateQueuedRetryDecision(
            new PassiveTransferFieldQueuedRetryDecisionState(
                HasPendingRequest: true,
                HasOneTimeActionCompleted: true,
                HasReadyFieldInterface: true,
                HasCollidingTransferPortal: false,
                HasLiveFieldInterface: true,
                HasPendingMapChange: false,
                HasBoundPlayer: false,
                IsPlayerActive: false));

        Assert.Equal(PassiveTransferFieldReadinessEvaluator.QueuedRetryDecision.Clear, decision);
    }

    [Fact]
    public void ShouldKeepQueuedRetryPending_DropsWhenMapChangeIsPending()
    {
        bool shouldKeepPending = PassiveTransferFieldReadinessEvaluator.ShouldKeepQueuedRetryPending(
            new PassiveTransferFieldQueuedRetryState(
                HasLiveFieldInterface: true,
                HasPendingMapChange: true,
                HasBoundPlayer: true,
                IsPlayerActive: true));

        Assert.False(shouldKeepPending);
    }
}
