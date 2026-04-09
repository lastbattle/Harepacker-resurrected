using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator;

public sealed class PreparedSkillHudParityTests
{
    [Theory]
    [InlineData(2121001, 0, 0, 1000)]
    [InlineData(2221001, 0, 0, 1000)]
    [InlineData(2321001, 0, 0, 1000)]
    [InlineData(3221001, 0, 0, 900)]
    [InlineData(4341002, 0, 0, 600)]
    [InlineData(4341003, 0, 0, 1200)]
    [InlineData(22121000, 0, 0, 500)]
    [InlineData(22151001, 0, 0, 500)]
    [InlineData(33101005, 0, 0, 900)]
    [InlineData(5221004, 0, 0, 2000)]
    [InlineData(5311002, 0, 0, 1080)]
    [InlineData(9999999, 0, 0, 2000)]
    [InlineData(5311002, 777, 1080, 777)]
    public void ResolvePreparedGaugeDuration_MatchesSupportedGaugeTable(
        int skillId,
        int explicitGaugeDurationMs,
        int preparedDurationMs,
        int expectedGaugeDurationMs)
    {
        int gaugeDurationMs = PreparedSkillHudRules.ResolvePreparedGaugeDuration(
            skillId,
            explicitGaugeDurationMs,
            preparedDurationMs);

        Assert.Equal(expectedGaugeDurationMs, gaugeDurationMs);
    }

    [Theory]
    [InlineData(5101004, false)]
    [InlineData(5221004, false)]
    [InlineData(3221001, false)]
    [InlineData(22121000, false)]
    [InlineData(22151001, false)]
    [InlineData(33101005, false)]
    [InlineData(1234567, true)]
    public void ResolveKeyDownSkillState_NormalizesSupportedFamilies(int skillId, bool inputState)
    {
        bool normalized = PreparedSkillHudRules.ResolveKeyDownSkillState(skillId, inputState);

        Assert.Equal(
            inputState || PreparedSkillHudRules.IsSupportedKeyDownSkill(skillId) || PreparedSkillHudRules.UsesReleaseTriggeredExecution(skillId),
            normalized);
    }

    [Fact]
    public void ResolveDisplayName_UsesExplicitTrimmedNameBeforeFallback()
    {
        string displayName = PreparedSkillHudRules.ResolveDisplayName(3221001, "  Piercing Arrow  ");

        Assert.Equal("Piercing Arrow", displayName);
    }

    [Fact]
    public void ResolveDisplayName_FallsBackToSkillIdWhenStringPoolIsUnavailable()
    {
        string displayName = PreparedSkillHudRules.ResolveDisplayName(9999999, null);

        Assert.Equal("Skill 9999999", displayName);
    }

    [Fact]
    public void BuildStatusText_UsesPreparingCaptionAfterGaugeFillBeforeActualReadyPoint()
    {
        StatusBarPreparedSkillRenderData preparedSkill = new()
        {
            DurationMs = 3000,
            RemainingMs = 1500
        };

        string statusText = PreparedSkillHudTextResolver.BuildStatusText(preparedSkill, 1000, 1f);

        Assert.Equal("Preparing 2 sec", statusText);
    }

    [Fact]
    public void BuildStatusText_UsesReleaseCaptionForReleaseArmedHoldState()
    {
        StatusBarPreparedSkillRenderData preparedSkill = new()
        {
            IsHolding = true,
            TextVariant = PreparedSkillHudTextVariant.ReleaseArmed
        };

        string statusText = PreparedSkillHudTextResolver.BuildStatusText(preparedSkill, 600, 1f);

        Assert.Equal("Release", statusText);
    }

    [Fact]
    public void BuildStatusText_UsesAmplifyingCaptionWhileGaugeIsFilling()
    {
        StatusBarPreparedSkillRenderData preparedSkill = new()
        {
            TextVariant = PreparedSkillHudTextVariant.Amplify
        };

        string statusText = PreparedSkillHudTextResolver.BuildStatusText(preparedSkill, 2000, 0.42f);

        Assert.Equal("Amplifying 42%", statusText);
    }

    [Fact]
    public void BuildStatusText_UsesAmplifiedCaptionWhenAmplifyGaugeCompletes()
    {
        StatusBarPreparedSkillRenderData preparedSkill = new()
        {
            TextVariant = PreparedSkillHudTextVariant.Amplify
        };

        string statusText = PreparedSkillHudTextResolver.BuildStatusText(preparedSkill, 2000, 1f);

        Assert.Equal("Amplified", statusText);
    }

    [Fact]
    public void BuildStatusText_UsesMaintainingCaptionWhileHoldTimerIsRunning()
    {
        StatusBarPreparedSkillRenderData preparedSkill = new()
        {
            IsHolding = true,
            IsKeydownSkill = true,
            MaxHoldDurationMs = 3000,
            HoldElapsedMs = 1200
        };

        string statusText = PreparedSkillHudTextResolver.BuildStatusText(preparedSkill, 900, 0.6f);

        Assert.Equal("Maintaining 2 sec", statusText);
    }

    [Fact]
    public void ResolveProgress_UsesHoldRemainingWhileHolding()
    {
        StatusBarPreparedSkillRenderData preparedSkill = new()
        {
            IsHolding = true,
            MaxHoldDurationMs = 4000,
            HoldElapsedMs = 1000
        };

        float progress = PreparedSkillHudTextResolver.ResolveProgress(preparedSkill, 900);

        Assert.Equal(0.75f, progress, 3);
    }
}
