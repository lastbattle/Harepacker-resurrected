using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class RemoteShadowPartnerParityTests
{
    [Fact]
    public void ResolveRemoteShadowPartnerObservedRawActionCode_PrefersIndexedAlertRawActionForHitFamily()
    {
        Assert.True(CharacterPart.TryGetClientRawActionCode("alert5", out int alert5RawActionCode));

        RemoteUserActor actor = CreateActor(
            baseActionName: "hit",
            baseActionRawCode: alert5RawActionCode,
            movementDrivenActionSelection: true);

        int? result = RemoteUserActorPool.ResolveRemoteShadowPartnerObservedRawActionCode(
            actor,
            PlayerState.Hit,
            "hit");

        Assert.Equal(alert5RawActionCode, result);
    }

    [Fact]
    public void ResolveRemoteShadowPartnerObservedRawActionCode_PrefersIndexedLadderRawActionForSameBranchHelperPlayback()
    {
        Assert.True(CharacterPart.TryGetClientRawActionCode("ladder2", out int ladder2RawActionCode));

        RemoteUserActor actor = CreateActor(
            baseActionName: "ladder",
            baseActionRawCode: ladder2RawActionCode,
            movementDrivenActionSelection: true);

        int? result = RemoteUserActorPool.ResolveRemoteShadowPartnerObservedRawActionCode(
            actor,
            PlayerState.Ladder,
            "ladder");

        Assert.Equal(ladder2RawActionCode, result);
    }

    [Fact]
    public void ResolveRemoteShadowPartnerObservedRawActionCode_DoesNotPreferUnrelatedMovementRawAction()
    {
        Assert.True(CharacterPart.TryGetClientRawActionCode("walk1", out int walk1RawActionCode));

        RemoteUserActor actor = CreateActor(
            baseActionName: "walk1",
            baseActionRawCode: walk1RawActionCode,
            movementDrivenActionSelection: true);
        actor.LastMoveActionRaw = 0x0E;

        int? result = RemoteUserActorPool.ResolveRemoteShadowPartnerObservedRawActionCode(
            actor,
            PlayerState.Standing,
            "stand1");

        Assert.Equal(actor.LastMoveActionRaw, result);
    }

    private static RemoteUserActor CreateActor(
        string baseActionName,
        int baseActionRawCode,
        bool movementDrivenActionSelection)
    {
        var build = new CharacterBuild
        {
            Name = "RemoteShadowPartner"
        };

        return new RemoteUserActor(
            characterId: 1,
            name: build.Name,
            build: build,
            position: Vector2.Zero,
            facingRight: true,
            actionName: baseActionName,
            sourceTag: "test",
            isVisibleInWorld: true)
        {
            BaseActionName = baseActionName,
            BaseActionRawCode = baseActionRawCode,
            MovementDrivenActionSelection = movementDrivenActionSelection,
            LastMoveActionRaw = 0
        };
    }
}
