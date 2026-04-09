using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public sealed class ClientOwnedVehicleTransformMountParityTests
{
    private const int MechanicTamingMobItemId = 1932016;

    [Theory]
    [InlineData("cyclone")]
    [InlineData("cyclone_after")]
    [InlineData("doubleJump")]
    [InlineData("mine")]
    [InlineData("swallow_loop")]
    [InlineData("lasergun")]
    public void KnownMechanicOwnerStaysLatchedForVehicleIdOnlyCurrentActions(string actionName)
    {
        CharacterPart mountPart = CreateMechanicTamingMobPart();

        Assert.True(ClientOwnedVehicleSkillClassifier.IsMechanicVehicleOwnedCurrentActionName(
            actionName,
            includeTransformStates: true));
        Assert.True(SkillManager.SupportsClientOwnedVehicleMountedStateForCurrentAction(
            mountPart,
            actionName));
        Assert.True(PlayerCharacter.ShouldKeepClientOwnedVehicleRenderOwner(
            mountPart,
            actionName));
    }

    [Theory]
    [InlineData("cyclone")]
    [InlineData("doubleJump")]
    [InlineData("mine")]
    [InlineData("swallow_loop")]
    [InlineData("lasergun")]
    public void OwnerlessVehicleIdOnlyMechanicActionsDoNotInferMountOwnership(string actionName)
    {
        Assert.Equal(
            0,
            SkillManager.ResolveClientOwnedVehicleCurrentActionMountItemId(
                actionName,
                activeSkillMountItemId: 0,
                equippedMountItemId: 0));
    }

    [Fact]
    public void UnconfirmedMechanicActionDoesNotKeepMountedState()
    {
        CharacterPart mountPart = CreateMechanicTamingMobPart();

        Assert.False(ClientOwnedVehicleSkillClassifier.IsMechanicVehicleOwnedCurrentActionName(
            "meteor",
            includeTransformStates: true));
        Assert.False(SkillManager.SupportsClientOwnedVehicleMountedStateForCurrentAction(
            mountPart,
            "meteor"));
        Assert.False(PlayerCharacter.ShouldKeepClientOwnedVehicleRenderOwner(
            mountPart,
            "meteor"));
    }

    private static CharacterPart CreateMechanicTamingMobPart()
    {
        return new CharacterPart
        {
            ItemId = MechanicTamingMobItemId,
            Type = CharacterPartType.TamingMob,
            Slot = EquipSlot.TamingMob
        };
    }
}
