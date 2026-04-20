using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;

namespace UnitTest_MapSimulator;

public sealed class ShadowPartnerAttackObservationGateParityTests
{
    [Fact]
    public void AttackObservationGate_ShouldMatchLocalAndRemote_ForAttackAndNonAttackContexts()
    {
        Assert.True(PlayerCharacter.ShouldUseShadowPartnerAttackObservationGateForTesting("stand1", PlayerState.Attacking));
        Assert.True(RemoteUserActorPool.ShouldUseRemoteShadowPartnerAttackObservationGateForTesting("stand1", PlayerState.Attacking));

        Assert.True(PlayerCharacter.ShouldUseShadowPartnerAttackObservationGateForTesting("avenger", PlayerState.Standing));
        Assert.True(RemoteUserActorPool.ShouldUseRemoteShadowPartnerAttackObservationGateForTesting("avenger", PlayerState.Standing));

        Assert.False(PlayerCharacter.ShouldUseShadowPartnerAttackObservationGateForTesting("stand1", PlayerState.Standing));
        Assert.False(RemoteUserActorPool.ShouldUseRemoteShadowPartnerAttackObservationGateForTesting("stand1", PlayerState.Standing));
    }

    [Fact]
    public void PostCreateRetryPredicate_ShouldMatchLocalAndRemote()
    {
        Assert.True(PlayerCharacter.ShouldRetryShadowPartnerAttackResolutionAfterCreateForTesting("create2", null, null, false));
        Assert.True(RemoteUserActorPool.ShouldRetryRemoteShadowPartnerAttackResolutionAfterCreateForTesting("create2", null, null, false));

        Assert.False(PlayerCharacter.ShouldRetryShadowPartnerAttackResolutionAfterCreateForTesting("create2", "avenger", null, false));
        Assert.False(RemoteUserActorPool.ShouldRetryRemoteShadowPartnerAttackResolutionAfterCreateForTesting("create2", "avenger", null, false));

        Assert.False(PlayerCharacter.ShouldRetryShadowPartnerAttackResolutionAfterCreateForTesting("create2", null, "avenger", false));
        Assert.False(RemoteUserActorPool.ShouldRetryRemoteShadowPartnerAttackResolutionAfterCreateForTesting("create2", null, "avenger", false));

        Assert.False(PlayerCharacter.ShouldRetryShadowPartnerAttackResolutionAfterCreateForTesting("create2", null, null, true));
        Assert.False(RemoteUserActorPool.ShouldRetryRemoteShadowPartnerAttackResolutionAfterCreateForTesting("create2", null, null, true));
    }

    [Fact]
    public void ObservationRefresh_ShouldAdmitRawActionDelta_ForLocalAndRemote()
    {
        Assert.True(PlayerCharacter.ShouldRefreshShadowPartnerObservationForTesting(
            observedPlayerActionName: "swingO1",
            observedFloatingState: false,
            observedFacingRight: true,
            observedActionTriggerTime: 100,
            observedRawActionCode: 56,
            previousObservedPlayerActionName: "swingO1",
            previousObservedFloatingState: false,
            previousObservedFacingRight: true,
            previousObservedActionTriggerTime: 100,
            previousObservedRawActionCode: 55));

        Assert.True(RemoteUserActorPool.ShouldRefreshRemoteShadowPartnerObservationForTesting(
            observedPlayerActionName: "swingO1",
            observedState: PlayerState.Attacking,
            observedFacingRight: true,
            observedRawActionCode: 56,
            observedActionTriggerTime: 100,
            previousObservedPlayerActionName: "swingO1",
            previousObservedState: PlayerState.Attacking,
            previousObservedFacingRight: true,
            previousObservedRawActionCode: 55,
            previousObservedActionTriggerTime: 100));
    }

    [Fact]
    public void BlockingHold_ShouldFallbackToActionCatalogPlayback_WhenCurrentPlaybackIsMissing()
    {
        var createPlayback = new SkillAnimation
        {
            Name = "create2",
            Frames = new List<SkillFrame>
            {
                new() { Delay = 120 }
            }
        };
        var actionAnimations = new Dictionary<string, SkillAnimation>(StringComparer.OrdinalIgnoreCase)
        {
            ["create2"] = createPlayback
        };

        Assert.True(ShadowPartnerClientActionResolver.ShouldHoldBlockingAction(
            "create2",
            playbackAnimation: null,
            actionAnimations,
            elapsedTimeMs: 0));

        Assert.False(ShadowPartnerClientActionResolver.ShouldHoldBlockingAction(
            "create2",
            playbackAnimation: null,
            actionAnimations,
            elapsedTimeMs: 121));
    }
}
