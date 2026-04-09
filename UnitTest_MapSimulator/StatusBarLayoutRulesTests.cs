using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator;

public sealed class StatusBarLayoutRulesTests
{
    [Theory]
    [InlineData("1", 45f)]
    [InlineData("10", 39f)]
    [InlineData("255", 33f)]
    [InlineData("9999", 33f)]
    [InlineData("", 45f)]
    [InlineData(null, 45f)]
    public void ResolveLevelSlotX_MatchesClientSetStatusValueSlots(string levelText, float expected)
    {
        float result = StatusBarLayoutRules.ResolveLevelSlotX(levelText);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("  Hero (Fighter)\r\n", "Fighter")]
    [InlineData("  Aran\tMaster  ", "Aran Master")]
    [InlineData("", "Beginner")]
    [InlineData(null, "Beginner")]
    public void FormatJobLabel_SanitizesAndUsesClientFacingTitleSlot(string input, string expected)
    {
        string result = StatusBarLayoutRules.FormatJobLabel(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(" Maple\r\nHero ", "Maple Hero")]
    [InlineData("", "Player")]
    [InlineData(null, "Player")]
    public void FormatNameLabel_SanitizesSingleLineFallback(string input, string expected)
    {
        string result = StatusBarLayoutRules.FormatNameLabel(input);

        Assert.Equal(expected, result);
    }
}
