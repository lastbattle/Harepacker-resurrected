using HaCreator.MapSimulator;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class PacketOwnedSkillApplicationParityTests
{
    [Fact]
    public void CollectPacketOwnedTimeBombInvincibilityOptionIds_FiltersToWzBackedInvincibilityFamily()
    {
        (int OptionId, int OptionType, string DisplayTemplate)[] candidates =
        {
            (20366, 52, "Invincible for #time more seconds after getting attacked."),
            (30366, 52, "Invincible for #time more seconds after getting attacked."),
            (40366, 52, "Invincible for #time more seconds after getting attacked."),
            (30371, 52, "#prop% chance to become invincible for #time seconds when attacked."),
            (40371, 52, "#prop% chance to become invincible for #time seconds when attacked."),
            (20366, 52, "Invincible for #time more seconds after getting attacked."),
            (99999, 51, "Invincible for #time more seconds after getting attacked."),
            (88888, 52, "Restores HP when attacked.")
        };

        IReadOnlyList<int> result = MapSimulator.CollectPacketOwnedTimeBombInvincibilityOptionIds(candidates);

        Assert.Equal(new[] { 20366, 30366, 30371, 40366, 40371 }, result);
    }

    [Theory]
    [InlineData("Invincible for #time more seconds after getting attacked.", true)]
    [InlineData("#prop% chance to become invincible for #time seconds when attacked.", true)]
    [InlineData("Restores HP when attacked.", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPacketOwnedTimeBombInvincibilityDisplayTemplate_MatchesOnlyClientRelevantTemplates(string template, bool expected)
    {
        bool result = MapSimulator.IsPacketOwnedTimeBombInvincibilityDisplayTemplate(template);

        Assert.Equal(expected, result);
    }
}
