using System.Reflection;
using System.Text;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class StatusBarChatWhisperPickerTests
{
    [Fact]
    public void OpenWhisperTargetPicker_WithoutExplicitTarget_KeepsEditBlankAndSelectionEmpty()
    {
        MapSimulatorChat chat = new();
        SetWhisperCandidates(chat, "Alice", "Bob");
        SetPrivateField(chat, "_replyTarget", string.Empty);
        SetPrivateField(chat, "_whisperTarget", string.Empty);

        chat.OpenWhisperTargetPicker(100, presentation: MapSimulatorChat.WhisperTargetPickerPresentation.Modal);
        MapSimulatorChatRenderState state = chat.GetRenderState();

        Assert.True(state.IsWhisperTargetPickerActive);
        Assert.Equal(MapSimulatorChat.WhisperTargetPickerPresentation.Modal, state.WhisperTargetPickerPresentation);
        Assert.Equal(string.Empty, state.InputText);
        Assert.Equal(-1, state.WhisperTargetPickerSelectionIndex);
        Assert.Equal(new[] { "Alice", "Bob" }, state.WhisperCandidates);
    }

    [Fact]
    public void SyncWhisperTargetPickerSelectionFromInput_BlankEditClearsSelection()
    {
        MapSimulatorChat chat = new();
        SetWhisperCandidates(chat, "Alice", "Bob");
        chat.OpenWhisperTargetPicker(100, "Alice", MapSimulatorChat.WhisperTargetPickerPresentation.Modal);

        SetInputText(chat, string.Empty);
        InvokePrivateMethod(chat, "SyncWhisperTargetPickerSelectionFromInput");

        MapSimulatorChatRenderState state = chat.GetRenderState();
        Assert.Equal(string.Empty, state.InputText);
        Assert.Equal(-1, state.WhisperTargetPickerSelectionIndex);
    }

    [Fact]
    public void ResolveWhisperPickerModalListBounds_UsesAuthoredDividerWidthWhenContentIsNarrower()
    {
        Rectangle bounds = StatusBarChatLayoutRules.ResolveWhisperPickerModalListBounds(
            new Rectangle(0, 0, 519, 200),
            listTop: 38,
            rowHeight: 17,
            visibleRowCount: 6,
            minimumRowWidth: 137,
            maxMeasuredTextWidth: 92f,
            authoredDividerWidth: 332);

        Assert.Equal(new Rectangle(93, 38, 332, 119), bounds);
    }

    [Fact]
    public void ResolveWhisperPickerModalListBounds_GrowsBeyondDividerForWideMeasuredContent()
    {
        Rectangle bounds = StatusBarChatLayoutRules.ResolveWhisperPickerModalListBounds(
            new Rectangle(10, 20, 519, 200),
            listTop: 64,
            rowHeight: 17,
            visibleRowCount: 6,
            minimumRowWidth: 137,
            maxMeasuredTextWidth: 420f,
            authoredDividerWidth: 332);

        Assert.Equal(434, bounds.Width);
        Assert.Equal(52, bounds.X);
        Assert.Equal(64, bounds.Y);
        Assert.Equal(119, bounds.Height);
    }

    private static void SetWhisperCandidates(MapSimulatorChat chat, params string[] candidates)
    {
        List<string> whisperCandidates = GetPrivateField<List<string>>(chat, "_whisperCandidates");
        whisperCandidates.Clear();
        whisperCandidates.AddRange(candidates);
    }

    private static void SetInputText(MapSimulatorChat chat, string text)
    {
        StringBuilder inputText = GetPrivateField<StringBuilder>(chat, "_inputText");
        inputText.Clear();
        inputText.Append(text);
        SetPrivateField(chat, "_cursorPosition", inputText.Length);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(instance));
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static void InvokePrivateMethod(object instance, string methodName)
    {
        MethodInfo? method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(instance, null);
    }
}
