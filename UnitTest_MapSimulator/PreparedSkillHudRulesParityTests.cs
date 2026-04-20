using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.UI;
using Xunit;

namespace UnitTest_MapSimulator;

public class PreparedSkillHudRulesParityTests
{
    [Theory]
    [InlineData(2121001, 1000)]
    [InlineData(2221001, 1000)]
    [InlineData(2321001, 1000)]
    [InlineData(3121004, 2000)]
    [InlineData(3221001, 900)]
    [InlineData(4341002, 600)]
    [InlineData(4341003, 1200)]
    [InlineData(5101004, 1000)]
    [InlineData(5201002, 1000)]
    [InlineData(13111002, 1000)]
    [InlineData(15101003, 1000)]
    [InlineData(22121000, 500)]
    [InlineData(22151001, 500)]
    [InlineData(33101005, 900)]
    [InlineData(33121009, 2000)]
    [InlineData(35001001, 2000)]
    [InlineData(35101009, 2000)]
    [InlineData(5221004, 2000)]
    public void ResolvePreparedGaugeDuration_ClientKeydownTable_UsesClientGaugeProfile(
        int skillId,
        int expectedGaugeDurationMs)
    {
        int resolved = PreparedSkillHudRules.ResolvePreparedGaugeDuration(
            skillId,
            explicitGaugeDurationMs: 0,
            preparedDurationMs: 7777);

        Assert.Equal(expectedGaugeDurationMs, resolved);
    }

    [Theory]
    [InlineData(23121000)]
    [InlineData(24121000)]
    [InlineData(24121005)]
    [InlineData(31001000)]
    [InlineData(31101000)]
    [InlineData(31111005)]
    [InlineData(5721001)]
    public void ResolveKeyDownSkillState_AuthoredWithoutClientLayer_StaysFalse(int skillId)
    {
        bool resolved = PreparedSkillHudRules.ResolveKeyDownSkillState(skillId, isKeydownSkill: false);
        Assert.False(resolved);
    }

    [Theory]
    [InlineData(2111002)]
    [InlineData(4211001)]
    public void ResolvePreparedGaugeDuration_ReleaseOwnedNonKeydown_UsesPreparedDuration(int skillId)
    {
        int resolved = PreparedSkillHudRules.ResolvePreparedGaugeDuration(
            skillId,
            explicitGaugeDurationMs: 0,
            preparedDurationMs: 1340);

        Assert.Equal(1340, resolved);
    }

    [Fact]
    public void BuildStatusText_ReleaseArmed_NoGaugeWindow_ReturnsReleaseCaption()
    {
        var prepared = new StatusBarPreparedSkillRenderData
        {
            TextVariant = PreparedSkillHudTextVariant.ReleaseArmed,
            RemainingMs = 0,
            DurationMs = 0,
            GaugeDurationMs = 0,
            Progress = 0f
        };

        string caption = PreparedSkillHudTextResolver.BuildStatusText(prepared, gaugeDurationMs: 0, progress: 0f);
        Assert.Equal("Release", caption);
    }
}
