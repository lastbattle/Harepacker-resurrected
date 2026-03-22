using HaCreator.MapSimulator.UI;
using System.Text;

namespace UnitTest_MapSimulator;

public sealed class SkillMacroNameRulesTests
{
    [Fact]
    public void TryAppendBestEffort_TruncatesAtByteBudgetForMultibyteLocaleText()
    {
        Encoding korean = Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

        bool appended = SkillMacroNameRules.TryAppendBestEffort(
            string.Empty,
            "가나다라마바사",
            out string updatedText,
            out string error,
            korean);

        Assert.True(appended);
        Assert.Equal("가나다라마바", updatedText);
        Assert.Equal(string.Empty, error);
        Assert.Equal(SkillMacroNameRules.MaxNameBytes, SkillMacroNameRules.GetByteCount(updatedText, korean));
    }

    [Fact]
    public void TryAppendBestEffort_RejectsUnencodableTextWhenNoPrefixFits()
    {
        Encoding western = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

        bool appended = SkillMacroNameRules.TryAppendBestEffort(
            string.Empty,
            "漢",
            out string updatedText,
            out string error,
            western);

        Assert.False(appended);
        Assert.Equal(string.Empty, updatedText);
        Assert.Equal("This character is not available in the current system locale.", error);
    }

    [Fact]
    public void TryNormalizeForEdit_AllowsEmptyTextDuringInlineEditing()
    {
        bool valid = SkillMacroNameRules.TryNormalizeForEdit(string.Empty, out string normalized, out string error);

        Assert.True(valid);
        Assert.Equal(string.Empty, normalized);
        Assert.Equal(string.Empty, error);
    }
}
