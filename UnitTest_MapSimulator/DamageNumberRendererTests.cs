using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Loaders;
using XnaPoint = Microsoft.Xna.Framework.Point;

namespace UnitTest_MapSimulator;

public class DamageNumberRendererTests
{
    [Fact]
    public void PrepareVisual_SingleDigitCanvasContainsDigitBounds()
    {
        DamageNumberDigitSet largeDigitSet = CreateDigitSet("NoRed1", width: 43, originX: 21, height: 48, originY: 47);
        DamageNumberDigitSet smallDigitSet = CreateDigitSet("NoRed0", width: 31, originX: 15, height: 33, originY: 32);

        var visual = DamageNumberRenderer.PrepareVisual(
            damage: 7,
            colorType: DamageColorType.Red,
            isCritical: false,
            isMiss: false,
            largeDigitSet,
            smallDigitSet);

        AssertDigitBoundsFitCanvas(visual, largeDigitSet, smallDigitSet);
    }

    [Fact]
    public void PrepareVisual_MultiDigitCanvasContainsRightmostDigitBounds()
    {
        DamageNumberDigitSet largeDigitSet = CreateDigitSet("NoRed1", width: 43, originX: 21, height: 48, originY: 47);
        DamageNumberDigitSet smallDigitSet = CreateDigitSet("NoRed0", width: 31, originX: 15, height: 33, originY: 32);

        var visual = DamageNumberRenderer.PrepareVisual(
            damage: 123456,
            colorType: DamageColorType.Red,
            isCritical: false,
            isMiss: false,
            largeDigitSet,
            smallDigitSet);

        AssertDigitBoundsFitCanvas(visual, largeDigitSet, smallDigitSet);
    }

    private static void AssertDigitBoundsFitCanvas(
        DamageNumberRenderer.PreparedDamageNumberVisual visual,
        DamageNumberDigitSet largeDigitSet,
        DamageNumberDigitSet smallDigitSet)
    {
        Assert.NotEmpty(visual.Digits);

        foreach (var digit in visual.Digits)
        {
            DamageNumberDigitSet digitSet = digit.UseLargeDigitSet ? largeDigitSet : smallDigitSet;
            int width = digitSet.Widths[digit.Digit];
            int drawLeft = digit.DrawOffsetX;
            int drawRight = drawLeft + width;

            Assert.True(drawLeft >= 0, $"Digit {digit.Digit} starts before canvas: {drawLeft}");
            Assert.True(
                drawRight <= visual.CanvasWidth,
                $"Digit {digit.Digit} extends past canvas: right={drawRight}, width={visual.CanvasWidth}");
        }
    }

    private static DamageNumberDigitSet CreateDigitSet(string name, int width, int originX, int height, int originY)
    {
        var digitSet = new DamageNumberDigitSet
        {
            Name = name,
            IsLoaded = true
        };

        for (int i = 0; i < 10; i++)
        {
            digitSet.Widths[i] = width;
            digitSet.Heights[i] = height;
            digitSet.Origins[i] = new XnaPoint(originX, originY);
        }

        return digitSet;
    }
}
