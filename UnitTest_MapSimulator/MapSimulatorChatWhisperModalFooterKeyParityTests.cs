using HaCreator.MapSimulator;
using Microsoft.Xna.Framework.Input;

namespace UnitTest_MapSimulator;

public sealed class MapSimulatorChatWhisperModalFooterKeyParityTests
{
    [Fact]
    public void FooterFocusedBackspace_DoesNotForceComboEditMutation()
    {
        MapSimulatorChat chat = CreateModalFooterFocusedChat("Alpha");

        bool consumed = chat.HandleInput(
            new KeyboardState(Keys.Back),
            new KeyboardState(),
            tickCount: 16);

        MapSimulatorChatRenderState state = chat.GetRenderState();
        Assert.True(consumed);
        Assert.Equal("Alpha", state.InputText);
        Assert.Equal(
            MapSimulatorChat.WhisperTargetPickerModalFocusTarget.FooterButtons,
            state.WhisperTargetPickerModalFocusTarget);
    }

    [Fact]
    public void FooterFocusedDelete_DoesNotForceComboEditMutation()
    {
        MapSimulatorChat chat = CreateModalFooterFocusedChat("Alpha");

        bool consumed = chat.HandleInput(
            new KeyboardState(Keys.Delete),
            new KeyboardState(),
            tickCount: 16);

        MapSimulatorChatRenderState state = chat.GetRenderState();
        Assert.True(consumed);
        Assert.Equal("Alpha", state.InputText);
        Assert.Equal(
            MapSimulatorChat.WhisperTargetPickerModalFocusTarget.FooterButtons,
            state.WhisperTargetPickerModalFocusTarget);
    }

    [Fact]
    public void FooterFocusedTyping_DoesNotForceComboEditMutation()
    {
        MapSimulatorChat chat = CreateModalFooterFocusedChat("Alpha");

        bool consumed = chat.HandleInput(
            new KeyboardState(Keys.A),
            new KeyboardState(),
            tickCount: 16);

        MapSimulatorChatRenderState state = chat.GetRenderState();
        Assert.True(consumed);
        Assert.Equal("Alpha", state.InputText);
        Assert.Equal(
            MapSimulatorChat.WhisperTargetPickerModalFocusTarget.FooterButtons,
            state.WhisperTargetPickerModalFocusTarget);
    }

    [Fact]
    public void FooterFocusedUpThenBackspace_ReturnsToComboThenEdits()
    {
        MapSimulatorChat chat = CreateModalFooterFocusedChat("Alpha");

        bool upConsumed = chat.HandleInput(
            new KeyboardState(Keys.Up),
            new KeyboardState(),
            tickCount: 16);
        bool backConsumed = chat.HandleInput(
            new KeyboardState(Keys.Back),
            new KeyboardState(),
            tickCount: 32);

        MapSimulatorChatRenderState state = chat.GetRenderState();
        Assert.True(upConsumed);
        Assert.True(backConsumed);
        Assert.Equal("Alph", state.InputText);
        Assert.Equal(
            MapSimulatorChat.WhisperTargetPickerModalFocusTarget.ComboBox,
            state.WhisperTargetPickerModalFocusTarget);
    }

    private static MapSimulatorChat CreateModalFooterFocusedChat(string comboText)
    {
        MapSimulatorChat chat = new MapSimulatorChat();
        chat.Activate(0);
        chat.RememberWhisperTarget("Alpha");
        chat.RememberWhisperTarget("Beta");
        chat.OpenWhisperTargetPicker(
            0,
            initialTarget: comboText,
            presentation: MapSimulatorChat.WhisperTargetPickerPresentation.Modal);
        chat.ActivateWhisperTargetPickerModalButtonFocus();
        return chat;
    }
}
