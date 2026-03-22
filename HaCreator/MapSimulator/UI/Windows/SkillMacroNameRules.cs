using System;
using System.Globalization;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    internal static class SkillMacroNameRules
    {
        internal const int MaxNameBytes = 12;

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
            Encoding sourceEncoding = encoding ?? Encoding.Default;
            return Encoding.GetEncoding(
                sourceEncoding.CodePage,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback);
        }
    }
}
