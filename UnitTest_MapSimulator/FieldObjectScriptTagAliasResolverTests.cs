using System.Linq;
using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator
{
    public sealed class FieldObjectScriptTagAliasResolverTests
    {
        [Fact]
        public void ResolvePublishedTags_MatchesCamelCaseTag_ForStageOneEntryScript()
        {
            string[] resolved = FieldObjectScriptTagAliasResolver.ResolvePublishedTags(
                "cannon_tuto_01",
                new[] { "cannonTuto", "cannonTuto2", "back_stoner" }).ToArray();

            Assert.Equal(new[] { "cannonTuto" }, resolved);
        }

        [Fact]
        public void ResolvePublishedTags_MatchesNumberedCamelCaseTag_ForLaterStages()
        {
            string[] resolved = FieldObjectScriptTagAliasResolver.ResolvePublishedTags(
                "cannon_tuto_03",
                new[] { "cannonTuto", "cannonTuto2", "cannonTuto3" }).ToArray();

            Assert.Equal(new[] { "cannonTuto3" }, resolved);
        }

        [Fact]
        public void ResolvePublishedTags_PrefersDirectTagMatch_WhenScriptAlreadyNamesTheTag()
        {
            string[] resolved = FieldObjectScriptTagAliasResolver.ResolvePublishedTags(
                "back_stoner",
                new[] { "back_stoner", "backStoner" }).ToArray();

            Assert.Equal(new[] { "back_stoner" }, resolved);
        }

        [Fact]
        public void ResolvePublishedTags_ReturnsEmpty_WhenNoAvailableTagAliasMatches()
        {
            string[] resolved = FieldObjectScriptTagAliasResolver.ResolvePublishedTags(
                "cannon_tuto_direction",
                new[] { "cannonTuto", "cannonTuto2" }).ToArray();

            Assert.Empty(resolved);
        }
    }
}
