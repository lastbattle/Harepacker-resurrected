using HaCreator.MapSimulator.Loaders;
using MapleLib.WzLib.WzProperties;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class LifeLoaderMobAttackSourceTests
    {
        [Fact]
        public void ResolveMobAttackHitSource_IgnoresCanvasAndNumericHitMetadata()
        {
            var hit = new WzSubProperty("hit");
            hit.AddProperty(new WzIntProperty("attach", 1));
            hit.AddProperty(new WzCanvasProperty("0"));
            hit.AddProperty(new WzStringProperty("source", "Mob/9601065.img/attack1/info/hit"));

            string source = LifeLoader.ResolveMobAttackHitSourceUolForTests("9601065", hit);

            Assert.Equal("Mob/9601065.img/attack1/info/hit", source);
        }
    }
}
