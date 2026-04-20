using System.Collections.Generic;
using System.Numerics;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Pools;

namespace UnitTest_MapSimulator;

public sealed class ClientOwnedVehicleMountOwnerParityTests
{
    [Fact]
    public void ResolveMountedStateTamingMobPart_PrefersTemporaryStatOverridePart()
    {
        CharacterPart equippedMount = CreateTamingMobPart(1932000, "stand1");
        CharacterPart overrideMount = CreateTamingMobPart(1932016, "stand1");
        var actor = new RemoteUserActor(
            1,
            "owner",
            CreateBuildWithMount(equippedMount),
            Vector2.Zero,
            facingRight: true,
            actionName: "stand1",
            sourceTag: "test",
            isVisibleInWorld: true)
        {
            TemporaryStatTamingMobOverridePart = overrideMount
        };

        CharacterPart resolved = actor.ResolveMountedStateTamingMobPart();

        Assert.Same(overrideMount, resolved);
    }

    [Fact]
    public void ResolveRemoteRidingVehicleId_UsesExplicitMechanicOwnerOverDifferentEquippedMountOnSharedStand1()
    {
        CharacterPart equippedMount = CreateTamingMobPart(1932000, "stand1");
        CharacterPart explicitMechanicOwner = CreateTamingMobPart(1932016, "stand1");

        int resolved = RemoteUserActorPool.ResolveRemoteRidingVehicleIdForTesting(
            equippedMount,
            "stand1",
            mechanicMode: null,
            activeMountedRenderOwner: explicitMechanicOwner);

        Assert.Equal(1932016, resolved);
    }

    [Fact]
    public void ResolveRemoteRidingVehicleId_DoesNotOverrideWhenExplicitMechanicOwnerDoesNotAdmitAction()
    {
        CharacterPart equippedType1EventMount = CreateTamingMobPart(1932001, "comboJudgement");
        CharacterPart explicitMechanicOwner = CreateTamingMobPart(1932016, "stand1");

        int resolved = RemoteUserActorPool.ResolveRemoteRidingVehicleIdForTesting(
            equippedType1EventMount,
            "comboJudgement",
            mechanicMode: null,
            activeMountedRenderOwner: explicitMechanicOwner);

        Assert.Equal(1932001, resolved);
    }

    [Fact]
    public void ResolveRemoteRidingVehicleId_UsesExplicitJaguarOwnerOverDifferentEquippedMountOnSharedStand1()
    {
        CharacterPart equippedEventType2Mount = CreateTamingMobPart(1932004, "stand1");
        CharacterPart explicitJaguarOwner = CreateTamingMobPart(1932030, "stand1");

        int resolved = RemoteUserActorPool.ResolveRemoteRidingVehicleIdForTesting(
            equippedEventType2Mount,
            "stand1",
            mechanicMode: null,
            activeMountedRenderOwner: explicitJaguarOwner);

        Assert.Equal(1932030, resolved);
    }

    [Fact]
    public void ResolveRemoteRidingVehicleId_DoesNotOverrideWhenExplicitJaguarOwnerDoesNotAdmitAction()
    {
        CharacterPart equippedType1EventMount = CreateTamingMobPart(1932002, "comboJudgement");
        CharacterPart explicitJaguarOwner = CreateTamingMobPart(1932031, "stand1");

        int resolved = RemoteUserActorPool.ResolveRemoteRidingVehicleIdForTesting(
            equippedType1EventMount,
            "comboJudgement",
            mechanicMode: null,
            activeMountedRenderOwner: explicitJaguarOwner);

        Assert.Equal(1932002, resolved);
    }

    [Fact]
    public void ResolveMountedVehicleId_UsesExplicitEventType1OwnerOverDifferentEquippedMountOnSharedStand1()
    {
        CharacterPart equippedMount = CreateTamingMobPart(1932000, "stand1");
        CharacterPart explicitEventType1Owner = CreateTamingMobPart(1932001, "stand1");

        int resolved = FollowCharacterEligibilityResolver.ResolveMountedVehicleId(
            equippedMount,
            "stand1",
            mechanicMode: null,
            activeMountedRenderOwner: explicitEventType1Owner);

        Assert.Equal(1932001, resolved);
    }

    private static CharacterBuild CreateBuildWithMount(CharacterPart mountPart)
    {
        var build = new CharacterBuild();
        if (mountPart != null)
        {
            build.Equipment[EquipSlot.TamingMob] = mountPart;
        }

        return build;
    }

    private static CharacterPart CreateTamingMobPart(int itemId, params string[] actionNames)
    {
        var part = new CharacterPart
        {
            ItemId = itemId,
            Type = CharacterPartType.TamingMob,
            Slot = EquipSlot.TamingMob,
            Animations = new Dictionary<string, CharacterAnimation>(System.StringComparer.OrdinalIgnoreCase),
            AvailableAnimations = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        };

        if (actionNames == null)
        {
            return part;
        }

        foreach (string actionName in actionNames)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                continue;
            }

            part.Animations[actionName] = new CharacterAnimation
            {
                ActionName = actionName,
                Frames = new List<CharacterFrame> { new() }
            };
            part.AvailableAnimations.Add(actionName);
        }

        return part;
    }
}
