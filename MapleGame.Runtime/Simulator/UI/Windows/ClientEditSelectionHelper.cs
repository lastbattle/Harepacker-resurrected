using System;

namespace HaCreator.MapSimulator.UI
{
    internal static class ClientEditSelectionHelper
    {
        internal static int GetSelectionStart(int textLength, int selectionAnchor, int caretIndex)
        {
            if (selectionAnchor < 0)
            {
                return -1;
            }

            int safeLength = Math.Max(0, textLength);
            int safeAnchor = Math.Clamp(selectionAnchor, 0, safeLength);
            int safeCaret = Math.Clamp(caretIndex, 0, safeLength);
            return Math.Min(safeAnchor, safeCaret);
        }

        internal static int GetSelectionLength(int textLength, int selectionAnchor, int caretIndex)
        {
            if (selectionAnchor < 0)
            {
                return 0;
            }

            int safeLength = Math.Max(0, textLength);
            int safeAnchor = Math.Clamp(selectionAnchor, 0, safeLength);
            int safeCaret = Math.Clamp(caretIndex, 0, safeLength);
            return Math.Abs(safeCaret - safeAnchor);
        }

        internal static int ResolveNavigationCaret(int textLength, int selectionAnchor, int caretIndex, bool moveRight)
        {
            int selectionStart = GetSelectionStart(textLength, selectionAnchor, caretIndex);
            int selectionLength = GetSelectionLength(textLength, selectionAnchor, caretIndex);
            if (selectionStart < 0 || selectionLength <= 0)
            {
                return Math.Clamp(caretIndex, 0, Math.Max(0, textLength));
            }

            return moveRight
                ? selectionStart + selectionLength
                : selectionStart;
        }

        internal static bool TryDeleteSelection(string text, int selectionAnchor, int caretIndex, out string updatedText, out int updatedCaretIndex)
        {
            string currentText = text ?? string.Empty;
            int selectionStart = GetSelectionStart(currentText.Length, selectionAnchor, caretIndex);
            int selectionLength = GetSelectionLength(currentText.Length, selectionAnchor, caretIndex);
            if (selectionStart < 0 || selectionLength <= 0)
            {
                updatedText = currentText;
                updatedCaretIndex = Math.Clamp(caretIndex, 0, currentText.Length);
                return false;
            }

            updatedText = currentText.Remove(selectionStart, selectionLength);
            updatedCaretIndex = selectionStart;
            return true;
        }
    }
}
