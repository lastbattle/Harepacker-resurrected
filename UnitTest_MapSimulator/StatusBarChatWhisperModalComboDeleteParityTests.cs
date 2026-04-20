using HaCreator.MapSimulator;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator
{
    public class StatusBarChatWhisperModalComboDeleteParityTests
    {
        [Fact]
        public void ModalComboSelectRowMapping_UsesFirstVisibleOffset_AndIgnoresScrollbarLane()
        {
            Rectangle dropdownBounds = new Rectangle(100, 200, 222, 96);
            Rectangle rowContentBounds = new Rectangle(100, 200, 210, 96);

            int mappedRow = StatusBarChatUI.ResolveWhisperPickerClientComboRowIndexFromReleasePoint(
                releaseX: 110,
                releaseY: 233,
                rowContentBounds,
                dropdownBounds,
                firstVisibleIndex: 3,
                candidateCount: 10,
                rowHeight: 16);
            int scrollbarLaneRow = StatusBarChatUI.ResolveWhisperPickerClientComboRowIndexFromReleasePoint(
                releaseX: 318,
                releaseY: 233,
                rowContentBounds,
                dropdownBounds,
                firstVisibleIndex: 3,
                candidateCount: 10,
                rowHeight: 16);

            Assert.Equal(5, mappedRow);
            Assert.Equal(-1, scrollbarLaneRow);
        }

        [Fact]
        public void ModalComboDeleteRowMapping_UsesVisibleRow_AndIgnoresScrollbarLane()
        {
            Rectangle dropdownBounds = new Rectangle(100, 200, 222, 96);
            Rectangle rowContentBounds = new Rectangle(100, 200, 210, 96);

            int mappedDeleteRow = StatusBarChatUI.ResolveWhisperPickerClientComboDeleteIndexFromReleasePoint(
                releaseX: 110,
                releaseY: 200 + (5 * 16),
                rowContentBounds,
                dropdownBounds,
                candidateCount: 3,
                rowHeight: 16);
            int scrollbarLaneDeleteRow = StatusBarChatUI.ResolveWhisperPickerClientComboDeleteIndexFromReleasePoint(
                releaseX: 318,
                releaseY: 220,
                rowContentBounds,
                dropdownBounds,
                candidateCount: 3,
                rowHeight: 16);

            Assert.Equal(2, mappedDeleteRow);
            Assert.Equal(-1, scrollbarLaneDeleteRow);
        }

        [Fact]
        public void ModalComboDelete_ResetsSelectionToFirstCandidate_AndKeepsComboFocus()
        {
            MapSimulatorChat chat = new MapSimulatorChat();
            chat.RememberWhisperTarget("ZetaOne");
            chat.RememberWhisperTarget("BetaTwo");
            chat.RememberWhisperTarget("Alpha3");

            chat.OpenWhisperTargetPicker(0, presentation: MapSimulatorChat.WhisperTargetPickerPresentation.Modal);
            chat.OpenWhisperTargetPickerModalComboDropdown();
            Assert.True(chat.SelectWhisperTargetPickerModalComboDropdownCandidateAtClientRowIndex(2));

            Assert.True(chat.DeleteWhisperTargetPickerModalComboDropdownCandidateAtClientRowIndex(2));
            MapSimulatorChatRenderState state = chat.GetRenderState();

            Assert.Equal(0, state.WhisperTargetPickerSelectionIndex);
            Assert.Equal("Alpha3", state.InputText);
            Assert.Equal(
                MapSimulatorChat.WhisperTargetPickerModalFocusTarget.ComboBox,
                state.WhisperTargetPickerModalFocusTarget);
        }

        [Fact]
        public void ModalComboDelete_WhenListBecomesEmpty_ClearsInputAndClosesDropdown()
        {
            MapSimulatorChat chat = new MapSimulatorChat();
            chat.RememberWhisperTarget("Solo12");

            chat.OpenWhisperTargetPicker(0, presentation: MapSimulatorChat.WhisperTargetPickerPresentation.Modal);
            chat.OpenWhisperTargetPickerModalComboDropdown();

            Assert.True(chat.DeleteWhisperTargetPickerModalComboDropdownCandidateAtClientRowIndex(0));
            MapSimulatorChatRenderState state = chat.GetRenderState();

            Assert.Empty(state.WhisperCandidates);
            Assert.Equal(-1, state.WhisperTargetPickerSelectionIndex);
            Assert.Equal(string.Empty, state.InputText);
            Assert.False(state.IsWhisperTargetPickerComboDropdownOpen);
        }
    }
}
