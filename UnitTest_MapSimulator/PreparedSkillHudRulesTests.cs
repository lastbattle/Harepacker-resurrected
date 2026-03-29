using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public sealed class PreparedSkillHudRulesTests
{
    [Theory]
    [InlineData(2121001)]
    [InlineData(2221001)]
    [InlineData(2321001)]
    [InlineData(4341002)]
    [InlineData(4341003)]
    [InlineData(5311002)]
    [InlineData(33101005)]
    public void ReleaseTriggeredChargeSkills_UseReleaseOwnedPreparedFlow(int skillId)
    {
        Assert.True(PreparedSkillHudRules.UsesReleaseTriggeredExecution(skillId));
        Assert.Equal(PreparedSkillHudTextVariant.ReleaseArmed, PreparedSkillHudRules.ResolveTextVariant(skillId));
    }

    [Theory]
    [InlineData(5101004)]
    [InlineData(15101003)]
    [InlineData(14111006)]
    [InlineData(35121003)]
    public void NonReleaseChargeSkills_KeepExistingPreparedFlow(int skillId)
    {
        Assert.False(PreparedSkillHudRules.UsesReleaseTriggeredExecution(skillId));
    }
}
