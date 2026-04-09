using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator;

public sealed class GuildSkillParityTests
{
    [Fact]
    public void BuildSnapshot_FormatsGuildPointsSeparatelyFromGuildFund()
    {
        GuildSkillRuntime runtime = new();
        runtime.UpdateContext(new GuildSkillUiContext(
            HasGuildMembership: true,
            GuildName: "Codex",
            GuildLevel: 15,
            GuildRoleLabel: "Master",
            CanManageSkills: true,
            GuildPoints: 1234));

        SkillDisplayData skill = new()
        {
            SkillId = 91000000,
            SkillName = "Banner of Nimbleness",
            CurrentLevel = 1,
            MaxLevel = 5
        };
        skill.LevelDescriptions[1] = "Accuracy +2%, Avoidability +2%";
        skill.GuildActivationCosts[2] = 320000;
        skill.GuildRenewalCosts[1] = 40000;
        skill.GuildDurationsMinutes[1] = 60;

        runtime.SetSkills(new[] { skill });

        GuildSkillSnapshot snapshot = runtime.BuildSnapshot();

        Assert.Contains("GP 1,234", snapshot.SummaryLines[2]);
        Assert.Contains("Fund 25,000,000", snapshot.SummaryLines[2]);
        Assert.DoesNotContain("GP 1,234 meso", snapshot.SummaryLines[2]);
    }

    [Fact]
    public void TooltipBuilder_ShowsGuildPointsWithoutMesoSuffix()
    {
        GuildSkillEntrySnapshot entry = new()
        {
            InGuild = true,
            SkillName = "Banner of Nimbleness",
            CurrentLevel = 1,
            MaxLevel = 5,
            GuildPoints = 1234,
            GuildFundMeso = 25000000
        };

        IReadOnlyList<string> lines = GuildSkillTooltipContentBuilder.BuildLines(entry);

        Assert.Contains("Guild points: 1,234", lines);
        Assert.DoesNotContain("Guild points: 1,234 meso", lines);
        Assert.Contains("Fund: 25,000,000 meso", lines);
    }

    [Fact]
    public void TooltipBuilder_HidesGuildCurrenciesWhenPlayerHasNoGuild()
    {
        GuildSkillEntrySnapshot entry = new()
        {
            InGuild = false,
            SkillName = "Banner of Nimbleness",
            CurrentLevel = 0,
            MaxLevel = 5,
            GuildPoints = 9876,
            GuildFundMeso = 25000000
        };

        IReadOnlyList<string> lines = GuildSkillTooltipContentBuilder.BuildLines(entry);

        Assert.DoesNotContain(lines, line => line.StartsWith("Guild points:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.StartsWith("Fund:", StringComparison.Ordinal));
        Assert.Contains("State: No guild", lines);
    }
}
