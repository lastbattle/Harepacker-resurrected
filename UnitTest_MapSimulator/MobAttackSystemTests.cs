using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Combat;

namespace UnitTest_MapSimulator;

public sealed class MobAttackSystemTests
{
    [Fact]
    public void BuildRangeSlotOffsets_UsesPerLaneRangeWidthForBoundedPatterns()
    {
        var attack = new MobAttackEntry
        {
            HasRangeBounds = true,
            RangeLeft = -120,
            RangeRight = 120,
            AreaWidth = 240,
            StartOffset = -7
        };

        List<float> offsets = MobAttackSystem.BuildRangeSlotOffsets(attack, 3);

        Assert.Equal(new[] { -1680f, -1440f, -1200f }, offsets);
    }

    [Fact]
    public void BuildRangeSlotOffsets_PreservesSingleSlotStartOffset()
    {
        var attack = new MobAttackEntry
        {
            HasRangeBounds = true,
            RangeLeft = -230,
            RangeRight = -110,
            AreaWidth = 120,
            StartOffset = -6
        };

        List<float> offsets = MobAttackSystem.BuildRangeSlotOffsets(attack, 1);

        Assert.Equal(new[] { -720f }, offsets);
    }
}
