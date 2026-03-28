using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Entities;
using MapleLib.WzLib.WzStructure.Data.MobStructure;

namespace UnitTest_MapSimulator;

public class SpecialMobInteractionRulesTests
{
    [Theory]
    [InlineData(MobDeathType.Bomb)]
    [InlineData(MobDeathType.Miss)]
    [InlineData(MobDeathType.Swallowed)]
    [InlineData(MobDeathType.Timeout)]
    public void ShouldSuppressRewardDrops_ReturnsTrue_ForSpecialDeathTypes(MobDeathType deathType)
    {
        bool suppress = SpecialMobInteractionRules.ShouldSuppressRewardDrops(new MobData(), deathType);

        Assert.True(suppress);
    }

    [Fact]
    public void ShouldSuppressRewardDrops_ReturnsTrue_ForEscortAndDamagedByMobKills()
    {
        Assert.True(SpecialMobInteractionRules.ShouldSuppressRewardDrops(new MobData { Escort = 1 }, MobDeathType.Killed));
        Assert.True(SpecialMobInteractionRules.ShouldSuppressRewardDrops(new MobData { DamagedByMob = true }, MobDeathType.Killed));
    }

    [Fact]
    public void ShouldDisableAutoRespawn_ReturnsTrue_ForEncounterMobFlags()
    {
        Assert.True(SpecialMobInteractionRules.ShouldDisableAutoRespawn(new MobData { Escort = 1 }));
        Assert.True(SpecialMobInteractionRules.ShouldDisableAutoRespawn(new MobData { DamagedByMob = true }));
        Assert.True(SpecialMobInteractionRules.ShouldDisableAutoRespawn(new MobData { RemoveAfter = 15 }));
    }

    [Fact]
    public void ShouldDisableAutoRespawn_ReturnsFalse_ForRegularMob()
    {
        bool disableRespawn = SpecialMobInteractionRules.ShouldDisableAutoRespawn(new MobData());

        Assert.False(disableRespawn);
    }
}
