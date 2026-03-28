using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator;

public sealed class GuildSkillRuntimeTests
{
    [Fact]
    public void UpdateLocalContext_WithoutGuild_SuppressesGuildSkillAffordances()
    {
        GuildSkillRuntime runtime = CreateRuntime();

        runtime.UpdateLocalContext(new CharacterBuild());

        GuildSkillSnapshot snapshot = runtime.BuildSnapshot();

        Assert.False(snapshot.InGuild);
        Assert.False(snapshot.CanRenew);
        Assert.False(snapshot.CanLevelUpSelected);
        Assert.Equal(0, snapshot.AvailablePoints);
        Assert.Equal(0, snapshot.RecommendedSkillId);
        Assert.All(snapshot.Entries, entry =>
        {
            Assert.Equal(0, entry.CurrentLevel);
            Assert.False(entry.IsRecommended);
            Assert.False(entry.CanLevelUp);
        });
        Assert.Equal("Join a guild to cycle guild skill recommendations.", runtime.RefreshRecommendation());
        Assert.Equal("Join a guild to level guild skills.", runtime.TryLevelSelectedSkill());
    }

    [Fact]
    public void UpdateLocalContext_RestoresSavedState_WhenGuildMembershipReturns()
    {
        GuildSkillRuntime runtime = CreateRuntime();
        CharacterBuild guildedBuild = new() { GuildName = "Maple GM", Level = 50 };

        runtime.UpdateLocalContext(guildedBuild);
        runtime.SelectEntry(0);

        Assert.Equal("Guild Rush advanced to Lv. 3.", runtime.TryLevelSelectedSkill());

        runtime.UpdateLocalContext(new CharacterBuild());
        runtime.UpdateLocalContext(guildedBuild);

        GuildSkillSnapshot snapshot = runtime.BuildSnapshot();

        Assert.True(snapshot.InGuild);
        Assert.True(snapshot.CanRenew);
        Assert.Equal(1, snapshot.AvailablePoints);
        Assert.Equal(3, snapshot.Entries[0].CurrentLevel);
        Assert.Equal(1, snapshot.Entries[1].CurrentLevel);
        Assert.Contains(snapshot.Entries, entry => entry.IsRecommended);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Maple Union", true)]
    public void HasGuildMembership_RequiresRealGuildName(string guildName, bool expected)
    {
        CharacterBuild build = guildName == null ? null : new CharacterBuild { GuildName = guildName };
        Assert.Equal(expected, GuildSkillRuntime.HasGuildMembership(build));
    }

    private static GuildSkillRuntime CreateRuntime()
    {
        GuildSkillRuntime runtime = new();
        runtime.SetSkills(
        [
            new SkillDisplayData
            {
                SkillId = 91000000,
                SkillName = "Guild Rush",
                Description = "Boosts guild movement speed.",
                MaxLevel = 5
            },
            new SkillDisplayData
            {
                SkillId = 91000001,
                SkillName = "Guild Guard",
                Description = "Boosts guild defense.",
                MaxLevel = 5
            }
        ]);

        return runtime;
    }
}
