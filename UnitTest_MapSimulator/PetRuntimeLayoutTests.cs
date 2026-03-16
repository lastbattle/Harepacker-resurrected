using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Companions;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public class PetRuntimeLayoutTests
{
    [Fact]
    public void ResolveAnchorTarget_UsesGroundFollowSpacing_WhenOwnerIsStanding()
    {
        Vector2 target = PetRuntime.ResolveAnchorTarget(
            ownerX: 100f,
            ownerY: 200f,
            ownerHitbox: new Rectangle(85, 140, 30, 60),
            ownerFacingRight: true,
            ownerState: PlayerState.Standing,
            slotIndex: 1,
            activePetCount: 3,
            out bool hangOnBack,
            out bool useMultiPetHangLayout);

        Assert.False(hangOnBack);
        Assert.False(useMultiPetHangLayout);
        Assert.Equal(54f, target.X);
        Assert.Equal(200f, target.Y);
    }

    [Fact]
    public void ResolveAnchorTarget_UsesSinglePetBackAnchor_WhenOwnerIsClimbingAlone()
    {
        Vector2 target = PetRuntime.ResolveAnchorTarget(
            ownerX: 100f,
            ownerY: 200f,
            ownerHitbox: new Rectangle(85, 140, 30, 60),
            ownerFacingRight: true,
            ownerState: PlayerState.Ladder,
            slotIndex: 0,
            activePetCount: 1,
            out bool hangOnBack,
            out bool useMultiPetHangLayout);

        Assert.True(hangOnBack);
        Assert.False(useMultiPetHangLayout);
        Assert.Equal(82f, target.X);
        Assert.Equal(164f, target.Y);
    }

    [Fact]
    public void ResolveAnchorTarget_UsesMultiPetBackLayout_ForPrimaryClimbingPet()
    {
        Vector2 target = PetRuntime.ResolveAnchorTarget(
            ownerX: 100f,
            ownerY: 200f,
            ownerHitbox: new Rectangle(85, 140, 30, 60),
            ownerFacingRight: true,
            ownerState: PlayerState.Rope,
            slotIndex: 0,
            activePetCount: 2,
            out bool hangOnBack,
            out bool useMultiPetHangLayout);

        Assert.True(hangOnBack);
        Assert.True(useMultiPetHangLayout);
        Assert.Equal(90f, target.X);
        Assert.Equal(158f, target.Y);
    }

    [Fact]
    public void ResolveAnchorTarget_MirrorsBackLayout_WhenOwnerFacesLeft()
    {
        Vector2 target = PetRuntime.ResolveAnchorTarget(
            ownerX: 100f,
            ownerY: 200f,
            ownerHitbox: new Rectangle(85, 140, 30, 60),
            ownerFacingRight: false,
            ownerState: PlayerState.Ladder,
            slotIndex: 2,
            activePetCount: 3,
            out bool hangOnBack,
            out bool useMultiPetHangLayout);

        Assert.True(hangOnBack);
        Assert.False(useMultiPetHangLayout);
        Assert.Equal(136f, target.X);
        Assert.Equal(180f, target.Y);
    }
}
