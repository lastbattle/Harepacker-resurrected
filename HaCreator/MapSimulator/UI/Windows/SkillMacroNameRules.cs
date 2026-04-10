using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    internal static class SkillMacroNameRules
    {
        internal const int MaxNameBytes = 12;

        static SkillMacroNameRules()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        internal static int GetByteCount(string text, Encoding encoding = null)
        {
            string value = text ?? string.Empty;
            Encoding activeEncoding = GetStrictEncoding(encoding);
            return activeEncoding.GetByteCount(value);
        }

        internal static bool TryNormalize(string text, out string normalized, out string error, Encoding encoding = null)
        {
            string trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                normalized = null;
                error = "Enter a macro name.";
                return false;
            }

            foreach (char ch in trimmed)
            {
                if (char.IsControl(ch))
                {
                    normalized = null;
                    error = "Macro names cannot contain control characters.";
                    return false;
                }
            }

            if (TryGetBlacklistedCharacterError(trimmed, out error))
            {
                normalized = null;
                return false;
            }

            Encoding activeEncoding = GetStrictEncoding(encoding);
            try
            {
                int byteCount = activeEncoding.GetByteCount(trimmed);
                if (byteCount > MaxNameBytes)
                {
                    normalized = null;
                    error = $"Macro names can use up to {MaxNameBytes} bytes.";
                    return false;
                }
            }
            catch (EncoderFallbackException)
            {
                normalized = null;
                error = "This character is not available in the current system locale.";
                return false;
            }

            normalized = trimmed;
            error = string.Empty;
            return true;
        }

        internal static bool TryAppend(string currentText, string appendedText, out string updatedText, out string error, Encoding encoding = null)
        {
            string candidate = (currentText ?? string.Empty) + (appendedText ?? string.Empty);
            if (TryNormalizeForEdit(candidate, out updatedText, out error, encoding))
            {
                return true;
            }

            updatedText = currentText ?? string.Empty;
            return false;
        }

        internal static bool TryAppendBestEffort(string currentText, string appendedText, out string updatedText, out string error, Encoding encoding = null)
        {
            string original = currentText ?? string.Empty;
            string incoming = appendedText ?? string.Empty;
            if (incoming.Length == 0)
            {
                updatedText = original;
                error = string.Empty;
                return true;
            }

            string working = original;
            string firstError = string.Empty;
            bool appendedAny = false;
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(incoming);
            while (enumerator.MoveNext())
            {
                string textElement = enumerator.GetTextElement();
                if (TryAppend(working, textElement, out string candidate, out string candidateError, encoding))
                {
                    working = candidate;
                    appendedAny = true;
                    continue;
                }

                if (string.IsNullOrEmpty(firstError))
                {
                    firstError = candidateError;
                }

                if (candidateError == $"Macro names can use up to {MaxNameBytes} bytes.")
                {
                    break;
                }

                if (!appendedAny)
                {
                    updatedText = original;
                    error = candidateError;
                    return false;
                }

                break;
            }

            updatedText = working;
            error = appendedAny ? string.Empty : firstError;
            return appendedAny;
        }

        internal static bool TryInsertBestEffort(string currentText, int insertionIndex, string insertedText, out string updatedText, out int insertedLength, out string error, Encoding encoding = null)
        {
            string original = currentText ?? string.Empty;
            string incoming = insertedText ?? string.Empty;
            int safeInsertionIndex = Math.Clamp(insertionIndex, 0, original.Length);

            if (incoming.Length == 0)
            {
                updatedText = original;
                insertedLength = 0;
                error = string.Empty;
                return true;
            }

            string working = original;
            int workingInsertionIndex = safeInsertionIndex;
            string firstError = string.Empty;
            bool insertedAny = false;

            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(incoming);
            while (enumerator.MoveNext())
            {
                string textElement = enumerator.GetTextElement();
                string candidate = working.Insert(workingInsertionIndex, textElement);
                if (TryNormalizeForEdit(candidate, out string normalizedCandidate, out string candidateError, encoding))
                {
                    working = normalizedCandidate;
                    workingInsertionIndex += textElement.Length;
                    insertedAny = true;
                    continue;
                }

                if (string.IsNullOrEmpty(firstError))
                {
                    firstError = candidateError;
                }

                if (candidateError == $"Macro names can use up to {MaxNameBytes} bytes.")
                {
                    break;
                }

                if (!insertedAny)
                {
                    updatedText = original;
                    insertedLength = 0;
                    error = candidateError;
                    return false;
                }

                break;
            }

            updatedText = working;
            insertedLength = workingInsertionIndex - safeInsertionIndex;
            error = insertedAny ? string.Empty : firstError;
            return insertedAny;
        }

        internal static string GetInsertablePrefix(string currentText, int insertionIndex, string insertedText, out string error, Encoding encoding = null)
        {
            string original = currentText ?? string.Empty;
            string incoming = insertedText ?? string.Empty;
            int safeInsertionIndex = Math.Clamp(insertionIndex, 0, original.Length);

            if (incoming.Length == 0)
            {
                error = string.Empty;
                return string.Empty;
            }

            string working = original;
            int workingInsertionIndex = safeInsertionIndex;
            string firstError = string.Empty;
            StringBuilder insertedPrefix = new();

            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(incoming);
            while (enumerator.MoveNext())
            {
                string textElement = enumerator.GetTextElement();
                string candidate = working.Insert(workingInsertionIndex, textElement);
                if (TryNormalizeForEdit(candidate, out string normalizedCandidate, out string candidateError, encoding))
                {
                    insertedPrefix.Append(textElement);
                    working = normalizedCandidate;
                    workingInsertionIndex += textElement.Length;
                    continue;
                }

                if (string.IsNullOrEmpty(firstError))
                {
                    firstError = candidateError;
                }

                break;
            }

            error = insertedPrefix.Length > 0 ? string.Empty : firstError;
            return insertedPrefix.ToString();
        }

        internal static string BuildCompositionPreview(string currentText, int insertionIndex, string compositionText, out string error, Encoding encoding = null)
        {
            string sanitized = RemoveControlCharacters(compositionText);
            if (sanitized.Length == 0)
            {
                error = string.Empty;
                return string.Empty;
            }

            return GetInsertablePrefix(currentText, insertionIndex, sanitized, out error, encoding);
        }

        internal static int GetPreviousCaretStop(string text, int caretIndex)
        {
            string value = text ?? string.Empty;
            int safeCaretIndex = Math.Clamp(caretIndex, 0, value.Length);
            if (safeCaretIndex <= 0 || value.Length == 0)
            {
                return 0;
            }

            int previousStop = 0;
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
            while (enumerator.MoveNext())
            {
                int elementIndex = enumerator.ElementIndex;
                if (elementIndex >= safeCaretIndex)
                {
                    break;
                }

                previousStop = elementIndex;
            }

            return previousStop;
        }

        internal static int GetNextCaretStop(string text, int caretIndex)
        {
            string value = text ?? string.Empty;
            int safeCaretIndex = Math.Clamp(caretIndex, 0, value.Length);
            if (safeCaretIndex >= value.Length || value.Length == 0)
            {
                return value.Length;
            }

            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
            while (enumerator.MoveNext())
            {
                int elementIndex = enumerator.ElementIndex;
                string element = enumerator.GetTextElement();
                int nextStop = elementIndex + element.Length;
                if (safeCaretIndex < nextStop)
                {
                    return nextStop;
                }
            }

            return value.Length;
        }

        internal static bool TryRemoveTextElementBeforeCaret(string text, int caretIndex, out string updatedText, out int updatedCaretIndex)
        {
            string value = text ?? string.Empty;
            int safeCaretIndex = Math.Clamp(caretIndex, 0, value.Length);
            if (safeCaretIndex <= 0 || value.Length == 0)
            {
                updatedText = value;
                updatedCaretIndex = safeCaretIndex;
                return false;
            }

            (int removeStart, int removeEnd) = GetTextElementBeforeOrContainingCaret(value, safeCaretIndex);
            int removeLength = removeEnd - removeStart;
            updatedText = value.Remove(removeStart, removeLength);
            updatedCaretIndex = removeStart;
            return true;
        }

        internal static bool TryRemoveTextElementAtCaret(string text, int caretIndex, out string updatedText, out int updatedCaretIndex)
        {
            string value = text ?? string.Empty;
            int safeCaretIndex = Math.Clamp(caretIndex, 0, value.Length);
            if (safeCaretIndex >= value.Length || value.Length == 0)
            {
                updatedText = value;
                updatedCaretIndex = safeCaretIndex;
                return false;
            }

            (int removeStart, int removeEnd) = GetTextElementAtOrContainingCaret(value, safeCaretIndex);
            updatedText = value.Remove(removeStart, removeEnd - removeStart);
            updatedCaretIndex = removeStart;
            return true;
        }

        private static (int Start, int End) GetTextElementBeforeOrContainingCaret(string value, int caretIndex)
        {
            int previousStart = 0;
            int previousEnd = 0;
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
            while (enumerator.MoveNext())
            {
                int start = enumerator.ElementIndex;
                int end = start + enumerator.GetTextElement().Length;
                if (caretIndex <= start)
                {
                    break;
                }

                if (caretIndex <= end)
                {
                    return (start, end);
                }

                previousStart = start;
                previousEnd = end;
            }

            return previousEnd > previousStart
                ? (previousStart, previousEnd)
                : (0, Math.Min(value.Length, Math.Max(1, caretIndex)));
        }

        private static (int Start, int End) GetTextElementAtOrContainingCaret(string value, int caretIndex)
        {
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
            while (enumerator.MoveNext())
            {
                int start = enumerator.ElementIndex;
                int end = start + enumerator.GetTextElement().Length;
                if (caretIndex >= start && caretIndex < end)
                {
                    return (start, end);
                }
            }

            return (caretIndex, Math.Min(value.Length, caretIndex + 1));
        }

        internal static bool TryNormalizeForEdit(string text, out string normalized, out string error, Encoding encoding = null)
        {
            string candidate = text ?? string.Empty;
            if (candidate.Length == 0)
            {
                normalized = string.Empty;
                error = string.Empty;
                return true;
            }

            foreach (char ch in candidate)
            {
                if (char.IsControl(ch))
                {
                    normalized = null;
                    error = "Macro names cannot contain control characters.";
                    return false;
                }
            }

            if (TryGetBlacklistedCharacterError(candidate, out error))
            {
                normalized = null;
                return false;
            }

            Encoding activeEncoding = GetStrictEncoding(encoding);
            try
            {
                int byteCount = activeEncoding.GetByteCount(candidate);
                if (byteCount > MaxNameBytes)
                {
                    normalized = null;
                    error = $"Macro names can use up to {MaxNameBytes} bytes.";
                    return false;
                }
            }
            catch (EncoderFallbackException)
            {
                normalized = null;
                error = "This character is not available in the current system locale.";
                return false;
            }

            normalized = candidate;
            error = string.Empty;
            return true;
        }

        private static Encoding GetStrictEncoding(Encoding encoding)
        {
            Encoding sourceEncoding = encoding ?? GetActiveAnsiEncoding();
            return Encoding.GetEncoding(
                sourceEncoding.CodePage,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback);
        }

        internal static Encoding GetActiveAnsiEncoding()
        {
            int codePage = OperatingSystem.IsWindows()
                ? GetACP()
                : CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
            return Encoding.GetEncoding(codePage);
        }

        [DllImport("kernel32.dll")]
        private static extern int GetACP();

        private static bool TryGetBlacklistedCharacterError(string text, out string error)
        {
            string value = text ?? string.Empty;
            foreach (Rune rune in value.EnumerateRunes())
            {
                if (IsAllowedMacroNameRune(rune))
                {
                    continue;
                }

                error = "This character is not allowed in macro names.";
                return true;
            }

            error = string.Empty;
            return false;
        }

        private static bool IsAllowedMacroNameRune(Rune rune)
        {
            if (rune.Value == '-' || rune.Value == '_')
            {
                return true;
            }

            UnicodeCategory category = Rune.GetUnicodeCategory(rune);
            return category switch
            {
                UnicodeCategory.UppercaseLetter => true,
                UnicodeCategory.LowercaseLetter => true,
                UnicodeCategory.TitlecaseLetter => true,
                UnicodeCategory.ModifierLetter => true,
                UnicodeCategory.OtherLetter => true,
                UnicodeCategory.DecimalDigitNumber => true,
                UnicodeCategory.LetterNumber => true,
                UnicodeCategory.OtherNumber => true,
                UnicodeCategory.SpaceSeparator => true,
                UnicodeCategory.NonSpacingMark => true,
                UnicodeCategory.SpacingCombiningMark => true,
                UnicodeCategory.EnclosingMark => true,
                _ => false
            };
        }

        private static string RemoveControlCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new(text.Length);
            foreach (char ch in text)
            {
                if (!char.IsControl(ch))
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString();
        }
    }
}
