using HaCreator.MapSimulator.Character;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class MirrorImageLayerParityTests
{
    [Fact]
    public void ResolveMirrorImagePreparedFallbackFacing_UsesCurrentFacing()
    {
        Assert.True(PlayerCharacter.ResolveMirrorImagePreparedFallbackFacing(
            preparedFacingRight: false,
            currentFacingRight: true));
        Assert.False(PlayerCharacter.ResolveMirrorImagePreparedFallbackFacing(
            preparedFacingRight: true,
            currentFacingRight: false));
    }

    [Fact]
    public void ResolveMirrorImageRenderablePositionBounds_PrefersLiveBoundsWhenAvailable()
    {
        Rectangle result = PlayerCharacter.ResolveMirrorImageRenderablePositionBounds(
            preparedBounds: new Rectangle(-14, -9, 40, 32),
            liveBounds: new Rectangle(-10, -5, 44, 36));

        Assert.Equal(new Rectangle(-10, -5, 44, 36), result);
    }

    [Fact]
    public void ResolveMirrorImageRenderablePositionBounds_FallsBackToPreparedBoundsWhenLiveBoundsMissing()
    {
        Rectangle preparedBounds = new(-14, -9, 40, 32);

        Rectangle result = PlayerCharacter.ResolveMirrorImageRenderablePositionBounds(
            preparedBounds,
            Rectangle.Empty);

        Assert.Equal(preparedBounds, result);
    }

    [Fact]
    public void ResolveMirrorImagePreparedFallbackPartBaseOffset_TracksLivePlacementDelta()
    {
        Point result = PlayerCharacter.ResolveMirrorImagePreparedFallbackPartBaseOffset(
            preparedBounds: new Rectangle(-14, -9, 40, 32),
            positionBounds: new Rectangle(-10, -5, 44, 36));

        Assert.Equal(new Point(4, 4), result);
    }
}
