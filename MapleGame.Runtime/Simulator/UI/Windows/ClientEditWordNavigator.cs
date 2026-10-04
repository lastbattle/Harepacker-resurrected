using System;

namespace HaCreator.MapSimulator.UI
{
    internal static class ClientEditWordNavigator
    {
        internal static bool IsClientWordSeparator(char character)
        {
            return character is ' ' or '\t' or '\r' or '\n';
        }

        internal static int FindPreviousWordBoundary(string text, int caretPosition)
        {
            string value = text ?? string.Empty;
            if (value.Length == 0 || caretPosition <= 0)
            {
                return 0;
            }

            int cursor = Math.Clamp(caretPosition, 0, value.Length);
            while (cursor > 0 && IsClientWordSeparator(value[cursor - 1]))
            {
                cursor--;
            }

            while (cursor > 0 && !IsClientWordSeparator(value[cursor - 1]))
            {
                cursor--;
            }

            return cursor;
        }

        internal static int FindNextWordBoundary(string text, int caretPosition)
        {
            string value = text ?? string.Empty;
            if (value.Length == 0 || caretPosition >= value.Length)
            {
                return value.Length;
            }

            int cursor = Math.Clamp(caretPosition, 0, value.Length);
            while (cursor < value.Length && IsClientWordSeparator(value[cursor]))
            {
                cursor++;
            }

            while (cursor < value.Length && !IsClientWordSeparator(value[cursor]))
            {
                cursor++;
            }

            return cursor;
        }

        internal static bool TryGetWordSelectionRange(string text, int caretPosition, out int selectionStart, out int selectionEnd)
        {
            string value = text ?? string.Empty;
            if (value.Length == 0)
            {
                selectionStart = 0;
                selectionEnd = 0;
                return false;
            }

            int clampedCaret = Math.Clamp(caretPosition, 0, value.Length);
            int characterIndex = clampedCaret >= value.Length
                ? value.Length - 1
                : clampedCaret;
            if (characterIndex < 0 || IsClientWordSeparator(value[characterIndex]))
            {
                selectionStart = clampedCaret;
                selectionEnd = clampedCaret;
                return false;
            }

            selectionStart = characterIndex;
            while (selectionStart > 0 && !IsClientWordSeparator(value[selectionStart - 1]))
            {
                selectionStart--;
            }

            selectionEnd = characterIndex;
            while (selectionEnd < value.Length && !IsClientWordSeparator(value[selectionEnd]))
            {
                selectionEnd++;
            }

            return selectionEnd > selectionStart;
        }
    }
}
