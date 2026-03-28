using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator;

public class CookieHouseFieldTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(89, 0)]
    [InlineData(90, 1)]
    [InlineData(159, 1)]
    [InlineData(160, 2)]
    [InlineData(229, 2)]
    [InlineData(230, 3)]
    [InlineData(349, 3)]
    [InlineData(350, 4)]
    [InlineData(999, 4)]
    public void OnPointUpdate_UsesRecoveredClientGradeThresholds(int point, int expectedGradeIndex)
    {
        CookieHouseField field = new();

        field.Enable(109080000);
        field.OnPointUpdate(point);

        Assert.Equal(point, field.Point);
        Assert.Equal(expectedGradeIndex, field.GradeIndex);
    }

    [Fact]
    public void Update_PollsPointProviderInsteadOfMutatingIndependentState()
    {
        int sourcePoint = 89;
        CookieHouseField field = new();

        field.Enable(109080000, () => sourcePoint);
        Assert.Equal(0, field.GradeIndex);

        sourcePoint = 230;
        field.Update();

        Assert.Equal(230, field.Point);
        Assert.Equal(3, field.GradeIndex);
    }

    [Fact]
    public void DescribeStatus_ReportsResolvedClientGradeThresholds()
    {
        CookieHouseField field = new();
        field.Enable(109080000);
        field.OnPointUpdate(350);

        string status = field.DescribeStatus();

        Assert.Contains("grade=5/5", status);
        Assert.Contains("thresholds=s_anGrade[90,160,230,350]", status);
    }
}
