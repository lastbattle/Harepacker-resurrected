using HaCreator.MapSimulator.Loaders;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator
{
    public class UIWindowLoaderAccountMoreInfoSetBackgrndTests
    {
        [Fact]
        public void ComposeAccountMoreInfoBackgroundPixelsForTesting_AppliesPositiveOffset()
        {
            Color[] source =
            {
                new Color(1, 0, 0),
                new Color(2, 0, 0),
                new Color(3, 0, 0),
                new Color(4, 0, 0),
            };

            Color[] composed = UIWindowLoader.ComposeAccountMoreInfoBackgroundPixelsForTesting(
                source,
                sourceWidth: 2,
                sourceHeight: 2,
                destinationWidth: 4,
                destinationHeight: 4,
                destinationOffsetX: 1,
                destinationOffsetY: 1);

            Assert.Equal(new Color(1, 0, 0), composed[(1 * 4) + 1]);
            Assert.Equal(new Color(2, 0, 0), composed[(1 * 4) + 2]);
            Assert.Equal(new Color(3, 0, 0), composed[(2 * 4) + 1]);
            Assert.Equal(new Color(4, 0, 0), composed[(2 * 4) + 2]);
            Assert.Equal(Color.Transparent, composed[0]);
        }

        [Fact]
        public void ComposeAccountMoreInfoBackgroundPixelsForTesting_ClipsNegativeOffset()
        {
            Color[] source =
            {
                new Color(10, 0, 0),
                new Color(20, 0, 0),
                new Color(30, 0, 0),
                new Color(40, 0, 0),
            };

            Color[] composed = UIWindowLoader.ComposeAccountMoreInfoBackgroundPixelsForTesting(
                source,
                sourceWidth: 2,
                sourceHeight: 2,
                destinationWidth: 2,
                destinationHeight: 2,
                destinationOffsetX: -1,
                destinationOffsetY: 0);

            Assert.Equal(new Color(20, 0, 0), composed[0]);
            Assert.Equal(new Color(40, 0, 0), composed[2]);
            Assert.Equal(Color.Transparent, composed[1]);
            Assert.Equal(Color.Transparent, composed[3]);
        }
    }
}
