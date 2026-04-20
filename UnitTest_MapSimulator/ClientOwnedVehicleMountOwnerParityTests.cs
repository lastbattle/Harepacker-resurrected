using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator;

public class ClientOwnedVehicleMountOwnerParityTests
{
    [Fact]
    public void ResolveMountedVehicleId_MechanicModeExplicitOwner_PrefersExplicitOwnerOverDifferentClientOwnedMount()
    {
        CharacterPart equippedMountPart = CreateTamingMobPart(
            1932000,
            "stand1");

        int mountedVehicleId = FollowCharacterEligibilityResolver.ResolveMountedVehicleId(
            equippedMountPart,
            "stand1",
            mechanicMode: 35121005);

        Assert.Equal(1932016, mountedVehicleId);
    }

    [Fact]
    public void ResolveMountedVehicleId_MechanicModeExplicitOwner_DoesNotOverrideWhenExplicitOwnerDoesNotAdmitAction()
    {
        CharacterPart equippedMountPart = CreateTamingMobPart(
            1932001,
            "comboJudgement");

        int mountedVehicleId = FollowCharacterEligibilityResolver.ResolveMountedVehicleId(
            equippedMountPart,
            "comboJudgement",
            mechanicMode: 35121005);

        Assert.Equal(1932001, mountedVehicleId);
    }

    private static CharacterPart CreateTamingMobPart(int itemId, params string[] actions)
    {
        var part = new CharacterPart
        {
            Type = CharacterPartType.TamingMob,
            Slot = EquipSlot.TamingMob,
            ItemId = itemId
        };

        foreach (string action in actions)
        {
            part.Animations[action] = new CharacterAnimation
            {
                ActionName = action,
                Frames =
                {
                    new CharacterFrame()
                }
            };
        }

        return part;
    }
}
