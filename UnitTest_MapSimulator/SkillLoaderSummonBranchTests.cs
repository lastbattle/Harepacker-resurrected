using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public class SkillLoaderSummonBranchTests
    {
        [Fact]
        public void SelectPreferredSummonAnimationBranch_PrefersSummonedOverIdleBranches()
        {
            string branch = SkillLoader.SelectPreferredSummonAnimationBranch(new[]
            {
                "stand",
                "summoned",
                "attack1"
            });

            Assert.Equal("summoned", branch);
        }

        [Fact]
        public void SelectPreferredSummonAnimationBranch_FallsBackToStandWhenSummonedIsMissing()
        {
            string branch = SkillLoader.SelectPreferredSummonAnimationBranch(new[]
            {
                "attack1",
                "stand",
                "die"
            });

            Assert.Equal("stand", branch);
        }

        [Fact]
        public void SelectPreferredSummonAnimationBranch_FallsBackToFirstAvailableBranch()
        {
            string branch = SkillLoader.SelectPreferredSummonAnimationBranch(new[]
            {
                "customIdle",
                "customAttack"
            });

            Assert.Equal("customIdle", branch);
        }
    }
}
