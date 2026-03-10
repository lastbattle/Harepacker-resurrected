using HaCreator.MapSimulator.Character.Skills;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace UnitTest_MapSimulator
{
    public class SkillDataRangeTests
    {
        [Fact]
        public void GetAttackRange_UsesExplicitLtRbBoundsAndMirrorsLeftFacing()
        {
            var skill = new SkillData
            {
                MaxLevel = 1,
                Levels =
                {
                    [1] = new SkillLevelData
                    {
                        RangeL = 70,
                        RangeR = 110,
                        RangeTop = -45,
                        RangeBottom = 25,
                        RangeY = 70
                    }
                }
            };

            XnaRectangle rightFacing = skill.GetAttackRange(1, facingRight: true);
            XnaRectangle leftFacing = skill.GetAttackRange(1, facingRight: false);

            Assert.Equal(new XnaRectangle(-70, -45, 180, 70), rightFacing);
            Assert.Equal(new XnaRectangle(-110, -45, 180, 70), leftFacing);
        }

        [Fact]
        public void GetAttackRange_FallsBackToLegacyCenteredRangeWhenExplicitBoundsMissing()
        {
            var skill = new SkillData
            {
                MaxLevel = 1,
                Levels =
                {
                    [1] = new SkillLevelData
                    {
                        Range = 90,
                        RangeY = 60
                    }
                }
            };

            XnaRectangle rightFacing = skill.GetAttackRange(1, facingRight: true);
            XnaRectangle leftFacing = skill.GetAttackRange(1, facingRight: false);

            Assert.Equal(new XnaRectangle(0, -30, 90, 60), rightFacing);
            Assert.Equal(new XnaRectangle(-90, -30, 90, 60), leftFacing);
        }
    }
}
