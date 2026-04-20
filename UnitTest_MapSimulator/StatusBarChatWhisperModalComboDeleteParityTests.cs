using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class StatusBarChatWhisperModalComboDeleteParityTests
{
    [Fact]
    public void SelectRowIndex_UsesFirstVisibleOffset()
    {
        Rectangle dropdownBounds = new Rectangle(100, 200, 180, 96);
        int releaseY = dropdownBounds.Y + 3 * StatusBarChatLayoutRules.ClientWhisperPickerModalComboDropdownRowHeight + 5;

        int row = StatusBarChatUI.ResolveWhisperPickerClientComboRowIndexFromReleaseY(
            releaseY,
            dropdownBounds,
            firstVisibleIndex: 12,
            candidateCount: 40);

        Assert.Equal(15, row);
    }

    [Fact]
    public void DeleteRowIndexFromReleaseY_DoesNotUseFirstVisibleOffset()
    {
        Rectangle dropdownBounds = new Rectangle(100, 200, 180, 96);
        int releaseY = dropdownBounds.Y + 3 * StatusBarChatLayoutRules.ClientWhisperPickerModalComboDropdownRowHeight + 5;

        int row = StatusBarChatUI.ResolveWhisperPickerClientComboDeleteIndexFromReleaseY(
            releaseY,
            dropdownBounds,
            candidateCount: 40);

        Assert.Equal(3, row);
    }

    [Fact]
    public void DeleteRowIndexFromReleaseY_ClampsToCandidateRange()
    {
        Rectangle dropdownBounds = new Rectangle(100, 200, 180, 96);

        int aboveTop = StatusBarChatUI.ResolveWhisperPickerClientComboDeleteIndexFromReleaseY(
            dropdownBounds.Y - 8,
            dropdownBounds,
            candidateCount: 4);
        int belowBottom = StatusBarChatUI.ResolveWhisperPickerClientComboDeleteIndexFromReleaseY(
            dropdownBounds.Bottom + 32,
            dropdownBounds,
            candidateCount: 4);

        Assert.Equal(0, aboveTop);
        Assert.Equal(3, belowBottom);
    }
}
