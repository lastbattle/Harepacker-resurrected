using HaCreator.MapSimulator.Combat;

namespace UnitTest_MapSimulator;

public sealed class MobAttackEffectRangeParityTests
{
    [Fact]
    public void BuildRangePositionXs_PreservesDescendingLocalOrder()
    {
        List<float> positions = MobAttackSystem.BuildRangePositionXs(120f, -120f, 4, randomPos: false);

        Assert.Equal(new[] { 120f, 40f, -40f, -120f }, positions);
    }

    [Fact]
    public void BuildRangePositionXsWithSpacing_PreservesDescendingLocalOrder()
    {
        List<float> positions = MobAttackSystem.BuildRangePositionXsWithSpacing(180f, -180f, 4, 90f);

        Assert.Equal(new[] { 180f, 90f, 0f, -90f }, positions);
    }
}
