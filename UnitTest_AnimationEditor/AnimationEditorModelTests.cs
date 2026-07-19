using HaCreator.GUI.FrameAnimation;
using MapleLib.WzLib.WzProperties;
using System.Globalization;

namespace UnitTest_AnimationEditor;

public class AnimationEditorModelTests
{
    [Fact]
    public void ReadOnlyPropertyRowRejectsChanges()
    {
        WzIntProperty property = new("delay", 100);
        AnimationPropertyRow row = new(property, isReadOnly: true);
        bool changed = false;
        row.ValueChanged += (_, _) => changed = true;

        row.Value = "250";

        Assert.Equal(100, property.Value);
        Assert.Equal("100", row.Value);
        Assert.False(changed);
    }

    [Fact]
    public void EditablePropertyRowReportsCanonicalOldAndNewValues()
    {
        WzIntProperty property = new("delay", 100);
        AnimationPropertyRow row = new(property);
        AnimationPropertyValueChangedEventArgs? changing = null;
        AnimationPropertyValueChangedEventArgs? changed = null;
        row.ValueChanging += (_, args) => changing = args;
        row.ValueChanged += (_, args) => changed = args;

        row.Value = "0250";

        Assert.Equal(250, property.Value);
        Assert.Equal("250", row.Value);
        Assert.Equal("100", changing?.OldValue);
        Assert.Equal("250", changing?.NewValue);
        Assert.Equal("100", changed?.OldValue);
        Assert.Equal("250", changed?.NewValue);
    }

    [Fact]
    public void FrameDelayDisplayUsesLocalizedTemplate()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            WzCanvasProperty canvas = new("0") { PngProperty = new WzPngProperty() };
            canvas.AddProperty(new WzIntProperty("delay", 125));
            AnimationFrameModel frame = new(canvas, canvas, 0, () => { });

            Assert.Equal("125 ms", frame.DisplayDelay);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
