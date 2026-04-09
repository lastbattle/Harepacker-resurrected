using HaCreator.MapSimulator.UI;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class SkillMacroParityTests
{
    [Fact]
    public void SkillMacroSoftKeyboardLayout_EnumeratesDashKeyAsVisible()
    {
        int[] visibleKeys = SkillMacroSoftKeyboardLayout.EnumerateVisibleKeyIndices(minimized: false).ToArray();

        Assert.Contains(36, visibleKeys);
    }

    [Fact]
    public void SkillMacroSoftKeyboardLayout_ResolvesDashKeyHitTarget()
    {
        Rectangle dashBounds = SkillMacroSoftKeyboardLayout.GetKeyBounds(36);

        int keyIndex = SkillMacroSoftKeyboardLayout.GetKeyIndexFromPoint(dashBounds.Center.X, dashBounds.Center.Y);

        Assert.Equal(36, keyIndex);
    }

    [Theory]
    [InlineData("Macro-1", true)]
    [InlineData("Macro_1", true)]
    [InlineData("Macro\u20101", false)]
    [InlineData("Macro\u203F1", false)]
    public void SkillMacroNameRules_OnlyAcceptsClientVisibleDashAndConnectorSurface(string text, bool expected)
    {
        bool result = SkillMacroNameRules.TryNormalize(text, out string normalized, out _);

        Assert.Equal(expected, result);
        if (expected)
        {
            Assert.Equal(text, normalized);
        }
        else
        {
            Assert.Null(normalized);
        }
    }
}
