using HaCreator.MapSimulator;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class MapSimulatorChatWhisperModalKeyParityTests
    {
        [Fact]
        public void ComboFocusedEnter_WithOpenDropdownAndNoTypedInput_AcceptsSeededSelectionWithoutClosingPicker()
        {
            MapSimulatorChat chat = CreateModalWhisperPickerWithCandidates();
            chat.OpenWhisperTargetPickerModalComboDropdown();

            MapSimulatorChatRenderState before = chat.GetRenderState("Tester");
            Assert.True(before.IsWhisperTargetPickerComboDropdownOpen);
            Assert.Equal(string.Empty, before.InputText);

            bool consumed = chat.HandleInput(
                new KeyboardState(Keys.Enter),
                new KeyboardState(),
                tickCount: 10);

            MapSimulatorChatRenderState after = chat.GetRenderState("Tester");
            Assert.True(consumed);
            Assert.True(after.IsWhisperTargetPickerActive);
            Assert.False(after.IsWhisperTargetPickerComboDropdownOpen);
            Assert.Equal("Charlie", after.InputText);
            Assert.Equal(after.InputText.Length, after.CursorPosition);
        }

        [Fact]
        public void ComboFocusedEnter_WithOpenDropdownAndNoCurrentRow_DoesNotFallThroughToModalConfirm()
        {
            MapSimulatorChat chat = CreateModalWhisperPickerWithCandidates();
            chat.OpenWhisperTargetPickerModalComboDropdown();

            bool typed = chat.HandleInput(
                new KeyboardState(Keys.Z),
                new KeyboardState(),
                tickCount: 10);
            Assert.True(typed);

            MapSimulatorChatRenderState beforeEnter = chat.GetRenderState("Tester");
            Assert.True(beforeEnter.IsWhisperTargetPickerComboDropdownOpen);
            Assert.Equal(-1, beforeEnter.WhisperTargetPickerSelectionIndex);
            Assert.Equal("z", beforeEnter.InputText);

            bool consumed = chat.HandleInput(
                new KeyboardState(Keys.Enter),
                new KeyboardState(),
                tickCount: 20);

            MapSimulatorChatRenderState after = chat.GetRenderState("Tester");
            Assert.True(consumed);
            Assert.True(after.IsWhisperTargetPickerActive);
            Assert.True(after.IsWhisperTargetPickerComboDropdownOpen);
            Assert.Equal(-1, after.WhisperTargetPickerSelectionIndex);
            Assert.Equal("z", after.InputText);
            Assert.Equal(after.InputText.Length, after.CursorPosition);
        }

        private static MapSimulatorChat CreateModalWhisperPickerWithCandidates()
        {
            MapSimulatorChat chat = new();
            chat.RememberWhisperTarget("Alpha");
            chat.RememberWhisperTarget("Bravo");
            chat.RememberWhisperTarget("Charlie");
            chat.OpenWhisperTargetPicker(
                tickCount: 1,
                presentation: MapSimulatorChat.WhisperTargetPickerPresentation.Modal);
            return chat;
        }
    }
}
