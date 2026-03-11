using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public class SkillLoaderSummonBranchTests
    {
        [Fact]
        public void SelectPreferredSummonSpawnBranch_PrefersSummonedOverIdleBranches()
        {
            string branch = SkillLoader.SelectPreferredSummonSpawnBranch(new[]
            {
                "stand",
                "summoned",
                "attack1"
            });

            Assert.Equal("summoned", branch);
        }

        [Fact]
        public void SelectPreferredSummonIdleBranch_FallsBackToStandWhenSummonedIsMissing()
        {
            string branch = SkillLoader.SelectPreferredSummonIdleBranch(new[]
            {
                "attack1",
                "stand",
                "die"
            });

            Assert.Equal("stand", branch);
        }

        [Fact]
        public void SelectPreferredSummonIdleBranch_FallsBackToFirstAvailableBranch()
        {
            string branch = SkillLoader.SelectPreferredSummonIdleBranch(new[]
            {
                "customIdle",
                "customAttack"
            });

            Assert.Equal("customIdle", branch);
        }

        [Fact]
        public void SelectPreferredSummonIdleBranch_PrefersStandOverAttackBranches()
        {
            string branch = SkillLoader.SelectPreferredSummonIdleBranch(new[]
            {
                "attack1",
                "fly",
                "stand"
            });

            Assert.Equal("stand", branch);
        }

        [Fact]
        public void SelectPreferredSummonAttackBranch_PrefersAttackOne()
        {
            string branch = SkillLoader.SelectPreferredSummonAttackBranch(new[]
            {
                "stand",
                "attack",
                "attack1"
            });

            Assert.Equal("attack1", branch);
        }

        [Fact]
        public void SelectPreferredSummonAttackBranch_ReturnsNullWhenAttackIsMissing()
        {
            string branch = SkillLoader.SelectPreferredSummonAttackBranch(new[]
            {
                "stand",
                "fly"
            });

            Assert.Null(branch);
        }

        [Fact]
        public void HasPersistentAvatarEffectBranches_RecognizesClientManagedAvatarLayerFamilies()
        {
            Assert.True(SkillLoader.HasPersistentAvatarEffectBranches(new[] { "effect", "special0" }, suddenDeath: false));
            Assert.True(SkillLoader.HasPersistentAvatarEffectBranches(new[] { "effect", "repeat" }, suddenDeath: true));
            Assert.False(SkillLoader.HasPersistentAvatarEffectBranches(new[] { "effect", "repeat" }, suddenDeath: false));
        }
    }
}
