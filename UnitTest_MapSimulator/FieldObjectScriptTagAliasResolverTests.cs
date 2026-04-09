using HaCreator.MapSimulator.Fields;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class FieldObjectScriptTagAliasResolverTests
{
    [Fact]
    public void ResolvePublishedTagMutation_RetiresSiblingStages_ForCamelCaseTrailingDigits()
    {
        string[] availableTags =
        {
            "cannonTuto",
            "cannonTuto2",
            "cannonTuto3"
        };

        FieldObjectScriptTagAliasResolver.PublishedTagMutation mutation =
            FieldObjectScriptTagAliasResolver.ResolvePublishedTagMutation("cannonTuto2", availableTags);

        Assert.Contains("cannonTuto2", mutation.TagsToEnable);
        Assert.Contains("cannonTuto", mutation.TagsToDisable);
        Assert.Contains("cannonTuto3", mutation.TagsToDisable);
    }

    [Fact]
    public void ResolvePublishedTagMutation_RetiresSiblingStages_ForBareTrailingDigits()
    {
        string[] availableTags =
        {
            "aranTutor",
            "aranTutor2",
            "aranTutor3"
        };

        FieldObjectScriptTagAliasResolver.PublishedTagMutation mutation =
            FieldObjectScriptTagAliasResolver.ResolvePublishedTagMutation("aranTutor3", availableTags);

        Assert.Contains("aranTutor3", mutation.TagsToEnable);
        Assert.Contains("aranTutor", mutation.TagsToDisable);
        Assert.Contains("aranTutor2", mutation.TagsToDisable);
    }

    [Fact]
    public void ResolvePublishedTagMutation_PreservesBaseTag_WhenStageZeroAliasMapsToBaseFamily()
    {
        string[] availableTags =
        {
            "vpTuto",
            "vpTuto1"
        };

        FieldObjectScriptTagAliasResolver.PublishedTagMutation mutation =
            FieldObjectScriptTagAliasResolver.ResolvePublishedTagMutation("vpTuto0", availableTags);

        Assert.Contains("vpTuto", mutation.TagsToEnable);
        Assert.Contains("vpTuto1", mutation.TagsToDisable);
    }
}
