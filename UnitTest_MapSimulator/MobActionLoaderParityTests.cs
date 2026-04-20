using System.Collections.Generic;
using HaCreator.MapSimulator.Loaders;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class MobActionLoaderParityTests
{
    [Fact]
    public void MobClientActionTable_ExposesExpectedCanonicalSlots()
    {
        IReadOnlyList<string> table = LifeLoader.GetMobClientActionNamesBySlotForTests();

        Assert.Equal(30, table.Count);
        Assert.Equal("stand", table[0]);
        Assert.Equal("move", table[1]);
        Assert.Equal("fly", table[2]);
        Assert.Equal("attack1", table[6]);
        Assert.Equal("attack8", table[13]);
        Assert.Equal("skill1", table[14]);
        Assert.Equal("skill16", table[29]);
    }

    [Fact]
    public void MobClientActionTable_ResolvesKnownActionSlots()
    {
        Assert.True(LifeLoader.TryResolveMobClientActionSlot("stand", out int standSlot));
        Assert.Equal(0, standSlot);

        Assert.True(LifeLoader.TryResolveMobClientActionSlot("attack1", out int attack1Slot));
        Assert.Equal(6, attack1Slot);

        Assert.True(LifeLoader.TryResolveMobClientActionSlot("Skill16", out int skill16Slot));
        Assert.Equal(29, skill16Slot);

        Assert.Equal("stand", LifeLoader.ResolveMobClientActionName(standSlot));
        Assert.Equal("attack1", LifeLoader.ResolveMobClientActionName(attack1Slot));
        Assert.Equal("skill16", LifeLoader.ResolveMobClientActionName(skill16Slot));
    }

    [Fact]
    public void MobClientActionTable_KeepsUnknownActionsUnmapped()
    {
        Assert.False(LifeLoader.TryResolveMobClientActionSlot("angergaugeeffect", out int slot));
        Assert.Equal(-1, slot);
        Assert.Null(LifeLoader.ResolveMobClientActionName(-1));
        Assert.Null(LifeLoader.ResolveMobClientActionName(999));
    }
}
