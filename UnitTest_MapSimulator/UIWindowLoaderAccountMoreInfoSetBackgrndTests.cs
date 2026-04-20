using HaCreator.MapSimulator.Loaders;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator
{
    public class UIWindowLoaderAccountMoreInfoSetBackgrndTests
    {
        [Fact]
        public void ResolveAccountMoreInfoSetBackgrndCreateSizeForTesting_AddsExpansionToSourceSize()
        {
            (int width, int height) = UIWindowLoader.ResolveAccountMoreInfoSetBackgrndCreateSizeForTesting(
                sourceWidth: 320,
                sourceHeight: 240,
                expandWidth: 10,
                expandHeight: 20);

            Assert.Equal(330, width);
            Assert.Equal(260, height);
        }

        [Fact]
        public void ResolveAccountMoreInfoSetBackgrndExpandedSizeForTesting_UsesRequestedSizeWhenEnforced()
        {
            (int width, int height) = UIWindowLoader.ResolveAccountMoreInfoSetBackgrndExpandedSizeForTesting(
                sourceWidth: 512,
                sourceHeight: 512,
                requestedWidth: 398,
                requestedHeight: 355,
                enforceRequestedSize: true);

            Assert.Equal(398, width);
            Assert.Equal(355, height);
        }

        [Fact]
        public void ComposeAccountMoreInfoBackgroundPixelsForTesting_RespectsPositiveOffsets()
        {
            Color[] source =
            {
                Color.Red,
                Color.Green,
                Color.Blue,
                Color.White
            };

            Color[] composed = UIWindowLoader.ComposeAccountMoreInfoBackgroundPixelsForTesting(
                sourceData: source,
                sourceWidth: 2,
                sourceHeight: 2,
                destinationWidth: 4,
                destinationHeight: 4,
                destinationOffsetX: 1,
                destinationOffsetY: 1);

            Assert.Equal(Color.Red, composed[(1 * 4) + 1]);
            Assert.Equal(Color.Green, composed[(1 * 4) + 2]);
            Assert.Equal(Color.Blue, composed[(2 * 4) + 1]);
            Assert.Equal(Color.White, composed[(2 * 4) + 2]);
            Assert.Equal(default, composed[0]);
        }

        [Fact]
        public void ComposeAccountMoreInfoBackgroundPixelsForTesting_ClipsWhenOffsetsAreNegative()
        {
            Color[] source =
            {
                Color.Red,
                Color.Green,
                Color.Blue,
                Color.White
            };

            Color[] composed = UIWindowLoader.ComposeAccountMoreInfoBackgroundPixelsForTesting(
                sourceData: source,
                sourceWidth: 2,
                sourceHeight: 2,
                destinationWidth: 2,
                destinationHeight: 2,
                destinationOffsetX: -1,
                destinationOffsetY: -1);

            Assert.Equal(Color.White, composed[0]);
            Assert.Equal(default, composed[1]);
            Assert.Equal(default, composed[2]);
            Assert.Equal(default, composed[3]);
        }
    }
}
