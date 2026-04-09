using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using MapSimulatorRuntime = HaCreator.MapSimulator.MapSimulator;

namespace UnitTest_MapSimulator;

public sealed class AnimationDisplayerParityTests
{
    [Theory]
    [InlineData(AnimationDisplayerQuestDeliveryStringPoolText.ArriveTemplateStringPoolId, "Effect/ItemEff.img/%d/arrive")]
    [InlineData(AnimationDisplayerQuestDeliveryStringPoolText.WaitTemplateStringPoolId, "Effect/ItemEff.img/%d/wait")]
            [InlineData(AnimationDisplayerQuestDeliveryStringPoolText.LeaveTemplateStringPoolId, "Effect/ItemEff.img/%d/leave")]
    public void MapleStoryStringPool_ResolvesQuestDeliveryPhaseTemplates(int stringPoolId, string expected)
    {
        Assert.True(MapleStoryStringPool.TryGet(stringPoolId, out string text));
        Assert.Equal(expected, text);
    }

    [Fact]
    public void QuestDeliveryStringPoolText_FormatsClientPhasePaths()
    {
        const int itemId = 2430071;

        Assert.Equal("Effect/ItemEff.img/2430071/arrive", AnimationDisplayerQuestDeliveryStringPoolText.ResolveArrivePath(itemId));
        Assert.Equal("Effect/ItemEff.img/2430071/wait", AnimationDisplayerQuestDeliveryStringPoolText.ResolveWaitPath(itemId));
        Assert.Equal("Effect/ItemEff.img/2430071/leave", AnimationDisplayerQuestDeliveryStringPoolText.ResolveLeavePath(itemId));
    }

    [Fact]
    public void EnumerateQuestDeliveryEffectItemIds_PrefersRequestedThenFallback()
    {
        Assert.Equal(
            new[] { 2000001, 2430071 },
            MapSimulatorRuntime.EnumerateAnimationDisplayerQuestDeliveryEffectItemIds(2000001));
        Assert.Equal(
            new[] { 2430071 },
            MapSimulatorRuntime.EnumerateAnimationDisplayerQuestDeliveryEffectItemIds(2430071));
    }

    [Fact]
    public void ResolveQuestDeliveryPhaseRanges_UsesAuthoredPlateauAsRepeatPhase()
    {
        Point[] frameSizes =
        {
            new(23, 23),
            new(23, 23),
            new(23, 23),
            new(23, 23),
            new(24, 25),
            new(25, 26),
            new(28, 28),
            new(35, 37),
            new(40, 40),
            new(57, 57),
            new(57, 57),
            new(57, 57),
            new(57, 57),
            new(21, 30),
            new(17, 28),
            new(16, 25),
            new(7, 7),
            new(7, 7)
        };

        MapSimulatorRuntime.ResolveAnimationDisplayerQuestDeliveryPhaseRanges(
            frameSizes,
            out int startIndex,
            out int startCount,
            out int repeatIndex,
            out int repeatCount,
            out int endIndex,
            out int endCount);

        Assert.Equal(0, startIndex);
        Assert.Equal(9, startCount);
        Assert.Equal(9, repeatIndex);
        Assert.Equal(4, repeatCount);
        Assert.Equal(13, endIndex);
        Assert.Equal(5, endCount);
    }
}
