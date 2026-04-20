using System.Linq;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public sealed class SkillLoaderMorphTemplateParityTests
    {
        [Fact]
        public void FlagOnlyMorphTemplateCandidates_KeepNoActionOutlierOnMorph0109()
        {
            int[] candidates = SkillLoader
                .EnumerateFlagOnlyMorphTemplateCandidatesForTesting(0010109)
                .ToArray();

            Assert.Equal(new[] { 109 }, candidates);
        }

        [Fact]
        public void FlagOnlyMorphTemplateCandidates_KeepActionBackedOutlierOnMorph0111()
        {
            int[] candidates = SkillLoader
                .EnumerateFlagOnlyMorphTemplateCandidatesForTesting(20020111)
                .ToArray();

            Assert.Equal(new[] { 111 }, candidates);
        }

        [Fact]
        public void FlagOnlyMorphTemplateCandidates_FallBackToSkillSuffixForGeneralRows()
        {
            int[] candidates = SkillLoader
                .EnumerateFlagOnlyMorphTemplateCandidatesForTesting(98765432)
                .ToArray();

            Assert.Equal(new[] { 5432 }, candidates);
        }
    }
}
