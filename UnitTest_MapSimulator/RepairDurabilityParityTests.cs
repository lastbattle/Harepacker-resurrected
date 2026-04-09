using HaCreator.MapSimulator;

namespace UnitTest_MapSimulator;

public sealed class RepairDurabilityParityTests
{
    [Fact]
    public void ResolveRequiredJobBadgeStates_ZeroMaskTreatsEquipAsAllClass()
    {
        IReadOnlyList<(string Key, bool Enabled)> states = RepairDurabilityClientParity.ResolveRequiredJobBadgeStates(0);

        Assert.Equal(6, states.Count);
        Assert.Equal("beginner", states[0].Key);
        Assert.Equal("pirate", states[^1].Key);
        Assert.All(states, state => Assert.True(state.Enabled));
    }

    [Fact]
    public void ResolveRequiredJobBadgeStates_PreservesClientBadgeOrderAndDisabledStates()
    {
        IReadOnlyList<(string Key, bool Enabled)> states = RepairDurabilityClientParity.ResolveRequiredJobBadgeStates(2 | 8);

        Assert.Equal(
            new[]
            {
                ("beginner", false),
                ("warrior", true),
                ("magician", false),
                ("bowman", true),
                ("thief", false),
                ("pirate", false)
            },
            states);
    }
}
