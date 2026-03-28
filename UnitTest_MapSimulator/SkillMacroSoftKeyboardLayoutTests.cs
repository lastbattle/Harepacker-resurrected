using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class SkillMacroSoftKeyboardLayoutTests
{
    [Theory]
    [InlineData(24, 20, 0)]
    [InlineData(47, 43, 10)]
    [InlineData(58, 66, 20)]
    [InlineData(74, 89, 29)]
    public void GetKeyIndexFromPoint_UsesClientRowGeometry(int x, int y, int expectedIndex)
    {
        int keyIndex = SkillMacroSoftKeyboardLayout.GetKeyIndexFromPoint(x, y);

        Assert.Equal(expectedIndex, keyIndex);
    }

    [Fact]
    public void GetFunctionKeyFromPoint_MapsSpecialKeys()
    {
        Assert.Equal(
            SkillMacroSoftKeyboardFunctionKey.CapsLock,
            SkillMacroSoftKeyboardLayout.GetFunctionKeyFromPoint(20, 50, minimized: false));
        Assert.Equal(
            SkillMacroSoftKeyboardFunctionKey.Enter,
            SkillMacroSoftKeyboardLayout.GetFunctionKeyFromPoint(250, 50, minimized: false));
        Assert.Equal(
            SkillMacroSoftKeyboardFunctionKey.Backspace,
            SkillMacroSoftKeyboardLayout.GetFunctionKeyFromPoint(260, 30, minimized: false));
    }

    [Fact]
    public void GetWindowButtonFromPoint_MapsHeaderButtons()
    {
        Assert.Equal(
            SkillMacroSoftKeyboardWindowButton.Maximize,
            SkillMacroSoftKeyboardLayout.GetWindowButtonFromPoint(247, 5));
        Assert.Equal(
            SkillMacroSoftKeyboardWindowButton.Minimize,
            SkillMacroSoftKeyboardLayout.GetWindowButtonFromPoint(261, 5));
        Assert.Equal(
            SkillMacroSoftKeyboardWindowButton.Close,
            SkillMacroSoftKeyboardLayout.GetWindowButtonFromPoint(275, 5));
    }

    [Theory]
    [InlineData(false, 10, "q")]
    [InlineData(true, 10, "Q")]
    [InlineData(false, 36, "-")]
    [InlineData(true, 36, "_")]
    public void GetKeyText_ReturnsShiftAwareLabel(bool uppercase, int keyIndex, string expected)
    {
        string label = SkillMacroSoftKeyboardLayout.GetKeyText(keyIndex, uppercase);

        Assert.Equal(expected, label);
    }

    [Fact]
    public void GetBounds_UsesExpandedWidthAndModeSpecificHeight()
    {
        Rectangle expanded = SkillMacroSoftKeyboardLayout.GetBounds(new Point(10, 20), minimized: false);
        Rectangle minimized = SkillMacroSoftKeyboardLayout.GetBounds(new Point(10, 20), minimized: true);

        Assert.Equal(new Rectangle(10, 20, 290, 136), expanded);
        Assert.Equal(new Rectangle(10, 20, 290, 119), minimized);
    }
}
