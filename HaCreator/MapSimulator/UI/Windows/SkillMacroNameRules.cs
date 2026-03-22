using System;
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
