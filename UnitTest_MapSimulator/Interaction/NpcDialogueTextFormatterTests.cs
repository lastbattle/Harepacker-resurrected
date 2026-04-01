using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator.Interaction;

public sealed class NpcDialogueTextFormatterTests
{
    [Fact]
    public void Format_PreservesMeaningfulInternalSpacesAndLineBreaks()
    {
        const string text = "  Alpha  Beta\\n  Gamma   Delta  ";

        string formatted = NpcDialogueTextFormatter.Format(text);

        Assert.Equal("Alpha  Beta\nGamma   Delta", formatted);
    }

    [Fact]
    public void Format_NormalizesTabsWithoutCollapsingExistingSpaces()
    {
        const string text = "Alpha \t Beta\t\tGamma";

        string formatted = NpcDialogueTextFormatter.Format(text);

        Assert.Equal("Alpha  Beta Gamma", formatted);
    }
}
