using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class PacketQuestResultClientSemanticsTests
{
    [Fact]
    public void GetSpeakerFrameBounds_AnchorsSpriteFromWzOriginWithinPortraitColumn()
    {
        Rectangle portraitBounds = new(18, 18, 220, 120);

        Rectangle result = PacketQuestResultUtilDialogLayout.GetSpeakerFrameBounds(
            portraitBounds,
            new Point(19, 73),
            frameWidth: 65,
            frameHeight: 74);

        Assert.Equal(new Rectangle(97, 18, 105, 120), result);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(179, 0)]
    [InlineData(180, 1)]
    [InlineData(359, 1)]
    [InlineData(360, 2)]
    [InlineData(540, 0)]
    public void ResolvePacketQuestResultSpeakerFrameIndex_UsesClientStyleLoopingFrameDelays(int elapsedMs, int expectedIndex)
    {
        int result = NpcInteractionOverlay.ResolvePacketQuestResultSpeakerFrameIndex(new[] { 180, 180, 180 }, elapsedMs);

        Assert.Equal(expectedIndex, result);
    }
}
