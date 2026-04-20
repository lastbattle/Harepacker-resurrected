using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Pools;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class ShadowPartnerAttackObservationGateParityTests
    {
        [Fact]
        public void AttackObservationGate_AttackingState_AdmittedInBothLocalAndRemote()
        {
            bool local = PlayerCharacter.ShouldUseShadowPartnerAttackObservationGateForTesting("stand1", PlayerState.Attacking);
            bool remote = RemoteUserActorPool.ShouldUseRemoteShadowPartnerAttackObservationGateForTesting("stand1", PlayerState.Attacking);

            Assert.True(local);
            Assert.True(remote);
        }

        [Fact]
        public void AttackObservationGate_AttackAliasAction_AdmittedInBothLocalAndRemote()
        {
            bool local = PlayerCharacter.ShouldUseShadowPartnerAttackObservationGateForTesting("assassination", PlayerState.Standing);
            bool remote = RemoteUserActorPool.ShouldUseRemoteShadowPartnerAttackObservationGateForTesting("assassination", PlayerState.Standing);

            Assert.True(local);
            Assert.True(remote);
        }

        [Fact]
        public void PostCreateRetryPredicate_CreateWithoutPendingQueuedAndNoHold_AdmittedInBothLocalAndRemote()
        {
            bool local = PlayerCharacter.ShouldRetryShadowPartnerAttackResolutionAfterCreateForTesting(
                currentActionName: "create2",
                pendingActionName: null,
                queuedActionName: null,
                currentActionBlockingHoldActive: false);
            bool remote = RemoteUserActorPool.ShouldRetryRemoteShadowPartnerAttackResolutionAfterCreateForTesting(
                currentActionName: "create2",
                pendingActionName: null,
                queuedActionName: null,
                currentActionBlockingHoldActive: false);

            Assert.True(local);
            Assert.True(remote);
        }

        [Fact]
        public void PostCreateRetryPredicate_CreateWithPendingOrQueuedOrHold_RejectedInBothLocalAndRemote()
        {
            Assert.False(PlayerCharacter.ShouldRetryShadowPartnerAttackResolutionAfterCreateForTesting("create2", "attack1", null, false));
            Assert.False(PlayerCharacter.ShouldRetryShadowPartnerAttackResolutionAfterCreateForTesting("create2", null, "attack1", false));
            Assert.False(PlayerCharacter.ShouldRetryShadowPartnerAttackResolutionAfterCreateForTesting("create2", null, null, true));

            Assert.False(RemoteUserActorPool.ShouldRetryRemoteShadowPartnerAttackResolutionAfterCreateForTesting("create2", "attack1", null, false));
            Assert.False(RemoteUserActorPool.ShouldRetryRemoteShadowPartnerAttackResolutionAfterCreateForTesting("create2", null, "attack1", false));
            Assert.False(RemoteUserActorPool.ShouldRetryRemoteShadowPartnerAttackResolutionAfterCreateForTesting("create2", null, null, true));
        }

        [Fact]
        public void ObservationRefresh_RawActionCodeDelta_AdmittedInBothLocalAndRemote()
        {
            bool local = PlayerCharacter.ShouldRefreshShadowPartnerObservationForTesting(
                observedPlayerActionName: "assassination",
                observedFloatingState: false,
                observedFacingRight: true,
                observedActionTriggerTime: 1000,
                observedRawActionCode: 161,
                previousObservedPlayerActionName: "assassination",
                previousObservedFloatingState: false,
                previousObservedFacingRight: true,
                previousObservedActionTriggerTime: 1000,
                previousObservedRawActionCode: 150);
            bool remote = RemoteUserActorPool.ShouldRefreshRemoteShadowPartnerObservationForTesting(
                observedPlayerActionName: "assassination",
                observedState: PlayerState.Attacking,
                observedFacingRight: true,
                observedRawActionCode: 161,
                observedActionTriggerTime: 1000,
                previousObservedPlayerActionName: "assassination",
                previousObservedState: PlayerState.Attacking,
                previousObservedFacingRight: true,
                previousObservedRawActionCode: 150,
                previousObservedActionTriggerTime: 1000);

            Assert.True(local);
            Assert.True(remote);
        }

        [Fact]
        public void ForcedReplayTrigger_FirstObservedTrigger_AdmittedInBothLocalAndRemote()
        {
            bool local = PlayerCharacter.ShouldForceShadowPartnerAttackReplayForTriggerForTesting(
                observedActionTriggerTime: 1234,
                previousReplayTriggerTime: int.MinValue);
            bool remote = RemoteUserActorPool.ShouldForceRemoteShadowPartnerAttackReplayForTriggerForTesting(
                observedActionTriggerTime: 1234,
                previousReplayTriggerTime: int.MinValue);

            Assert.True(local);
            Assert.True(remote);
        }

        [Fact]
        public void ForcedReplayTrigger_AlreadyConsumedTrigger_RejectedInBothLocalAndRemote()
        {
            bool local = PlayerCharacter.ShouldForceShadowPartnerAttackReplayForTriggerForTesting(
                observedActionTriggerTime: 1234,
                previousReplayTriggerTime: 1234);
            bool remote = RemoteUserActorPool.ShouldForceRemoteShadowPartnerAttackReplayForTriggerForTesting(
                observedActionTriggerTime: 1234,
                previousReplayTriggerTime: 1234);

            Assert.False(local);
            Assert.False(remote);
        }

        [Fact]
        public void ForcedReplayTrigger_MissingTriggerTimestamp_RejectedInBothLocalAndRemote()
        {
            bool local = PlayerCharacter.ShouldForceShadowPartnerAttackReplayForTriggerForTesting(
                observedActionTriggerTime: int.MinValue,
                previousReplayTriggerTime: int.MinValue);
            bool remote = RemoteUserActorPool.ShouldForceRemoteShadowPartnerAttackReplayForTriggerForTesting(
                observedActionTriggerTime: int.MinValue,
                previousReplayTriggerTime: int.MinValue);

            Assert.False(local);
            Assert.False(remote);
        }
    }
}
