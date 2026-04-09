using HaCreator.MapSimulator.Character;

namespace UnitTest_MapSimulator;

public sealed class TamingMobActionFrameOwnerParityTests
{
    private const int OrdinaryMountItemId = 1902000;
    private const int MechanicTamingMobItemId = 1932016;
    private const int WildHunterJaguarItemId = 1932030;

    [Fact]
    public void TiredStandLookupsPreferTiredBranchWhenDurabilityIsExhausted()
    {
        CharacterPart mountPart = CreateMountPart(
            OrdinaryMountItemId,
            "stand1",
            "tired");
        mountPart.MaxDurability = 100;
        mountPart.Durability = 0;

        CharacterAnimation animation = mountPart.TamingMobActionFrameOwner.GetAnimation(
            mountPart,
            "stand1");

        Assert.NotNull(animation);
        Assert.Equal("tired", animation.ActionName);
    }

    [Fact]
    public void HealthyAndTiredStandResolutionUseSeparateCacheEntries()
    {
        CharacterPart mountPart = CreateMountPart(
            OrdinaryMountItemId,
            "stand1",
            "tired");
        mountPart.MaxDurability = 100;
        mountPart.Durability = 50;

        CharacterAnimation healthyAnimation = mountPart.TamingMobActionFrameOwner.GetAnimation(
            mountPart,
            "stand1");

        mountPart.Durability = 0;
        CharacterAnimation tiredAnimation = mountPart.TamingMobActionFrameOwner.GetAnimation(
            mountPart,
            "stand1");

        Assert.NotNull(healthyAnimation);
        Assert.NotNull(tiredAnimation);
        Assert.Equal("stand1", healthyAnimation.ActionName);
        Assert.Equal("tired", tiredAnimation.ActionName);
    }

    [Theory]
    [InlineData("swallow_pre")]
    [InlineData("swallow_loop")]
    [InlineData("swallow")]
    [InlineData("swallow_attack")]
    [InlineData("crossRoad")]
    [InlineData("wildbeast")]
    [InlineData("sonicBoom")]
    [InlineData("clawCut")]
    [InlineData("mine")]
    [InlineData("ride")]
    [InlineData("getoff")]
    [InlineData("proneStab_jaguar")]
    [InlineData("herbalism_jaguar")]
    [InlineData("mining_jaguar")]
    public void JaguarOnlyFamiliesRequireJaguarVehicleOwnership(string actionName)
    {
        CharacterPart ordinaryMount = CreateMountPart(OrdinaryMountItemId, actionName);
        CharacterPart jaguarMount = CreateMountPart(WildHunterJaguarItemId, actionName);

        Assert.False(ordinaryMount.TamingMobActionFrameOwner.SupportsAction(ordinaryMount, actionName));
        Assert.True(jaguarMount.TamingMobActionFrameOwner.SupportsAction(jaguarMount, actionName));
    }

    [Theory]
    [InlineData("tank_msummon")]
    [InlineData("tank_msummon2")]
    [InlineData("tank_rbooster_pre")]
    [InlineData("tank_rbooster_after")]
    [InlineData("tank_mRush")]
    [InlineData("msummon")]
    [InlineData("msummon2")]
    [InlineData("ride2")]
    [InlineData("getoff2")]
    [InlineData("mRush")]
    [InlineData("rope2")]
    [InlineData("ladder2")]
    [InlineData("herbalism_mechanic")]
    [InlineData("mining_mechanic")]
    public void MechanicOnlyFamiliesRequireMechanicVehicleOwnership(string actionName)
    {
        CharacterPart ordinaryMount = CreateMountPart(OrdinaryMountItemId, actionName);
        CharacterPart mechanicMount = CreateMountPart(MechanicTamingMobItemId, actionName);

        Assert.False(ordinaryMount.TamingMobActionFrameOwner.SupportsAction(ordinaryMount, actionName));
        Assert.True(mechanicMount.TamingMobActionFrameOwner.SupportsAction(mechanicMount, actionName));
    }

    private static CharacterPart CreateMountPart(int itemId, params string[] publishedActionNames)
    {
        var part = new CharacterPart
        {
            ItemId = itemId,
            Type = CharacterPartType.TamingMob,
            Slot = EquipSlot.TamingMob,
            AvailableAnimations = new HashSet<string>(
                publishedActionNames,
                StringComparer.OrdinalIgnoreCase),
            AnimationResolver = actionName => publishedActionNames.Contains(
                actionName,
                StringComparer.OrdinalIgnoreCase)
                ? CreateAnimation(actionName)
                : null,
            TamingMobActionFrameOwner = new TamingMobActionFrameOwner(itemId)
        };

        return part;
    }

    private static CharacterAnimation CreateAnimation(string actionName)
    {
        return new CharacterAnimation
        {
            ActionName = actionName,
            Frames = new List<CharacterFrame>
            {
                new()
            }
        };
    }
}
