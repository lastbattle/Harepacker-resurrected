using HaCreator.MapSimulator.Effects;

namespace UnitTest_MapSimulator;

public sealed class MassacreFieldTests
{
    [Fact]
    public void IncGaugeDoesNotInventKeyAnimation()
    {
        MassacreField field = CreateField();

        field.OnMassacreIncGauge(5, 1000);

        Assert.False(field.HasKeyAnimation);
    }

    [Fact]
    public void SkillInfoDrivesOpenLoopAndCloseKeyFlow()
    {
        MassacreField field = CreateField();

        field.SetMassacreInfo(hit: 3, miss: 1, cool: 2, skill: 1, currentTimeMs: 1000);
        Assert.True(field.HasKeyAnimation);
        Assert.Equal(1, field.SkillCount);

        field.Update(1950, 0.95f);
        Assert.True(field.HasKeyAnimation);

        field.SetMassacreInfo(hit: 3, miss: 1, cool: 2, skill: 0, currentTimeMs: 2000);
        Assert.True(field.HasKeyAnimation);

        field.Update(2950, 0.95f);
        Assert.False(field.HasKeyAnimation);
        Assert.Equal(0, field.SkillCount);
    }

    [Fact]
    public void ExplicitCountPresentationDoesNotDependOnKillThresholdInference()
    {
        MassacreField field = CreateField();

        field.ShowCountEffectPresentation(stage: 4, currentTimeMs: 1000);

        Assert.True(field.HasCountEffectPresentation);
        Assert.Equal(4, field.ActiveCountEffectStage);
    }

    [Fact]
    public void DescribeStatusIncludesPacketOwnedCountBoardState()
    {
        MassacreField field = CreateField();

        field.SetMassacreInfo(hit: 12, miss: 4, cool: 6, skill: 2, currentTimeMs: 1000);

        string status = field.DescribeStatus();

        Assert.Contains("point=12/6/4/2", status);
    }

    private static MassacreField CreateField()
    {
        MassacreField field = new();
        field.Enable(910000000);
        return field;
    }
}
