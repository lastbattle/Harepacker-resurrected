using Microsoft.Xna.Framework.Input;

namespace HaCreator.MapSimulator.UI
{
    internal static class SkillMacroOwnerKeyHandler
    {
        internal const int ClientForwardedFunctionKeyCount = 12;

        internal static bool ShouldCloseWindow(KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            return keyboardState.IsKeyDown(Keys.Escape)
                && previousKeyboardState.IsKeyUp(Keys.Escape);
        }

        internal static bool TryGetClientForwardedFunctionKeyIndex(Keys key, out int functionKeyIndex)
        {
            if (key >= Keys.F1 && key <= Keys.F12)
            {
                functionKeyIndex = key - Keys.F1;
                return true;
            }

            functionKeyIndex = -1;
            return false;
        }

        internal static bool ShouldApplyCaretBoundaryNavigation(bool controlHeld)
        {
            // `CCtrlEdit::OnKey` forwards Ctrl+Home/Ctrl+End to the parent owner path
            // instead of moving the local edit caret.
            return !controlHeld;
        }
    }
}
